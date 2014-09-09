using System;
using System.Net;
using System.Collections.Generic;
using transf.Utils;

namespace transf.Net
{
	class Node
	{
		public string Nickname { get; set; }
		public IPAddress RemoteAddress { get; set; }
		public ulong LastCheckin { get; set; }
        public ulong LastDirectoryListing { get; set; }
        public ulong LastDirectoryRequest { get; set; }
        public HashSet<RemoteFileEntry> DirectoryListing { get; set; }

		public const ulong MAX_TIMEOUT = 10000; // 10000 ms is the timeout      
        public const ulong MAX_DIRECTORY_TIMEOUT = 10000; // discover new files every 10 seconds

        public Node()
        {
              DirectoryListing = new HashSet<RemoteFileEntry>();
        }

		/// <summary>
		/// Determines whether this instance has timed out, based on the maximum timeout.
		/// </summary>
		/// <returns><c>true</c> if this instance has timed out based on the current Unix time; otherwise, <c>false</c>.</returns>
		public bool HasTimedOut()
		{
			ulong currTimeMs = TimeUtils.GetUnixTimestampMs ();
			return currTimeMs - LastCheckin > MAX_TIMEOUT;
		}

		public override int GetHashCode ()
		{
			return Nickname.GetHashCode () ^ RemoteAddress.GetHashCode ();
		}

        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == GetHashCode();
        }
	}
}   