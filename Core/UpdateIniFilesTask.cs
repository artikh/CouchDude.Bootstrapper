using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Common.Logging;
using CouchDude.Utils;


namespace CouchDude.Bootstrapper
{
	class UpdateIniFilesTask: StartupTaskBase
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(UpdateIniFilesTask));

		public override System.Collections.Generic.IEnumerable<string> Dependencies
		{
			get
			{
				yield return "UpdateCouchDbLuceneStartScript";
				yield return "ExtractCouchDb";
			}
		}

		public override void Invoke(BootstrapSettings settings)
		{
			var localIniFilePath = Path.Combine(settings.CouchDBDirectory.FullName, @"etc\couchdb\local.ini");
			var iniFile = new IniFile(localIniFilePath);

			UpdateDatabaseLocation(iniFile, Path.Combine(settings.DataDirectory.FullName, "couchdb"));
			UpdateLogLocation(iniFile, settings.LogDirectory.FullName);

			if (settings.SetupCouchDBLucene)
			{
				var couchDBLuceneConfigFileFullName = Path.Combine(settings.CouchDBLuceneDirectory.FullName, @"conf\couchdb-lucene.ini");
				var couchdbLuceneIniFile = new IniFile(couchDBLuceneConfigFileFullName);

				// ReSharper disable PossibleInvalidOperationException
				var port = settings.CouchDBLucenePort.Value;
				// ReSharper restore PossibleInvalidOperationException

				UpdateLuceneListeningPort(couchdbLuceneIniFile, port, settings.EndpointToListenOn.ToHttpUri());
				UpdateLuceneDataDirectory(couchdbLuceneIniFile, Path.Combine(settings.DataDirectory.FullName, "couchdb"));
				UpdateLuceneLogDirectory(settings.CouchDBLuceneDirectory, settings.LogDirectory.FullName);

				AddCouchDBLuceneHooks(iniFile, settings.CouchDBLuceneDirectory, port);
			}

			UpdateIPSettings(iniFile, settings.EndpointToListenOn);

			string userName = settings.AdminUserName;
			string plainTextPassword = settings.AdminPlainTextPassword;
			if(userName.HasValue() && plainTextPassword.HasValue())
				AddAdminUser(iniFile, userName, plainTextPassword);
		}

		private void UpdateLuceneLogDirectory(DirectoryInfo couchDBLuceneDirectory, string logDirectoryName)
		{
			var log4JConfigFileName = Path.Combine(couchDBLuceneDirectory.FullName, @"conf\log4j.xml");
			var logFileName = Path.Combine(logDirectoryName, "couchdb-lucene.log");

			Log.InfoFormat("Setting couchdb-lucene log file to {0} in config file: {1}", logFileName, log4JConfigFileName);

			// ReSharper disable PossibleNullReferenceException
			var log4JConfigXml = XDocument.Load(log4JConfigFileName);
			var fileNameNode =
				log4JConfigXml
					.Element("log4j:configuration")
					.Elements("log4j:appender")
					.Where(e => e.Attribute("name").Value == "FILE")
					.Elements("log4j:param")
					.First(e => e.Attribute("name").Value == "file");
			// ReSharper restore PossibleNullReferenceException

			fileNameNode.SetAttributeValue("value", logFileName);
			log4JConfigXml.Save(log4JConfigFileName);
		}

		private void UpdateLuceneDataDirectory(IniFile couchdbLuceneIniFile, string dataDirectory)
		{
			Log.InfoFormat("Setting couchdb-lucene data directory to {0} in INI file: {1}", dataDirectory, couchdbLuceneIniFile.Path);

			couchdbLuceneIniFile.WriteValue("lucene", "dir", dataDirectory);
		}

		static void UpdateLuceneListeningPort(IniFile couchdbLuceneIniFile, int port, Uri baseUrl)
		{
			Log.InfoFormat("Setting couchdb-lucene port to {0} in INI file: {1}", port, couchdbLuceneIniFile.Path);

			couchdbLuceneIniFile.WriteValue("lucene", "port", port.ToString());
			couchdbLuceneIniFile.WriteValue("local", "url", baseUrl.ToString());
		}

		static void AddCouchDBLuceneHooks(IniFile iniFile, FileSystemInfo couchDBLuceneDirectory, int port)
		{
			var luceneRunScriptFullName = Path.Combine(couchDBLuceneDirectory.FullName, @"bin\run.bat");

			Log.InfoFormat(
				"Updating INI file {0} run couchdb-lucene: {1}", iniFile.Path, luceneRunScriptFullName);

			iniFile.WriteValue(
				"httpd_global_handlers", 
				"_fti", 
				string.Format("{{couch_httpd_proxy, handle_proxy_req, <<\"http://127.0.0.1:{0}\">>}}", port));

			iniFile.WriteValue("os_daemons", "couchdb-lucene", luceneRunScriptFullName);
		}

		static void UpdateLogLocation(IniFile iniFile, string logDirectoryName)
		{
			var logFileName = Path.Combine(logDirectoryName, "couchdb.log");
			Log.InfoFormat("Updating INI file {0} to store log file here: {1}", iniFile.Path, logFileName);

			iniFile.WriteValue("log", "file", logFileName);
		}

		static void UpdateDatabaseLocation(IniFile iniFile, string databasePath)
		{
			Log.InfoFormat(
				"Updating INI file {0} to store database files in this folder: {1}", iniFile.Path, databasePath);

			iniFile.WriteValue("couchdb", "database_dir", databasePath);
		}

		static void AddAdminUser(IniFile iniFile, string userName, string plainTextPassword)
		{
			Log.InfoFormat(
				"Updating INI file {0} adding new administrator {1}", iniFile.Path, userName);
			
			// CouchDB will hash plain text password when it'll start.
			iniFile.WriteValue("admins", userName, plainTextPassword);

			iniFile.WriteValue("httpd", "WWW-Authenticate", @"Basic realm=""administrator""");
			iniFile.WriteValue("couch_httpd_auth", "require_valid_user", @"true");
		}

		static void UpdateIPSettings(IniFile iniFile, IPEndPoint ipEndpoint)
		{
			Log.InfoFormat(
				"Updating INI file {0} to use configured enpoint {1}", iniFile.Path, ipEndpoint);
			iniFile.WriteValue("httpd", "port", ipEndpoint.Port);
			iniFile.WriteValue("httpd", "bind_address", "0.0.0.0");
		}
	}
}