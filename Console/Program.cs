using System;
using CLAP;
using Common.Logging;
using Common.Logging.Simple;

namespace CouchDude.Bootstrapper.Console
{
	class Program
	{
		static void Main(string[] args)
		{
			LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter();
			Parser<BootstrapperConsole>.Run(args);
		}
	}
}
