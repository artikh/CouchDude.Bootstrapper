using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using CLAP;
using Common.Logging;

namespace CouchDude.Bootstrapper.Console
{
	[DefaultVerb("Default")]
	class BootstrapperConsole
	{
		private static readonly ILog Log = LogManager.GetCurrentClassLogger();

		[Verb]
		public static void Help()
		{
			var execName = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
			System.Console.WriteLine(
				string.Format(
					"Usage:\n\t{0} --ports=8081,8082,8083 --replicate-databases=db1,db2,db3 --admin-user-name=admin --admin-password=passw0rd", execName));
		}

		[Verb]
		public static void Default(
			[Parameter(Required = true)] int[] ports,
			[Parameter(Aliases = "databases")] string[] databasesToReplicate,
			[Parameter(Aliases = "admin-user-name")] string adminUserName,
			[Parameter(Aliases = "admin-user-password")] string adminPassword
		)
		{
			try
			{
				var replicationSettings =
					new ReplicationSettings {
						EndPointsToReplicateTo = (
							from p in ports
							select new IPEndPoint(IPAddress.Loopback, p)
							).ToArray(),
						DatabasesToReplicate = databasesToReplicate ?? new string[0]
					};

				foreach (var endPoint in replicationSettings.EndPointsToReplicateTo)
					StartCouchInstance(endPoint.Port - 10, endPoint.Port, replicationSettings);

				System.Console.Write("Press ENTER to quit:");
				System.Console.ReadLine();
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
		}

		private static void StartCouchInstance(int lucenePort, int port, ReplicationSettings replicationSettings)
		{
			var storageDir =
				new DirectoryInfo(Path.Combine(Path.GetTempPath(), "couch-bootstrapper-" + DateTime.Now.Ticks + port));
			storageDir.Create();

			var dataDir = new DirectoryInfo(Path.Combine(storageDir.FullName, "data"));
			dataDir.Create();
			var logDir = new DirectoryInfo(Path.Combine(storageDir.FullName, "logs"));
			logDir.Create();
			var binDir = new DirectoryInfo(Path.Combine(storageDir.FullName, "bin"));
			binDir.Create();

			var bootstrapSettings = new BootstrapSettings
			{
				CouchDBDistributive =
					new FileInfo(
						Path.Combine(Environment.CurrentDirectory, "couchdb-1.1.0+COUCHDB-1152_otp_R14B03_lean.zip")),
				CouchDBLuceneDistributive =
					new FileInfo(Path.Combine(Environment.CurrentDirectory, "couchdb-lucene-0.7.0.zip")),
				JavaDistributive = new FileInfo(Path.Combine(Environment.CurrentDirectory, "jre6.zip")),
				CouchDBLucenePort = lucenePort,
				BinDirectory = binDir,
				DataDirectory = dataDir,
				LogDirectory = logDir,
				EndpointToListenOn = new IPEndPoint(IPAddress.Loopback, port),
				SetupCouchDBLucene = true,
			};

			bootstrapSettings.ReplicationSettings.DatabasesToReplicate = replicationSettings.DatabasesToReplicate;
			bootstrapSettings.ReplicationSettings.EndPointsToReplicateTo = replicationSettings.EndPointsToReplicateTo;

			CouchDBBootstraper.Bootstrap(bootstrapSettings);
		}
	}
}