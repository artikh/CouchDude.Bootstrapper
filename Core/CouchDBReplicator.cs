using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

			var descriptorsToCreate = (
				from endpoint in settings.EndPointsToReplicateTo
				from dbName in settings.DatabasesToReplicate
				let endpointUri = endpoint.ToHttpUri()
				let descriptorId = GetDescriptorId(endpointUri, dbName)
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

			Log.InfoFormat(
				"Replication descriptors should be created: {0}", 
				string.Join(", ", descriptorsToCreate.Select(d => d.Id))
			);

			var replicatorApi = couchApi.Replicator;
			replicatorApi.GetAllDescriptorNames()
				.ContinueWith(t => {
					Log.Info("Loading existing replication descriptors...");
					var loadTasks = (
						from descriptorId in t.Result
						select replicatorApi.RequestDescriptorById(descriptorId)
							.ContinueWith(pt => pt.IsCompleted? pt.Result: null)
					).ToArray();
					return Task.Factory.ContinueWhenAll(
						loadTasks,
						tasks => {
							var existingDescriptors = (
								from loadTask in tasks
								select loadTask.Result
								into descriptor
								where descriptor != null
								select descriptor
							).ToArray();

							Log.InfoFormat(
								"{0} replication descriptors found in the _replicator database: {1}",
								existingDescriptors.Length,
								string.Join(", ", existingDescriptors.Select(d => d.Id))
							);
							return existingDescriptors;
						});
				})
				.Unwrap()
				.ContinueWith(t => {
				  var descritorsToDelete = (
				    from descriptorToCreate in descriptorsToCreate
				    from existingDescriptor in t.Result
				    where existingDescriptor.Id == descriptorToCreate.Id
						select existingDescriptor
					).ToArray();
					Log.InfoFormat(
						"Deleting {0} replication descriptors from _replicator database: {1}",
						descritorsToDelete.Length,
						string.Join(", ", descritorsToDelete.Select(d => d.Id))
					);
				  var deleteTasks =
						from descritorToDelete in descritorsToDelete
						select replicatorApi.DeleteDescriptor(descritorToDelete);
				  return Task.Factory.ContinueWhenAll(deleteTasks.ToArray(), ThrowAggregateIfFaulted);
				})
				.Unwrap()
				.ContinueWith(t => {
					Log.InfoFormat(
						"Creating {0} replication descriptors in _replicator database: {1}",
						descriptorsToCreate.Length,
						string.Join(", ", descriptorsToCreate.Select(d => d.Id))
					);
					var createDescriptorsTasks =
						from descriptorToCreate in descriptorsToCreate
						select replicatorApi.SaveDescriptor(descriptorToCreate);
					return Task.Factory.ContinueWhenAll(createDescriptorsTasks.ToArray(), ThrowAggregateIfFaulted);
				})
				.Unwrap()
				.Wait();

			Log.Info("Replication descriptors have been created");
		}

		private static void ThrowAggregateIfFaulted(Task<DocumentInfo>[] tasks)
		{
			var exceptions = tasks.Where<Task>(dt => dt.IsFaulted).Select(dt => dt.Exception).ToArray();
			if (exceptions.Length > 0)
				throw new AggregateException(exceptions);
		}

		private static void CreateDatabases(ICouchApi couchApi, IEnumerable<string> databasesToReplicate)
		{
			foreach (var dbApi in databasesToReplicate.Select(couchApi.Db))
				try
				{
					dbApi.Synchronously.Create();
				}
				catch (CouchCommunicationException e)
				{
					// i.e. database file is already exists meannig it have been concurrently created, witch in case is possible
					// if DB data is stored in CloudDrive or if we running on dev fabric
					if (!e.Message.Contains("file_exists"))
						throw;
				}
		}

		static string GetDescriptorId(Uri remoteCouchDBBaseAddress, string databaseName)
		{
			return string.Format("from_{0}_to_{1}", remoteCouchDBBaseAddress.ToString().Replace("/", "_"), databaseName);
		}
	}
}
