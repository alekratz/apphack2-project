using System;

namespace transf.Utils
{
	public static class TimeUtils
	{
		/// <summary>
		/// Gets the current unix timestamp.
		/// </summary>
		/// <returns>The current unix timestamp.</returns>
		public static uint GetUnixTimestamp()
		{
			var unixTime = DateTime.Now.ToUniversalTime() -
				new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			return (uint)unixTime.TotalSeconds;
		}

		/// <summary>
		/// Gets the current unix timestamp in ms.
		/// </summary>
		/// <returns>The current unix timestamp in ms.</returns>
		public static ulong GetUnixTimestampMs ()
		{
			var unixTime = DateTime.Now.ToUniversalTime() -
				new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			return (ulong)unixTime.TotalMilliseconds;
		}
	}
}

