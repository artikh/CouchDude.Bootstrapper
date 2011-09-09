using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Logging;


namespace CouchDude.Bootstrapper
{
	/// <summary>Обёртка над процессом CouchDB</summary>
	public class CouchDBWatchdog
	{
		private readonly BootstrapSettings settings;
		private static readonly ILog Log = LogManager.GetLogger(typeof(CouchDBWatchdog)); 

		private readonly DirectoryInfo couchDbBinFolder;
		private readonly FileInfo startCouchDbBatchFile;
		private readonly ICouchApi couchApi;

		private Process batchFileProcess;
		private Process erlProcess;
		private readonly Uri couchAddress;

		/// <constructor />
		public CouchDBWatchdog(BootstrapSettings settings)
		{
			this.settings = settings;
			if (!settings.BinDirectory.Exists)
				throw new Exception("CouchDB folder does not exist");

			startCouchDbBatchFile = settings.BinDirectory.EnumerateFiles("couchdb.bat", SearchOption.AllDirectories).First();
			couchDbBinFolder = startCouchDbBatchFile.Directory;
			couchAddress = settings.EndpointToListenOn.ToHttpUri();
			couchApi = Factory.CreateCouchApi(couchAddress);
		}

		/// <summary>Запускает CouchDB.</summary>
		public void Start()
		{
			Log.Info("Starting CouchDB process...");

			Environment.SetEnvironmentVariable("ERL", "erl.exe");

			batchFileProcess = Process.Start(new ProcessStartInfo(startCouchDbBatchFile.FullName, couchDbBinFolder.FullName) {
				CreateNoWindow = true,
				UseShellExecute = false
			});

			erlProcess = WaitForErlToBeUp();
			erlProcess.StartInfo.RedirectStandardError = true;
			erlProcess.StartInfo.RedirectStandardInput = true;
			erlProcess.OutputDataReceived += LogInfo;
			erlProcess.ErrorDataReceived += LogError;

			WaitTillResponding();
		}

		/// <summary>Проверяет поднята ли CouchDB.</summary>
		public bool ProcessIsUp { get { return erlProcess != null && !erlProcess.HasExited; } }

		/// <summary>Останавливает CouchDB.</summary>
		public void TerminateIfUp()
		{
			if (ProcessIsUp)
			{
				Log.Info("Terminating CouchDB process");
				erlProcess.Kill();
				batchFileProcess.Kill();
			}
		}

		/// <summary>Restarts CouchDB instance if it have crushed.</summary>
		public void RestartIfDown()
		{
			if (!ProcessIsUp)
			{
				Trace.TraceError("CouchDB process terminate unexpectedly.");
				Start();
			}
		}

		private bool CheckIfCouchDBIsResponding()
		{
			var webClient = new WebClient();
			try
			{
				var response = webClient.DownloadString(settings.EndpointToListenOn.ToHttpUri());
				Log.InfoFormat("CouchDB have responded: \"{0}\"", response);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>Тормозит текущий поток до тех пор пока CouchDB не отвечает по HTTP.</summary>
		private void WaitTillResponding()
		{
			var pingAddress = settings.EndpointToListenOn.ToHttpUri();
			Log.InfoFormat("Wating for CouchDB to respond on {0}", pingAddress);

			while (!CheckIfCouchDBIsResponding())
				Thread.Sleep(TimeSpan.FromSeconds(50));

			Log.InfoFormat("CouchDB is up");
		}

		private static void LogInfo(object _, DataReceivedEventArgs args)
		{
			Log.Info(args.Data);
		}

		private static void LogError(object _, DataReceivedEventArgs args)
		{
			Log.Error(args.Data);
		}

		private static Process WaitForErlToBeUp()
		{
			Log.Info("Waiting for erl.exe to be up");
			while (true)
			{
				var erl = ErlProcesses.FirstOrDefault();
				if (erl != null)
				{
					Log.Info("erl.exe is up");
					return erl;
				}
				else
					Thread.Sleep(200);
			}
		}

		private static IEnumerable<Process> ErlProcesses { get { return FilterRunningProcessesByName("erl"); } }

		private static IEnumerable<Process> FilterRunningProcessesByName(string processName)
		{
			return Process.GetProcesses().Where(p => p.ProcessName == processName);
		}
	}
}
