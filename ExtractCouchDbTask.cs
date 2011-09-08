using System.Collections.Generic;
using System.IO;
using Common.Logging;
using Ionic.Zip;

namespace CouchDude.Bootstrapper
{
	class ExtractCouchDbTask: StartupTaskBase
	{
		static readonly ILog Log = LogManager.GetLogger(typeof(ExtractCouchDbTask));

		public override IEnumerable<string> Dependencies { get { return new[] { "SetAcls" }; } }

		public override void Invoke(BootstrapSettings settings)
		{
			Extract(settings.CouchDBDistributive, settings.CouchDBDirectory);
			if (settings.SetupCouchDBLucene)
				Extract(settings.CouchDBLuceneDistributive, settings.CouchDBLuceneDirectory);
		}

		private static void Extract(FileInfo distributiveFile, DirectoryInfo targetDir)
		{
			Log.InfoFormat("Extracting \"{0}\" -> \"{1}\"", distributiveFile.FullName, targetDir.FullName);

			if (!targetDir.Exists)
			{
				Log.InfoFormat("Creating \"{0}\"...", targetDir.FullName);
				targetDir.Create();
			}

			using (var distributiveStream = distributiveFile.OpenRead())
			using (var distributive = ZipFile.Read(distributiveStream))
			{
				distributive.ExtractAll(targetDir.FullName, ExtractExistingFileAction.OverwriteSilently);
				Log.InfoFormat("Extracted \"{0}\" -> \"{1}\"", distributiveFile.FullName, targetDir.FullName);
			}
		}
	}

}