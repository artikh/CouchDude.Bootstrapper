using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using CouchDude.Utils;

namespace CouchDude.Bootstrapper
{
	/// <summary>DB replication settings.</summary>
	public class ReplicationSettings
	{
		private ICollection<IPEndPoint> endPointsToReplicateTo = new IPEndPoint[0];
		private ICollection<string> databasesToReplicate = new string[0];
		private bool locked;

		/// <summary>Locks replication settings object.</summary>
		public void Lock() { locked = true; }

		/// <summary>List of CouchDB endpoints to replicate data to.</summary>
		public ICollection<IPEndPoint> EndPointsToReplicateTo
		{
			get { return endPointsToReplicateTo; }
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				ThrowIfLocked();
				
				endPointsToReplicateTo = new ReadOnlyCollection<IPEndPoint>(value.ToArray());
			}
		}

		/// <summary>List of database names to replicate.</summary>
		public ICollection<string> DatabasesToReplicate
		{
			get { return databasesToReplicate; } 
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				foreach (var dbName in value)
					CheckIf.DatabaseNameIsOk(dbName, "value");
				ThrowIfLocked();
				databasesToReplicate = new ReadOnlyCollection<string>(value.ToArray());
			}
		}

		private void ThrowIfLocked()
		{
			if (locked)
				throw new InvalidOperationException("Settings should not be modified after bootstrap pocess started.");
		}
	}
}