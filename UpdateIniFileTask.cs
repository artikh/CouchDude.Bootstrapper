using System;
using System.IO;
using System.Net;
using Common.Logging;
using CouchDude.Utils;


namespace CouchDude.Bootstrapper
{
	class UpdateIniFileTask: StartupTaskBase
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(UpdateIniFileTask));

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

			UpdateDatabaseLocation(iniFile, settings.DataDirectory.FullName);
			UpdateLogLocation(iniFile, settings.LogDirectory);

			if (settings.SetupCouchDBLucene)
			{
				// ReSharper disable PossibleInvalidOperationException
				var port = settings.CouchDBLucenePort.Value;
				// ReSharper restore PossibleInvalidOperationException

				UpdateLuceneListeningPort(settings.CouchDBLuceneDirectory, port, settings.EndpointToListenOn.ToHttpUri());
				AddCouchDBLuceneHooks(iniFile, settings.CouchDBLuceneDirectory, port);
			}

			UpdateIPSettings(iniFile, settings.EndpointToListenOn);

			string userName = settings.AdminUserName;
			string plainTextPassword = settings.AdminPlainTextPassword;
			if(userName.HasValue() && plainTextPassword.HasValue())
				AddAdminUser(iniFile, userName, plainTextPassword);
		}

		static void UpdateLuceneListeningPort(DirectoryInfo couchDBLuceneDirectory, int port, Uri baseUrl)
		{
			var couchDBLuceneConfigFileFullName = Path.Combine(couchDBLuceneDirectory.FullName, @"conf\couchdb-lucene.ini");
			Log.InfoFormat("Setting couchdb-lucene port to {0} in INI file: {1}", port, couchDBLuceneConfigFileFullName);

			var iniFile = new IniFile(couchDBLuceneConfigFileFullName);
			iniFile.WriteValue("lucene", "port", port.ToString());
			iniFile.WriteValue("local", "url", baseUrl.ToString());
		}

		static void AddCouchDBLuceneHooks(IniFile iniFile, DirectoryInfo couchDBLuceneDirectory, int port)
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

		static void UpdateLogLocation(IniFile iniFile, DirectoryInfo logDirectory)
		{
			var logFileName = Path.Combine(logDirectory.FullName, "couchdb.log");
			Log.InfoFormat(
				"Updating INI file {0} to store log file here: {1}", iniFile.Path, logFileName);

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