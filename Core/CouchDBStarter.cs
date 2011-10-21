using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using Common.Logging;

namespace CouchDude.Bootstrapper
{
	/// <summary>CouchDB watching agent.</summary>
	public class CouchDBStarter
	{
		private const int IsRespondingTimeout = 60000;

		private readonly BootstrapSettings settings;
		private static readonly ILog Log = LogManager.GetLogger(typeof(CouchDBStarter)); 

		private readonly DirectoryInfo couchDbBinFolder;
		private readonly FileInfo startCouchDbBatchFile;
		private Process batchProcess;

		/// <constructor />
		public CouchDBStarter(BootstrapSettings settings)
		{
			this.settings = settings;
			if (!settings.BinDirectory.Exists)
				throw new Exception("CouchDB folder does not exist");

			startCouchDbBatchFile = settings.BinDirectory.EnumerateFiles("couchdb.bat", SearchOption.AllDirectories).First();
			couchDbBinFolder = startCouchDbBatchFile.Directory;
		}

		/// <summary>Starts CouchDB process.</summary>
		public void Start()
		{
			Log.Info("Starting CouchDB process...");

			Environment.SetEnvironmentVariable("ERL", "erl.exe");

			var processStartInfo = new ProcessStartInfo(startCouchDbBatchFile.FullName, couchDbBinFolder.FullName) {
				CreateNoWindow = true,
				UseShellExecute = false
			};
			
			batchProcess = Process.Start(processStartInfo);

			WaitTillResponding();
		}

		/// <summary>Throws <see cref="InvalidOperationException"/> if CouchDB launch script process exites.</summary>
		public void ThrowIfExitedUnexpectedly()
		{
			if (batchProcess.HasExited)
				throw new InvalidOperationException("couchdb.bat has exited with exit code " + batchProcess.ExitCode);
		}
		
		/// <summary>Blocks current thread until CouchDB is responding OK.</summary>
		private void WaitTillResponding()
		{
			var pingAddress = settings.EndpointToListenOn.ToHttpUri();
			Log.InfoFormat("Wating for CouchDB to respond on {0}", pingAddress);

			using (var httpClient = new HttpClient())
			{
				var stopwatch = new Stopwatch();
				stopwatch.Start();
				while (true)
				{
					ThrowIfExitedUnexpectedly();

					if(stopwatch.ElapsedMilliseconds > IsRespondingTimeout)	
						throw new Exception(string.Format("CouchDB has not responded for {0} milliseconds", stopwatch.ElapsedMilliseconds));
					try
					{
						var response = httpClient.Get(pingAddress);
						if (response.IsSuccessStatusCode)
						{
							Log.InfoFormat("CouchDB is up and avaliable at {0}", pingAddress);
							return;
						}
					}
					catch (HttpRequestException) { }
				}
			}
		}
	}
}
