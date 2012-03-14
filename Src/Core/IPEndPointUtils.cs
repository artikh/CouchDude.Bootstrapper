using System;
using System.Net;


namespace CouchDude.Bootstrapper
{
	/// <summary>Utility methods for <see cref="IPEndPointUtils"/>.</summary>
	public static class IPEndPointUtils
	{
		/// <summary>Constructs HTTP URL from endpoint.</summary>
		public static Uri ToHttpUri(this IPEndPoint endpoint)
		{
			if (endpoint == null) throw new ArgumentNullException("endpoint");

			var address = new UriBuilder(Uri.UriSchemeHttp, endpoint.Address.ToString(), endpoint.Port);
			return address.Uri;
		}
	}
}
