using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CouchDude.Bootstrapper.Azure
{
	static class GetFileTask
	{
		public static Task<FileInfo> Start(DirectoryInfo tempDir, string distributiveNameOrUrl)
		{
			Uri distributiveUri;
			if (Uri.TryCreate(distributiveNameOrUrl, UriKind.Absolute, out distributiveUri))
				return DownloadFile(tempDir, distributiveUri);
			else
				return GetLocalFile(distributiveNameOrUrl);
		}

		public static Task<FileInfo> DownloadFile(DirectoryInfo tempDir, Uri distributiveUri)
		{
			var httpClient = new HttpClient();
			var requestMessage = new HttpRequestMessage(HttpMethod.Get, distributiveUri);
			return httpClient
				.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead)
				.ContinueWith(
					getDistributiveTask => {
					                       	var response = getDistributiveTask.Result;
					                       	response.EnsureSuccessStatusCode();
					                       	var downloadingFileName =
					                       		string.Format("{0}.{1}.zip", Guid.NewGuid(), distributiveUri.Segments.LastOrDefault() ?? string.Empty);
					                       	var tempFile = new FileInfo(Path.Combine(tempDir.FullName, downloadingFileName));
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

		public static Task<FileInfo> GetLocalFile(string distributiveNameOrUrl)
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
				      			String.Format(
				      				"Distributive file {0} have not been found. Check \"Copy to Output Directory\" property is set to \"Copy always\"",
				      				distributiveFile.FullName));
				      	return distributiveFile;
				});
		}
	}
}