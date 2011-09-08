using System.Collections.Generic;

namespace CouchDude.Bootstrapper
{
	interface IStartupTask
	{
		string Name { get; }
		IEnumerable<string> Dependencies { get; }
		void Invoke(BootstrapSettings settings);
	}
}