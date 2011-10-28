using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace CouchDude.Bootstrapper.Azure
{
	/// <summary>Initializes CouchDB background process conventionally in Windows Azure worker or web role environment.</summary>
	public static class CouchDBAzureBootstrapper
	{
		const string ConfigPrefix = "CouchDB.";
		const string DatabasesToReplicateConfigOption             = ConfigPrefix + "DatabasesToReplicate";
		const string CouchDBDistributiveConfigOption              = ConfigPrefix + "CouchDBDistributive";
		const string CouchDBLuceneDistributiveConfigOption        = ConfigPrefix + "CouchDBLuceneDistributive";
		const string JreDistributiveConfigOption                  = ConfigPrefix + "JreDistributive";
		const string UseCloudDriveConfigOption                    = ConfigPrefix + "UseCloudDrive";

		const string CloudDriveConfigPrefix                       = ConfigPrefix + "CloudDrive.";
		const string CloudDriveBlobContainerConfigOption          = CloudDriveConfigPrefix + "BlobContainer";
		const string CloudDriveBlobNameConfigOption               = CloudDriveConfigPrefix + "BlobName";
		const string CloudDriveBlobSizeConfigOption               = CloudDriveConfigPrefix + "BlobSize";
		const string CloudDriveConnectionStringConfigOption       = CloudDriveConfigPrefix + "ConnectionString";
		const string CloudDriveInitCacheConfigOption              = CloudDriveConfigPrefix + "InitCache";
		const string CloudDriveCacheLocalResourceNameConfigOption = CloudDriveConfigPrefix + "CacheResourceName";
		
		const string InstanceEndpointName = "CouchDB";
		const string BinariesResourceName      = "CouchDB";
		const string TempResourceName     = "CouchDB.Temp";
		const string LogResourceName			= "CouchDB.Log";
		const string DataResourceName			= "CouchDB.Data";

		private const int DefaultInitializationTimeout = 60000;
		private const int CloudDriveMountWaitTime = 60000;

		static readonly ILog Log = LogManager.GetCurrentClassLogger();

		/// <summary>Configures CouchDB and couchdb-lucene log transfer.</summary>
		public static void ConfigureLogTransfer(DirectoriesBufferConfiguration directoriesBufferConfiguration)
		{
			var logResource = RoleEnvironment.GetLocalResource(LogResourceName);
			directoriesBufferConfiguration.DataSources.Add(
				new DirectoryConfiguration { Container = "couchdb-logs", Path = logResource.RootPath }
			);
		}

		/// <summary>Starts CouchDB initialization task.</summary>
		public static Task<CouchDBWatchdog> Start()
		{
			if (!RoleEnvironment.IsAvailable)
				throw new InvalidOperationException(
					"This should be used in Windows Azure role environment only");

			// Reading and validating all configuration
			var couchDBDistributive = RoleEnvironment.GetConfigurationSettingValue(CouchDBDistributiveConfigOption);
			var couchDBLuceneDistributive = RoleEnvironment.GetConfigurationSettingValue(CouchDBLuceneDistributiveConfigOption);
			var jreDistributive = RoleEnvironment.GetConfigurationSettingValue(JreDistributiveConfigOption);

			var localIPEndPoint = GetLocalIPEndPoint();
			var endpointsToReplicateTo = GetCouchDBEndpoints(localIPEndPoint.Port).ToArray();
			var databasesToReplicate = RoleEnvironment
				.GetConfigurationSettingValue(DatabasesToReplicateConfigOption)
				.Split(new []{';'}, StringSplitOptions.RemoveEmptyEntries);
			var binariesResource = RoleEnvironment.GetLocalResource(BinariesResourceName);
			var logResource = RoleEnvironment.GetLocalResource(LogResourceName);
			var tempResource = RoleEnvironment.GetLocalResource(TempResourceName);
			var tempDir = new DirectoryInfo(tempResource.RootPath);
			var useCloudDrive =
				bool.Parse(RoleEnvironment.GetConfigurationSettingValue(UseCloudDriveConfigOption));

			CloudDriveSettings cloudDriveSettings = null;
			LocalResource dataResource = null;
			if (useCloudDrive)
				cloudDriveSettings = CloudDriveSettings.Read();
			else
				dataResource = RoleEnvironment.GetLocalResource(DataResourceName);

			return Task.Factory.StartNew(
				() => {
					// Preparing environment
					var logDir = new DirectoryInfo(logResource.RootPath);
					var binDir = new DirectoryInfo(binariesResource.RootPath);

					var getCouchDBDistributiveTask = GetFileTask.Start(tempDir, couchDBDistributive);
					var getCouchDBLuceneDistributiveTask = GetFileTask.Start(tempDir, couchDBLuceneDistributive);
					var getJreDistributiveTask = GetFileTask.Start(tempDir, jreDistributive);
					var getDataDirTask = useCloudDrive
						? Task.Factory.StartNew(() => InitCloudDrive(cloudDriveSettings))
						: Task.Factory.StartNew(() => new DirectoryInfo(dataResource.RootPath));

					// Waiting for all prepare tasks to finish
					Task.WaitAll(
						getCouchDBDistributiveTask, getCouchDBLuceneDistributiveTask, getJreDistributiveTask, getDataDirTask);

					var bootstrapSettings = new BootstrapSettings {
						CouchDBDistributive        = getCouchDBDistributiveTask.Result,
						CouchDBLuceneDistributive  = getCouchDBLuceneDistributiveTask.Result,
						JavaDistributive           = getJreDistributiveTask.Result,
						CouchDBLucenePort          = localIPEndPoint.Port + 42, // hardcoded, but it's only matter within instance
						BinDirectory               = binDir,
						DataDirectory              = getDataDirTask.Result,
						LogDirectory               = logDir,
						EndpointToListenOn         = localIPEndPoint,
						SetupCouchDBLucene         = true,
						// For some reason this is requried to run CouchDB in Azure or even devfabric
						UseShellExecute            = true
					};
					bootstrapSettings.ReplicationSettings.DatabasesToReplicate = databasesToReplicate;
					bootstrapSettings.ReplicationSettings.EndPointsToReplicateTo = endpointsToReplicateTo;

					return CouchDBBootstraper.Bootstrap(bootstrapSettings);
				})
			.ContinueWith(t => {
				if(t.IsFaulted && t.Exception != null)
				{
					const string errorDescription = "Error occured initializing CouchDB";
					WriteBackupFile(errorDescription, t.Exception);
					Log.Error(errorDescription, t.Exception);
				}
				else
					Log.Info("CouchDB started");

				return t.Result;
			});
		}

		private static void WriteBackupFile(string errorDescription, Exception exception)
		{
			try
			{
				var buffer = new byte[4];
				System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(buffer);
				var randomInt = BitConverter.ToInt32(buffer, 0);
				var fileName = 
					Path.Combine("C:\\", string.Concat("couchdude.botstrapper.startup.error.", DateTime.Now.Ticks, randomInt, ".log"));
				File.WriteAllText(
					path: fileName,
					contents: string.Concat(DateTime.UtcNow.ToString("u"), "\n", errorDescription, "\n", exception.ToString())
				);
			}
			// ReSharper disable EmptyGeneralCatchClause
			catch { }
			// ReSharper restore EmptyGeneralCatchClause
		}

		/// <summary>Starts CouchDB initialization task and waits for result.</summary>
		public static CouchDBWatchdog StartAndWaitForResult(int timeout = DefaultInitializationTimeout)
		{
			Task<CouchDBWatchdog> task = Start();
			task.Wait(timeout);
			return task.Result;
		}

		private static DirectoryInfo InitCloudDrive(CloudDriveSettings settings)
		{
			Log.Info("Configuring CloudDrive...");
			
			if (settings.InitCache)
				DoInitCloudDriveCache(settings.CacheLocalResource);
			

			var cloudDriveStorageAccount = CloudStorageAccount.Parse(settings.ConnectionString);
			var blobClient = cloudDriveStorageAccount.CreateCloudBlobClient();
			blobClient.GetContainerReference(settings.BlobContainerName).CreateIfNotExist();

			var driveBlobName =
				string.Format("{0}.{1}", settings.BlobName, RoleEnvironment.CurrentRoleInstance.Id);

			var pageBlob = blobClient.GetContainerReference(settings.BlobContainerName).GetPageBlobReference(driveBlobName);

			var cloudDrive = cloudDriveStorageAccount.CreateCloudDrive(pageBlob.Uri.ToString());

			try
			{
				if (!pageBlob.Exists())
				{
					Log.InfoFormat("Creating page blob {0}", cloudDrive.Uri);
					cloudDrive.Create(settings.BlobSize);
				}

				Log.InfoFormat("Mounting {0}", cloudDrive.Uri);
				cloudDrive.Mount(cacheSize: 25, options: DriveMountOptions.Force);
				Log.InfoFormat("CloudDrive {0} mounted at {1}", cloudDrive.Uri, cloudDrive.LocalPath);

				WaitTillCouldWrite(cloudDrive.LocalPath);

				return new DirectoryInfo(cloudDrive.LocalPath);
			}
			catch (CloudDriveException e)
			{
				Log.Error(e);
				throw;
			}
		}

		private static void WaitTillCouldWrite(string localPath)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < CloudDriveMountWaitTime)
				try
				{
					string tempFile = Path.Combine(localPath, Guid.NewGuid() + ".tmp");
					File.WriteAllText(tempFile, "test");
					File.Delete(tempFile);
					// if write completes without exception drive must be operational
					return;
				}
				catch(IOException) { }

			throw new TimeoutException("Time out waiting for cloud drive to be mounted.");
		}

		private static void DoInitCloudDriveCache(LocalResource cloudDriveCacheLocalResource)
		{
			const int tries = 30;

			// Temporary workaround for ERROR_UNSUPPORTED_OS seen with Windows Azure Drives
			// See http://blogs.msdn.com/b/windowsazurestorage/archive/2010/12/17/error-unsupported-os-seen-with-windows-azure-drives.aspx
			for (int i = 0; i < tries; i++)
				try
				{
					CloudDrive.InitializeCache(cloudDriveCacheLocalResource.RootPath, cloudDriveCacheLocalResource.MaximumSizeInMegabytes);
					break;
				}
				catch (CloudDriveException ex)
				{
					if (!ex.Message.Equals("ERROR_UNSUPPORTED_OS"))
						throw;

					if (i >= (tries - 1))
					{
						// If the workaround fails then it would be dangerous to continue silently, so exit 
						Log.Error(
							"Workaround for ERROR_UNSUPPORTED_OS see http://bit.ly/fw7qzo FAILED");
						Environment.Exit(-1);
					}

					Log.Info("Using temporary workaround for ERROR_UNSUPPORTED_OS see http://bit.ly/fw7qzo");
					Thread.Sleep(10000);
				}
		}

		private static IEnumerable<IPEndPoint> GetCouchDBEndpoints(int port)
		{
			var currentRoleInstance = RoleEnvironment.CurrentRoleInstance;
			var currentRoleInstanceId = currentRoleInstance.Id;

			return from roleInstance in currentRoleInstance.Role.Instances
			       where roleInstance.Id != currentRoleInstanceId
			       // getting ip address using first internal instance end point
			       let ipAddress = roleInstance.InstanceEndpoints.Values.Select(ep => ep.IPEndpoint.Address).First()
			       select new IPEndPoint(ipAddress, port);
		}

		private static IPEndPoint GetLocalIPEndPoint()
		{
			RoleInstance instance = RoleEnvironment.CurrentRoleInstance;
			RoleInstanceEndpoint instanceEndpoint;
			if (!instance.InstanceEndpoints.TryGetValue(InstanceEndpointName, out instanceEndpoint))
				throw new Exception("CouchDB instance endpoint should be declared on current role instances.");
			if (instanceEndpoint.Protocol != "tcp")
				throw new Exception(
					string.Format("CouchDB instance endpoint should be tcp, not {0}.", instanceEndpoint.Protocol));
			return instanceEndpoint.IPEndpoint;
		}

		private class CloudDriveSettings
		{
			public readonly string BlobContainerName;
			public readonly string BlobName;
			public readonly int    BlobSize;
			public readonly string ConnectionString;
			public readonly bool   InitCache;
			public readonly LocalResource CacheLocalResource;

			public static CloudDriveSettings Read() { return new CloudDriveSettings(); }

			private CloudDriveSettings()
			{
				BlobContainerName  = RoleEnvironment.GetConfigurationSettingValue(CloudDriveBlobContainerConfigOption);
				BlobName           = RoleEnvironment.GetConfigurationSettingValue(CloudDriveBlobNameConfigOption);
				BlobSize           = ushort.Parse(RoleEnvironment.GetConfigurationSettingValue(CloudDriveBlobSizeConfigOption));
				ConnectionString   = RoleEnvironment.GetConfigurationSettingValue(CloudDriveConnectionStringConfigOption);
				InitCache          = bool.Parse(RoleEnvironment.GetConfigurationSettingValue(CloudDriveInitCacheConfigOption));

				CacheLocalResource = null;
				if (InitCache)
				{
					var cacheResourceName = RoleEnvironment.GetConfigurationSettingValue(CloudDriveCacheLocalResourceNameConfigOption);
					CacheLocalResource = RoleEnvironment.GetLocalResource(cacheResourceName);
				}
			}
		}
	}
}
