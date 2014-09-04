using System;
using System.Net;
using transf.Utils;

namespace transf.Net
{
	public struct Node
	{
		public string Nickname { get; set; }
		public IPAddress RemoteAddress { get; set; }
		public ulong LastCheckin { get; set; }

		public const ulong MAX_TIMEOUT = 10000; // 10000 ms is the timeout

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

