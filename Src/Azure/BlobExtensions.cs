using Microsoft.WindowsAzure.StorageClient;

namespace CouchDude.Bootstrapper.Azure
{
	internal static class BlobExtensions
	{
		public static bool Exists(this CloudBlob blob)
		{
			try
			{
				blob.FetchAttributes();
				return true;
			}
			catch (StorageClientException e)
			{
				if (e.ErrorCode == StorageErrorCode.ResourceNotFound)
				{
					return false;
				}
				else
				{
					throw;
				}
			}
		}
	}
}
