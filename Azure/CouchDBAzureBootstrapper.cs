using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Simple;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace CouchDude.Bootstrapper.Azure
{
	/// <summary>Initializes CouchDB background process conventionally in Windows Azure worker or web role environment.</summary>
	public class CouchDBAzureBootstrapper
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
		const string LocalResourceName    = "CouchDB";
		const string DataDirName          = "data";
		const string LogDirName           = "logs";
		const string ExecutableDirName    = "bin";

		static readonly ILog Log = LogManager.GetCurrentClassLogger();

		static CouchDBAzureBootstrapper()
		{
			LogManager.Adapter = new TraceLoggerFactoryAdapter(new NameValueCollection {
				{ "level", "INFO" },
				{ "showLogName", "true" },
				{ "showDataTime", "true" },
				{ "dateTimeFormat", "yyyy-MM-dd HH:mm:ss:fff" },
			});
		}

		/// <summary>Starts CouchDB initialization task.</summary>
		public Task Start()
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
			var localResource = RoleEnvironment.GetLocalResource(LocalResourceName);
			var useCloudDrive =
				bool.Parse(RoleEnvironment.GetConfigurationSettingValue(UseCloudDriveConfigOption));

			var cloudDriveSettings = useCloudDrive ? CloudDriveSettings.Read() : null;

			return Task.Factory.StartNew(
				() => {
					// Preparing environment
					var logDir = GetSubDirectory(localResource, LogDirName);
					var binDir = GetSubDirectory(localResource, ExecutableDirName);

					var getCouchDBDistributiveTask = GetDistributive(couchDBDistributive);
					var getCouchDBLuceneDistributiveTask = GetDistributive(couchDBLuceneDistributive);
					var getJreDistributiveTask = GetDistributive(jreDistributive);
					var getDataDirTask = useCloudDrive
						? Task.Factory.StartNew(() => InitCloudDrive(cloudDriveSettings))
						: Task.Factory.StartNew(() => GetSubDirectory(localResource, DataDirName));

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
					};
					bootstrapSettings.ReplicationSettings.DatabasesToReplicate = databasesToReplicate;
					bootstrapSettings.ReplicationSettings.EndPointsToReplicateTo = endpointsToReplicateTo;

					CouchDBBootstraper.Bootstrap(bootstrapSettings);
				});
		}

		private static Task<FileInfo> GetDistributive(string distributiveNameOrUrl)
		{
			Uri distributiveUri;
			if (Uri.TryCreate(distributiveNameOrUrl, UriKind.Absolute, out distributiveUri))
				return DownloadFile(distributiveUri);
			else
				return GetLocalFile(distributiveNameOrUrl);
		}

		private static Task<FileInfo> DownloadFile(Uri distributiveUri)
		{
			var httpClient = new HttpClient();
			return httpClient
				.GetAsync(distributiveUri)
				.ContinueWith(
					getDistributiveTask => {
						var response = getDistributiveTask.Result;
						response.EnsureSuccessStatusCode();
						var tempFile = new FileInfo(Path.Combine(Path.GetTempFileName(), ".zip"));
						var tempFileWriteStream = tempFile.OpenWrite();
						return response.Content
							.CopyToAsync(tempFileWriteStream)
							.ContinueWith(
								copyTask => {
									copyTask.Wait(); // ensuring exception propagated (is it nessesary?)
									tempFileWriteStream.Close();
									return tempFile;
								});
					})
				.Unwrap()
				.ContinueWith(
					downloadTask => {
						httpClient.Dispose();
						return downloadTask.Result;
					});
		}

		private static Task<FileInfo> GetLocalFile(string distributiveNameOrUrl)
		{
			return Task.Factory.StartNew(
				() => {
					var roleRootDirName =
						Environment.GetEnvironmentVariable("RoleRoot");
					Debug.Assert(roleRootDirName != null);
					if (roleRootDirName.EndsWith(Path.VolumeSeparatorChar.ToString()))
						roleRootDirName += Path.DirectorySeparatorChar;

					var binDirectory =
						new DirectoryInfo(
							Path.Combine(roleRootDirName, "approot", "bin"));
					if (!binDirectory.Exists) // i.e. it's worker role, not web role
						binDirectory =
							new DirectoryInfo(Path.Combine(roleRootDirName, "approot"));

					var distributiveFile = new FileInfo(
						Path.Combine(binDirectory.FullName, distributiveNameOrUrl));

					if (!distributiveFile.Exists)
						throw new Exception(
							string.Format(
								"Distributive file {0} have not been found. Check \"Copy to Output Directory\" property is set to \"Copy always\"",
								distributiveFile.FullName));
					return distributiveFile;
				});
		}

		private static DirectoryInfo InitCloudDrive(CloudDriveSettings settings)
		{
			Log.Info("Configuring CloudDrive...");
			
			if (settings.InitCache)
				DoInitCloudDriveCache(settings.CacheLocalResource);

			var cloudDriveStorageAccount = CloudStorageAccount.Parse(settings.ConnectionString);
			var blobClient = cloudDriveStorageAccount.CreateCloudBlobClient();
			blobClient.GetContainerReference(settings.BlobContainerName).CreateIfNotExist();

			var pageBlob = blobClient.GetContainerReference(settings.BlobContainerName).GetPageBlobReference(settings.BlobName);

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

				return new DirectoryInfo(cloudDrive.LocalPath);
			}
			catch (CloudDriveException e)
			{
				Log.Error(e);
				throw;
			}
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

		private static DirectoryInfo GetSubDirectory(LocalResource localResource, string subDirName)
		{
			return new DirectoryInfo(localResource.RootPath).CreateSubdirectory(subDirName);
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
