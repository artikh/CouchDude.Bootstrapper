using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using Common.Logging;

namespace CouchDude.Bootstrapper
{
	/// <summary>Initializes, sets up and runs CouchDB (+couchdb-lucene) instance.</summary>
	public static class CouchDBBootstraper
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(CouchDBBootstraper));

		/// <summary>Initializes, sets up and runs CouchDB (+couchdb-lucene) instance.</summary>
		public static CouchDBWatchdog Bootstrap(BootstrapSettings settings)
		{
			if(RunStartupTasks(settings))
				return new CouchDBWatchdog(settings);
			return null;
		}

		private static bool RunStartupTasks(BootstrapSettings settings)
		{
			try
			{
				var tasks =
					Assembly.GetExecutingAssembly()
						.GetTypes()
						.Where(t => typeof(IStartupTask).IsAssignableFrom(t))
						.Where(t => !t.IsAbstract)
						.Select(Activator.CreateInstance)
						.Cast<IStartupTask>()
						.ToDictionary(t => t.Name, t => t);

				var rootTask = tasks.Values.First(t => t.Name == "Root");
				ExecuteTaskRecursively(rootTask, tasks, new HashSet<string>(), settings);
			}
			catch (Exception e)
			{
				Log.Error(e);
				return false;
			}

			return true;
		}

		private static void ExecuteTaskRecursively(
			IStartupTask task, IDictionary<string, IStartupTask> tasks, ISet<string> executedTasks, BootstrapSettings settings)
		{
			foreach (var dependencyTaskName in task.Dependencies.Where(t => !executedTasks.Contains(t)))
			{
				IStartupTask dependencyTask;
				if (!tasks.TryGetValue(dependencyTaskName, out dependencyTask))
					throw new Exception("Unknown depended task " + dependencyTaskName);
				ExecuteTaskRecursively(dependencyTask, tasks, executedTasks, settings);
			}
			Log.InfoFormat("Executing {0} startup task", task.Name);
			task.Invoke(settings);
			Log.InfoFormat("{0} startup task have executed successfully", task.Name);
			executedTasks.Add(task.Name);
		}
	}
}
