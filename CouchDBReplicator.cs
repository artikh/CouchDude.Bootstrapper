using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common.Logging;


namespace CouchDude.Bootstrapper
{
	/// <summary>Manages CouchDB interinstance replication.</summary>
	public class CouchDBReplicator
	{
		static readonly ILog Log = LogManager.GetLogger(typeof(CouchDBReplicator));

		/// <summary>Updates interinstance replication state.</summary>
		public static void UpdateReplicationState(IPEndPoint localEndPoint, ReplicationSettings settings)
		{
			var localCouchDBUri = localEndPoint.ToHttpUri();
			var couchApi = Factory.CreateCouchApi(localCouchDBUri);
			
			CreateDatabases(couchApi, settings.DatabasesToReplicate);

			var existingReplicationDescriptorIds =
				new HashSet<string>(couchApi.Replicator.Synchronously.GetAllDescriptorNames());

			var descriptorsToCreate = (
				from endpoint in settings.EndPointsToReplicateTo
				from dbName in settings.DatabasesToReplicate
				let endpointUri = endpoint.ToHttpUri()
				let descriptorId = GetDescriptorId(endpointUri, dbName)
				where !existingReplicationDescriptorIds.Contains(descriptorId)
				let descriptor = new ReplicationTaskDescriptor {
					Id = descriptorId,
					Continuous = true,
					Source = new Uri(endpointUri, dbName),
					Target = new Uri(dbName, UriKind.Relative),
					UserContext = new ReplicationUserContext {
						Roles = new[] { "admin" }
					}
				}
				select descriptor
			).ToArray();
			
			foreach (var descriptorToCreate in descriptorsToCreate)
			{
				Log.InfoFormat("Saving replication descriptor ID: {0}", descriptorToCreate.Id);
				couchApi.Replicator.Synchronously.SaveDescriptor(descriptorToCreate);
			}
		}

		private static void CreateDatabases(ICouchApi couchApi, IEnumerable<string> databasesToReplicate)
		{
			foreach (var dbToReplicateName in databasesToReplicate)
			{
				var dbApi = couchApi.Db(dbToReplicateName);
				if(!dbApi.Synchronously.RequestInfo().Exists)
					dbApi.Synchronously.Create();
			}
		}

		static string GetDescriptorId(Uri remoteCouchDBBaseAddress, string databaseName)
		{
			return string.Format("from_{0}_to_{1}", remoteCouchDBBaseAddress.ToString().Replace("/", "_"), databaseName);
		}
	}
}
