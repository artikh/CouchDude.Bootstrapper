using System.Diagnostics;
using System.Net;
using System.Threading;
using CouchDude.Bootstrapper.Azure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace BootstrapperTestWorker
{
	public class WorkerRole : RoleEntryPoint
	{
		private const int InitializationTimeout = 60000;

		public override bool OnStart()
		{
			// Set the maximum number of concurrent connections 
			ServicePointManager.DefaultConnectionLimit = 12;

			new CouchDBAzureBootstrapper().Start().Wait(InitializationTimeout);
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
