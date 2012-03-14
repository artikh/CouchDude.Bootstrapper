using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CLAP;
using Common.Logging;

namespace CouchDude.Bootstrapper.Console
{
	class BootstrapperConsole
	{
		[Verb(IsDefault = true)]
		public static void Bootstrap(
			[Parameter(Required = true)] int[] ports,
			[Parameter(Aliases = "databases")] string[] databasesToReplicate,
			[Parameter(Aliases = "admin-user-name")] string adminUserName,
			[Parameter(Aliases = "admin-user-password")] string adminPassword
		)
		{
			var replicationSettings =
				new ReplicationSettings {
					EndPointsToReplicateTo = (
						from p in ports
						select new IPEndPoint(IPAddress.Loopback, p)
						).ToArray(),
					DatabasesToReplicate = databasesToReplicate ?? new string[0]
				};

			CouchDBWatchdog[] watchdogs =
				replicationSettings
					.EndPointsToReplicateTo
					.Select(endPoint => StartCouchInstance(endPoint.Port - 10, endPoint.Port, replicationSettings))
					.ToArray();

			Task.Factory.StartNew(
				() =>
					{
						System.Console.Write("Press ENTER to quit:");
						System.Console.ReadLine();

						foreach (var watchdog in watchdogs)
							watchdog.TerminateIfRunning();

						shouldExit = true;
					});

			while (!shouldExit)
			{
				foreach (var watchdog in watchdogs)
					watchdog.ThrowIfExitedUnexpectedly();
				Thread.Sleep(2000);
			}
		}

		private static volatile bool shouldExit;

		private static CouchDBWatchdog StartCouchInstance(int lucenePort, int port, ReplicationSettings replicationSettings)
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
				CouchDBDistributive = new FileInfo(Path.Combine(Environment.CurrentDirectory, "couchdb-1.1.0.zip")),
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

			return CouchDBBootstraper.Bootstrap(bootstrapSettings);
		}
	}
}