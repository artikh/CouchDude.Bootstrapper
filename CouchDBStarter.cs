using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

			Process.Start(new ProcessStartInfo(startCouchDbBatchFile.FullName, couchDbBinFolder.FullName) {
				CreateNoWindow = true,
				UseShellExecute = false
			});

			WaitTillResponding();
		}
		
		private static Task<bool> IsResponding(HttpClient httpClient, Uri couchDBUri)
		{
			return httpClient
				.GetAsync(couchDBUri)
				.ContinueWith(
					requestTask => {
						if (requestTask.IsFaulted)
							return Task.Factory.StartNew(() => false);
						else if (requestTask.Result.IsSuccessStatusCode)
							return Task.Factory.StartNew(() => true);
						else
							return IsResponding(httpClient, couchDBUri);
					})
				.Unwrap();
		}

		/// <summary>Blocks current thread until CouchDB is responding OK.</summary>
		public void WaitTillResponding()
		{
			var pingAddress = settings.EndpointToListenOn.ToHttpUri();
			Log.InfoFormat("Wating for CouchDB to respond on {0}", pingAddress);

			using (var httpClient = new HttpClient())
				IsResponding(httpClient, pingAddress).Wait(IsRespondingTimeout);

			Log.InfoFormat("CouchDB is up");
		}
	}
}
