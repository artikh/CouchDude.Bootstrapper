using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace CouchDude.Bootstrapper
{
	/// <summary>CouchDB bootstrap settings.</summary>
	public class BootstrapSettings
	{
		private DirectoryInfo binDirectory;
		private DirectoryInfo couchDBDirectory;
		private DirectoryInfo couchDBluceneDirectory;
		private DirectoryInfo javaDirectory;
		private DirectoryInfo dataDirectory;
		private DirectoryInfo logDirectory;
		private DirectoryInfo tempDirectory = new DirectoryInfo(Path.GetTempPath());
		private bool setupCouchDBLucene;
		private FileInfo couchDBDistributive;
		private FileInfo couchDBLuceneDistributive;
		private FileInfo javaDistributive;
		private IPEndPoint endpointToListenOn;
		private bool useShellExecute;
		private readonly ReplicationSettings replicationSettings = new ReplicationSettings();

		private bool locked;

		/// <summary>Prevents any further changes to object.</summary>
		public void Lock()
		{
			CheckIfExists(BinDirectory, "Working directory");
			CheckIfExists(DataDirectory, "Data directory");
			CheckIfExists(LogDirectory, "Log directory");
			CheckIfExists(CouchDBDistributive, "CouchDB distributive file");
			if (SetupCouchDBLucene)
			{
				CheckIfExists(CouchDBLuceneDistributive, "couchdb-lucene distributive file");
				CheckIfExists(javaDistributive, "JRE 1.6 distributive file");
				if(CouchDBLucenePort.HasValue == false)
					throw new ArgumentException("couchdb-lucene port should be configured");
			}
			if (EndpointToListenOn == null)
				throw new ArgumentException("IP endpoint should be provided.");

			replicationSettings.Lock();
			locked = true;
		}

		private static void CheckIfExists(FileSystemInfo fsItem, string name)
		{
			if (!fsItem.Exists)
				throw new ArgumentException(name + " does not exist: " + fsItem.FullName);
		}

		/// <summary>Main working directory where CouchDB executables should be located.</summary>
		public DirectoryInfo BinDirectory
		{
			get { return binDirectory; }
			set
			{
				ThrowIfLocked();
				binDirectory = value;
			}
		}

		/// <summary>Target directory for CouchDB binaries and configuration</summary>
		public DirectoryInfo CouchDBDirectory
		{
			get { return couchDBDirectory ?? (couchDBDirectory = new DirectoryInfo(Path.Combine(BinDirectory.FullName, "couchdb"))); }
		}

		/// <summary>Target directory for couchdb-lucene binaries and configuration</summary>
		public DirectoryInfo CouchDBLuceneDirectory
		{
			get { return couchDBluceneDirectory ?? (couchDBluceneDirectory = new DirectoryInfo(Path.Combine(BinDirectory.FullName, "couchdb-lucene"))); }
		}

		/// <summary>Target directory for couchdb-lucene binaries and configuration</summary>
		public DirectoryInfo JavaDirectory
		{
			get { return javaDirectory ?? (javaDirectory = new DirectoryInfo(Path.Combine(BinDirectory.FullName, "java"))); }
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

		/// <summary>Indicates wether to use <see cref="ProcessStartInfo.UseShellExecute"/> starting
		/// CouchDB process.</summary>
		public bool UseShellExecute
		{
			get { return useShellExecute; }
			set
			{
				ThrowIfLocked();
				useShellExecute = value;
			}
		}
		
		/// <summary>Temporary data directory.</summary>
		public DirectoryInfo TempDirectory
		{
			get { return tempDirectory; }
			set
			{
				ThrowIfLocked();
				tempDirectory = value;
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

		/// <summary>Port for couchdb-lucene to listen on.</summary>
		public int? CouchDBLucenePort { get; set; }
		
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
		
		/// <summary>Zip file containing JRE 1.6 distributive.</summary>
		public FileInfo JavaDistributive
		{
			get { return javaDistributive; }
			set
			{
				ThrowIfLocked();
				javaDistributive = value;
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

		/// <summary>Replication settings object.</summary>
		public ReplicationSettings ReplicationSettings { get { return replicationSettings; } }

		private void ThrowIfLocked()
		{
			if(locked)
				throw new InvalidOperationException("Settings should not be modified after bootstrap pocess started.");
		}
	}
}