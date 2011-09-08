using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Common.Logging;


namespace CouchDude.Bootstrapper
{
	/// <summary>Manages CouchDB interinstance replication.</summary>
	public class CouchDBReplicator
	{
		private readonly BootstrapSettings settings;
		static readonly ILog Log = LogManager.GetLogger(typeof(CouchDBReplicator));

		readonly HashSet<Uri> knownCouchDBInstanceAddresses = new HashSet<Uri>();
		readonly ICouchApi couchApi;

		/// <constructor />
		public CouchDBReplicator(BootstrapSettings settings)
		{
			this.settings = settings;
			couchApi = Factory.CreateCouchApi(settings.EndpointToListenOn.ToHttpUri());
																											 
			UpdateReplicationState();
		}

		/// <summary>Updates interinstance replication state.</summary>
		public void UpdateReplicationState()
		{
			lock (knownCouchDBInstanceAddresses)
			{
				var currentCouchDBInstanceAddresses = GetCurrentCouchDBInstanceAddresses();
				var extinctedCouchDBInstanceAddresses =
					knownCouchDBInstanceAddresses.Except(currentCouchDBInstanceAddresses).ToList();

				foreach (var extinctedCouchDBInstanceAddress in extinctedCouchDBInstanceAddresses)
				{
					foreach (var databaseToReplicate in settings.DatabasesToReplicate)
						RemoveReplicationDescriptor(extinctedCouchDBInstanceAddress, databaseToReplicate);
					knownCouchDBInstanceAddresses.Remove(extinctedCouchDBInstanceAddress);
				}
				
				knownCouchDBInstanceAddresses.UnionWith(currentCouchDBInstanceAddresses);
				foreach (var address in knownCouchDBInstanceAddresses)
				{
					foreach (var databaseToReplicate in settings.DatabasesToReplicate)
						UpdateReplicationDescriptor(address, databaseToReplicate);
				}
			}
		}

		private void UpdateReplicationDescriptor(Uri remoteCouchDBBaseAddress, string databaseName)
		{
			try
			{
				var descriptorId = GetDescriptorId(remoteCouchDBBaseAddress, databaseName);
				var existingDescriptor = couchApi.Replicator.Synchronously.RequestDescriptorById(descriptorId);
				if (existingDescriptor == null)
				{
					var dbApi = couchApi.Db(databaseName);
					if(!dbApi.Synchronously.RequestInfo().Exists)
						dbApi.Synchronously.Create();

					
					var replicationDescriptor = new ReplicationTaskDescriptor {
						Id = descriptorId,
						Target = new Uri(databaseName, UriKind.Relative),
						Source = new Uri(remoteCouchDBBaseAddress, databaseName),
						Continuous = true,
					};

					Log.InfoFormat("Saving replication descriptor {0}", descriptorId);
					couchApi.Replicator.Synchronously.SaveDescriptor(replicationDescriptor);
				}
			}
			catch(Exception e)
			{
				Log.Error("Error replicating to " + remoteCouchDBBaseAddress, e);
			}
		}

		private void RemoveReplicationDescriptor(Uri remoteCouchDBBaseAddress, string databaseName)
		{
			try
			{
				var descriptorId = GetDescriptorId(remoteCouchDBBaseAddress, databaseName);

				var existingDescriptor = couchApi.Replicator.Synchronously.RequestDescriptorById(descriptorId);
				if (existingDescriptor != null)
				{
					Log.InfoFormat("Removing replication descriptor with ID{0} and revision {1})", descriptorId, existingDescriptor.Revision);
					couchApi.Replicator.Synchronously.Delete(existingDescriptor);
				}
			}
			catch(Exception e)
			{
				Log.Error("Error stopping replication to " + remoteCouchDBBaseAddress, e);
			}
		}

		static string GetDescriptorId(Uri remoteCouchDBBaseAddress, string databaseName)
		{
			return string.Format("from_{0}_to_{1}", remoteCouchDBBaseAddress.ToString().Replace("/", "_"), databaseName);
		}

		static ICollection<Uri> GetCurrentCouchDBInstanceAddresses() { return Enumerable.Empty<Uri>().ToArray(); }
	}
}
