using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Common.Logging;

namespace CouchDude.Bootstrapper
{
	class SetAclsTask: StartupTaskBase
	{
		private static readonly ILog Log = LogManager.GetCurrentClassLogger();
		
		public override void Invoke(BootstrapSettings settings)
		{
			var currentUserWindowsIdentity = WindowsIdentity.GetCurrent();
			if(currentUserWindowsIdentity == null)
				throw new InvalidOperationException("Current user Windows identity could not be retrived.");

			var currentUserSecurityIdentifier = currentUserWindowsIdentity.User;
			if (currentUserSecurityIdentifier == null)
				throw new InvalidOperationException("Current user Windows security identifier could not be retrived.");
			
			SetSecurity(FileSystemRights.FullControl, currentUserSecurityIdentifier, settings.WorkingDirectory);
			SetSecurity(FileSystemRights.FullControl, currentUserSecurityIdentifier, settings.LogDirectory);
			SetSecurity(FileSystemRights.FullControl, currentUserSecurityIdentifier, settings.DataDirectory);
		}

		private static void SetSecurity(FileSystemRights fileSystemRights, SecurityIdentifier currentUserSecurityIdentifier, DirectoryInfo directory)
		{
			Log.InfoFormat("Setting {0} rights for {1} on {2}", fileSystemRights, currentUserSecurityIdentifier.Value, directory.FullName);

			var directorySecurity = directory.GetAccessControl();
			directorySecurity.AddAccessRule(
				new FileSystemAccessRule(
					currentUserSecurityIdentifier,
					fileSystemRights,
					InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
					PropagationFlags.None,
					AccessControlType.Allow));
			directory.SetAccessControl(directorySecurity);
		}
	}
}