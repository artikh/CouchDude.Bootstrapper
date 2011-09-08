using System.Collections.Generic;

namespace CouchDude.Bootstrapper
{
	internal abstract class StartupTaskBase : IStartupTask
	{
		private static readonly string[] EmptyDependencyList = new string[0];

		public virtual IEnumerable<string> Dependencies { get { return EmptyDependencyList; } }

		public virtual void Invoke(BootstrapSettings settings) { }

		public virtual string Name
		{
			get
			{
				const string suffix = "Task";
				var currentTaskTypeName = GetType().Name;
				if (currentTaskTypeName.EndsWith(suffix))
					return currentTaskTypeName.Substring(0, currentTaskTypeName.Length - suffix.Length);
				else
					return currentTaskTypeName;
			}
		}
	}
}