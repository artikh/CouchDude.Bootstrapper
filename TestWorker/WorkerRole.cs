using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using CouchDude.Bootstrapper.Azure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace BootstrapperTestWorker
{
	public class WorkerRole : RoleEntryPoint
	{
		private Action throwIfCouchDBExited = () => {};

		public override bool OnStart()
		{
			// Set the maximum number of concurrent connections 
			ServicePointManager.DefaultConnectionLimit = 12;
			var diagnosticsConfiguration = DiagnosticMonitor.GetDefaultInitialConfiguration();
			diagnosticsConfiguration.WindowsEventLog.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
			diagnosticsConfiguration.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
			diagnosticsConfiguration.DiagnosticInfrastructureLogs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
			CouchDBAzureBootstrapper.ConfigureLogTransfer(diagnosticsConfiguration.Directories);
			diagnosticsConfiguration.Directories.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
			DiagnosticMonitor.Start(
				"Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString",
				diagnosticsConfiguration);

			//try {
				throwIfCouchDBExited = CouchDBAzureBootstrapper.StartAndWaitForResult();
			//} catch { }

			return true;
		}

		public override void Run()
		{
			// This is a sample worker implementation. Replace with your logic.
			Trace.WriteLine("BootstrapperTestWorker entry point called", "Information");

			while (true)
			{
				throwIfCouchDBExited();
				Thread.Sleep(TimeSpan.FromMinutes(5));
				Trace.WriteLine("Still working", "Information");
			}
		}
	}
}
