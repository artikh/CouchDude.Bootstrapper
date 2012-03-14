using System;

namespace CouchDude.Bootstrapper
{
	class RootTask : StartupTaskBase
	{
		public override System.Collections.Generic.IEnumerable<Type> Dependencies
		{
			get { return new[] { typeof(ExtractCouchDbTask), typeof(UpdateIniFilesTask) }; }
		}
	}
}