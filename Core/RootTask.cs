namespace CouchDude.Bootstrapper
{
	class RootTask : StartupTaskBase
	{
		public override System.Collections.Generic.IEnumerable<string> Dependencies
		{
			get { return new[] { "ExtractCouchDb", "SetAcls", "UpdateIniFile" }; }
		}
	}
}