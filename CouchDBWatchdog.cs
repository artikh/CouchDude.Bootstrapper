using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Logging;
using CouchDude.Utils;


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
		CouchDBReplicator dbReplicator;
		private readonly Uri couchAddress;

		/// <constructor />
		public CouchDBWatchdog(BootstrapSettings settings)
		{
			this.settings = settings;
			if (!settings.WorkingDirectory.Exists)
				throw new Exception("CouchDB folder does not exist");

			startCouchDbBatchFile = settings.WorkingDirectory.EnumerateFiles("couchdb.bat", SearchOption.AllDirectories).First();
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
			erlProcess.OutputDataReceived += LogInfo;
			erlProcess.ErrorDataReceived += LogError;

			WaitTillResponding();
			
			dbReplicator = new CouchDBReplicator(settings);
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

		/// <summary>Updates interinstance replication state.</summary>
		public void UpdateReplicationState()
		{
			if(dbReplicator != null)
				dbReplicator.UpdateReplicationState();
		}

		/// <summary>Watches for CouchDB process to be up forever restarting it if needed.</summary>
		public void WatchForever()
		{
			while (true)
			{
				if(!ProcessIsUp)
				{
					Log.Error("CouchDB process exited unexpectedly. Restarting...");
					Start();
					Log.Warn("CouchDB have restarted after crush");
				}

				if (!CouchDBIsResponding)
				{
					Log.Error("CouchDB process is unresponsive. Restarting...");
					TerminateIfUp();
					Start();
					Log.Warn("CouchDB have restarted after being terminated");
				}

				Thread.Sleep(5000);
			}
		}


		private bool CouchDBIsResponding { get { return couchApi.Synchronously.CheckIfUp(); } }

		/// <summary>Тормозит текущий поток до тех пор пока CouchDB не отвечает по HTTP.</summary>
		private void WaitTillResponding()
		{
			var pingAddress = settings.EndpointToListenOn.ToHttpUri();
			Log.InfoFormat("Wating for CouchDB to respond on {0}", pingAddress);

			while (!CouchDBIsResponding)
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
