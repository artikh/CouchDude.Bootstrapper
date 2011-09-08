using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace CouchDude.Bootstrapper
{
	/// <summary>String utilities class.</summary>
	public static class StringUtils
	{
		/// <summary>Combines sequence of strings to one string separated by provided characters.</summary>
		[Pure]
		public static string Combine(this IEnumerable<string> parts, string separator = " ")
		{
			var result = new StringBuilder();
			foreach (var part in parts)
				result.Append(part).Append(separator);

			if (result.Length > separator.Length)
				result.Remove(result.Length - separator.Length, separator.Length);

			return result.ToString();
		}
		
		/// <summary>Computes base64-encoded SHA1 hash form the string.</summary>
		[Pure]
		public static string ToSha1(this string self)
		{
			if (self == null) throw new ArgumentNullException("self");
			Contract.EndContractBlock();

			using (var sha1Algorithm = SHA1.Create())
			{
				var bytes = Encoding.Unicode.GetBytes(self);
				var hashBytes = sha1Algorithm.ComputeHash(bytes);
				return Convert.ToBase64String(hashBytes);
			}
		}
	}
}
