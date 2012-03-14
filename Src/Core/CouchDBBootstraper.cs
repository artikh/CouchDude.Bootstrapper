using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

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
			settings.Lock();

			RunStartupTasks(settings);
			var watchdog = new CouchDBWatchdog(settings);
			watchdog.Start();
			CouchDBReplicator.UpdateReplicationState(
				new IPEndPoint(IPAddress.Loopback, settings.EndpointToListenOn.Port), 
				settings.ReplicationSettings
			);
			return watchdog;
		}

		private static void RunStartupTasks(BootstrapSettings settings)
		{
			ExecuteTaskRecursively(Activator.CreateInstance<RootTask>(), new HashSet<Type>(), settings);
		}

		private static void ExecuteTaskRecursively(
			IStartupTask task, ISet<Type> executedTasks, BootstrapSettings settings)
		{
			var dependencies =
				from dependencyType in task.Dependencies
				where !executedTasks.Contains(dependencyType)
				select (IStartupTask) Activator.CreateInstance(dependencyType);

			foreach (var dependencyTask in dependencies)
				ExecuteTaskRecursively(dependencyTask, executedTasks, settings);

			Log.InfoFormat("Executing {0} startup task", task.Name);
			task.Invoke(settings);
			Log.InfoFormat("{0} startup task have executed successfully", task.Name);
			executedTasks.Add(task.GetType());
		}
	}
}
