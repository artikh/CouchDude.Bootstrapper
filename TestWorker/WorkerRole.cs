using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Common.Logging;
using Common.Logging.Simple;
using CouchDude.Bootstrapper.Azure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace BootstrapperTestWorker
{
	public class WorkerRole : RoleEntryPoint
	{
		public override bool OnStart()
		{
			// Set the maximum number of concurrent connections 
			ServicePointManager.DefaultConnectionLimit = 12;

			CouchDBAzureBootstrapper.StartAndWaitForResult();
			return true;
		}

		public override void Run()
		{
			// This is a sample worker implementation. Replace with your logic.
			Trace.WriteLine("BootstrapperTestWorker entry point called", "Information");

			while (true)
			{
				Thread.Sleep(10000);
				Trace.WriteLine("Working", "Information");
			}
		}
	}
}
