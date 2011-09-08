using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace CouchDude.Bootstrapper
{
	/// <summary>CouchDB bootstrap settings.</summary>
	public class BootstrapSettings
	{
		private DirectoryInfo workingDirectory;
		private DirectoryInfo couchDBDirectory;
		private DirectoryInfo couchDBluceneDirectory;
		private DirectoryInfo dataDirectory;
		private DirectoryInfo logDirectory;
		private bool setupCouchDBLucene;
		private FileInfo couchDBDistributive;
		private FileInfo couchDBLuceneDistributive;
		private IPEndPoint endpointToListenOn;

		private bool locked;

		/// <summary>Prevents any further changes to object.</summary>
		public void Lock()
		{
			CheckIfExists(WorkingDirectory, "Working directory");
			CheckIfExists(DataDirectory, "Data directory");
			CheckIfExists(LogDirectory, "Log directory");
			CheckIfExists(CouchDBDistributive, "CouchDB distributive file");
			if (SetupCouchDBLucene)
				CheckIfExists(CouchDBLuceneDistributive, "couchdb-lucene distributive file");
			if (EndpointToListenOn == null)
				throw new ArgumentException("IP endpoint should be provided.");

			locked = true;
		}

		private static void CheckIfExists(FileSystemInfo fsItem, string name)
		{
			if (!fsItem.Exists)
				throw new ArgumentException(name + " does not exist: " + fsItem.FullName);
		}

		/// <summary>Main working directory where CouchDB executables should be located.</summary>
		public DirectoryInfo WorkingDirectory
		{
			get { return workingDirectory; }
			set
			{
				ThrowIfLocked();
				workingDirectory = value;
			}
		}

		/// <summary>Target directory for CouchDB binaries and configuration</summary>
		public DirectoryInfo CouchDBDirectory
		{
			get { return couchDBDirectory ?? (couchDBDirectory = new DirectoryInfo(Path.Combine(WorkingDirectory.FullName, "couchdb"))); }
		}

		/// <summary>Target directory for couchdb-lucene binaries and configuration</summary>
		public DirectoryInfo CouchDBLuceneDirectory
		{
			get { return couchDBluceneDirectory ?? (couchDBluceneDirectory = new DirectoryInfo(Path.Combine(WorkingDirectory.FullName, "couchdb-lucene"))); }
		}
		
		/// <summary>Directory where main data should be stored (not including view and lucene indexes).</summary>
		public DirectoryInfo DataDirectory
		{
			get { return dataDirectory; }
			set
			{
				ThrowIfLocked();
				dataDirectory = value;
			}
		}
		
		/// <summary>Log target directory.</summary>
		public DirectoryInfo LogDirectory
		{
			get { return logDirectory; }
			set
			{
				ThrowIfLocked();
				logDirectory = value;
			}
		}

		/// <summary>Indicates weather  couchdb-lucene instance should be bootstrapped.</summary>
		public bool SetupCouchDBLucene
		{
			get { return setupCouchDBLucene; }
			set
			{
				ThrowIfLocked();
				setupCouchDBLucene = value;
			}
		}
		
		/// <summary>Zip file containing CouchDB distributive.</summary>
		public FileInfo CouchDBDistributive
		{
			get { return couchDBDistributive; }
			set
			{
				ThrowIfLocked();
				couchDBDistributive = value;
			}
		}
		
		/// <summary>Zip file containing couchdb-lucene distributive.</summary>
		public FileInfo CouchDBLuceneDistributive
		{
			get { return couchDBLuceneDistributive; }
			set
			{
				ThrowIfLocked();
				couchDBLuceneDistributive = value;
			}
		}

		/// <summary>CouchDB endpoint.</summary>
		public IPEndPoint EndpointToListenOn
		{
			get { return endpointToListenOn; }
			set
			{
				ThrowIfLocked();
				endpointToListenOn = value; 
			}
		}

		/// <summary>Administrator user name.</summary>
		public string AdminUserName { get; set; }

		/// <summary>Administrator password.</summary>
		public string AdminPlainTextPassword { get; set; }

		/// <summary>Databases to replicate.</summary>
		public string[] DatabasesToReplicate { get; set; }

		private void ThrowIfLocked()
		{
			if(locked)
				throw new InvalidOperationException("Settings should not be modified after bootstrap pocess started.");
		}
	}
}