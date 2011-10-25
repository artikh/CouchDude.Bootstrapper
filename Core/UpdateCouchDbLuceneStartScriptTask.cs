using System;
using System.IO;
using System.Text;

namespace CouchDude.Bootstrapper
{
	internal class UpdateCouchDbLuceneStartScriptTask : StartupTaskBase
	{
		public override System.Collections.Generic.IEnumerable<string> Dependencies { get { yield return "ExtractCouchDb"; } }

		public override void Invoke(BootstrapSettings settings)
		{
			if (!settings.SetupCouchDBLucene) return;

			var luceneRunScript = new FileInfo(Path.Combine(settings.CouchDBLuceneDirectory.FullName, @"bin\run.bat"));

			if(!luceneRunScript.Exists)
				throw new InvalidOperationException("couchdb-lucene run script missing. Expected to be at: " + luceneRunScript.FullName);

			var javaExecutable = new FileInfo(Path.Combine(settings.JavaDirectory.FullName, @"bin\java.exe"));
			if(!javaExecutable.Exists)
				throw new InvalidOperationException("java.exe missing. Expected to be at: " + javaExecutable.FullName);
			
			var originalContent = File.ReadAllText(luceneRunScript.FullName);
			var edititedContent = originalContent.Replace("java.exe", string.Format("\"{0}\"", javaExecutable.FullName));
			File.WriteAllText(luceneRunScript.FullName, edititedContent, Encoding.Default);
		}
	}
}