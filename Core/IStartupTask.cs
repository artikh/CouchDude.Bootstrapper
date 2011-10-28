using System;
using System.Collections.Generic;

namespace CouchDude.Bootstrapper
{
	interface IStartupTask
	{
		string Name { get; }
		IEnumerable<Type> Dependencies { get; }
		void Invoke(BootstrapSettings settings);
	}
}