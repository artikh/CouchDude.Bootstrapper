﻿using System;
using System.Reflection;
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
			var parser = new Parser<BootstrapperConsole>();
			parser.Register.EmptyHelpHandler(HandleEmpty);
			parser.Register.ErrorHandler(HandleError);
			parser.Run(args, new BootstrapperConsole());
		}

		private static void HandleEmpty(string obj)
		{
			var execName = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
			System.Console.WriteLine(
				string.Format(
					"Usage:\n\t{0} --ports=8081,8082,8083 --replicate-databases=db1,db2,db3 --admin-user-name=admin --admin-password=passw0rd", 
					execName));
		}

		private static void HandleError(ExceptionContext e)
		{
			System.Console.WriteLine("\nUnexpected error occured:"
#if DEBUG
			                         + "\n" + e.Exception.ToString()
#else
							+ e.Exception.GetType().Name
							+ ": "
							+ e.Exception.Message
#endif
);
		}
	}
}