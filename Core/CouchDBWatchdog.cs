using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using Common.Logging;

namespace CouchDude.Bootstrapper
{
	/// <summary>CouchDB watching agent.</summary>
	public class CouchDBWatchdog
	{
		private const int IsRespondingTimeout = 180000;

		private readonly BootstrapSettings settings;
		private static readonly ILog Log = LogManager.GetLogger(typeof(CouchDBWatchdog));
		private static readonly ILog CouchDBLog = LogManager.GetLogger("CouchDB");

		private readonly DirectoryInfo couchDbBinFolder;
		private readonly FileInfo startCouchDbBatchFile;
		private Process erlProcess;

		/// <constructor />
		public CouchDBWatchdog(BootstrapSettings settings)
		{
			this.settings = settings;
			if (!settings.BinDirectory.Exists)
				throw new Exception("CouchDB folder does not exist");

			startCouchDbBatchFile = settings.BinDirectory.EnumerateFiles("couchdb.bat", SearchOption.AllDirectories).First();
			couchDbBinFolder = startCouchDbBatchFile.Directory;
		}

		/// <summary>Starts CouchDB process.</summary>
		internal void Start()
		{
			Log.Info("Starting CouchDB process...");

			var processStartInfo = new ProcessStartInfo(
				Path.Combine(couchDbBinFolder.FullName, "erl.exe"), @"-sasl errlog_type error -s couch") {
				WorkingDirectory = couchDbBinFolder.FullName,
				CreateNoWindow = true,
				UseShellExecute = settings.UseShellExecute,
				RedirectStandardError = !settings.UseShellExecute,
				RedirectStandardOutput = !settings.UseShellExecute,
				WindowStyle = ProcessWindowStyle.Minimized,
				LoadUserProfile = false				
			};
			erlProcess = Process.Start(processStartInfo);

			if (!settings.UseShellExecute)
			{
				erlProcess.OutputDataReceived += (_, e) => CouchDBLog.Info(e.Data);
				erlProcess.ErrorDataReceived += (_, e) => CouchDBLog.Error(e.Data);
				erlProcess.Exited +=
					(_, e) => Log.ErrorFormat("erl.exe has exited with exit code {1}", erlProcess.ProcessName, erlProcess.ExitCode);
				erlProcess.BeginErrorReadLine();
				erlProcess.BeginOutputReadLine();
			}

			WaitTillResponding();
		}

		/// <summary>Throws <see cref="InvalidOperationException"/> if CouchDB launch script process exites.</summary>
		public void ThrowIfExitedUnexpectedly()
		{
			if (erlProcess.HasExited)
				throw new InvalidOperationException("erl.exe has exited unexpectedly with code " + erlProcess.ExitCode);
		}

		/// <summary>Terminates CouchDB process if it's up and running.</summary>
		public void TerminateIfRunning()
		{
			try
			{
				if (!erlProcess.HasExited)
				{
					Log.Info("Killing erl.exe...");
					erlProcess.Kill();
				}
				else
					Log.Info("Could not kill erl.exe: it's not running anymore");
			} 
			catch(Exception e)
			{
				Log.Error("Unable to kill erl.exe", e);	
			}
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
