using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using transf.Log;
using transf.Utils;

namespace transf
{
	public class DiscoveryWorker : WorkerThread
	{
		//public const int DEFAULT_PORT = 44444;
		/// <summary>
		/// The "magic number" at the beginning of each of the packets, to denote that we care about it
		/// </summary>
		public const uint MAGIC = 0xf510ba2d;

		/// <summary>
		/// The broadcast period, in milliseconds. This is the amount of time between broadcasts.
		/// </summary>
		public const int BCAST_PERIOD_MS = 1000;

        /// <summary>
        /// The set of discovered nodes by the discovery worker
        /// </summary>
        public HashSet<Node> DiscoveredNodes { get; private set; }

        #region Singleton methods
        private static DiscoveryWorker instance = null;
        public static DiscoveryWorker Instance
        {
            get
            {
                if (instance == null)
                    instance = new DiscoveryWorker();
                return instance;
            }
        }
        #endregion

        private IPAddress thisAddr;
		private int port;
		private string nickname;
		private ulong lastBcast;

		private DiscoveryWorker ()
		{
		}

        #region Utility methods
        /// <summary>
		/// Emits the discovery signal for this worker
		/// </summary>
		private void EmitDiscovery()
		{
			ulong lastBcastDelta = TimeUtils.GetUnixTimestampMs () - lastBcast;
			if (lastBcastDelta >= BCAST_PERIOD_MS)
			{
				Logger.WriteVerbose (Logger.GROUP_NET, "Emitting discovery signal");
				// Create a broadcast packet
				// starts with the 4-byte magic number and a nickname
				byte[] packet = new byte[4 + nickname.Length];
				BitConverter.GetBytes (MAGIC).CopyTo (packet, 0);
				Encoding.ASCII.GetBytes (nickname).CopyTo (packet, 4);
				// Send it to the broadcast address
                MessageWorker.Instance.SendDatagram(packet, IPAddress.Broadcast);
				lastBcast = TimeUtils.GetUnixTimestampMs ();
			}
		}

		/// <summary>
		/// Checks to see if there are any datagrams that have been sent for discovery.
		/// </summary>
		private void CheckMessages()
		{
			while (MessageWorker.Instance.DatagramsAvailable > 0)
			{
                IPAddress address = null;
				// Get the data
				byte[] msg = MessageWorker.Instance.NextDatagram(ref address);
				if (msg.Length < 4) // it's not got at least four bytes, so ignore it
					continue;
				// get the first four bytes and compare it to the magic number
				uint magic = BitConverter.ToUInt32 (msg, 0);
                if (magic != MAGIC) // if it's not the magic number, continue
                    continue;
                if (address.Equals(thisAddr)) // if it's us, continue
                {
                    Logger.WriteVerbose(Logger.GROUP_NET, "Received discovery signal from self");
                    continue;
                }

				byte[] noMagicMsg = new byte[msg.Length - 4];
				Array.Copy (msg, 4, noMagicMsg, 0, noMagicMsg.Length);
				string remoteNickname = Encoding.ASCII.GetString (noMagicMsg);

				Node remoteNode = new Node () 
				{
					Nickname = remoteNickname,
					RemoteAddress = address,
					LastCheckin = TimeUtils.GetUnixTimestampMs ()
				};

				// If it couldn't be added, then it exists in the set already
				// remove it and add the new updated one
                if (!DiscoveredNodes.Add(remoteNode))
                {
                    DiscoveredNodes.Remove(remoteNode);
                    DiscoveredNodes.Add(remoteNode);
                    Logger.WriteVerbose(Logger.GROUP_NET, "Refreshing connection with {0}", address);
                }
                else
                {
                    Logger.WriteInfo(Logger.GROUP_NET, "Discovered new user, {0} at {1}", remoteNickname, address, address.GetHashCode());
                }
			}
		}

		/// <summary>
		/// Removes any nodes that have timed out.
		/// </summary>
		private void PruneNodes()
		{
			List<Node> toRemove = new List<Node> ();
			foreach (Node node in DiscoveredNodes)
			{
				if (node.HasTimedOut ())
					toRemove.Add (node);
			}

            if(toRemove.Count > 0)
                Logger.WriteDebug(Logger.GROUP_NET, "Pruning {0} nodes from discovery worker", toRemove.Count);
			foreach (Node node in toRemove)
				DiscoveredNodes.Remove (node);
		}
        #endregion

        protected override bool Initialize(params object[] args)
        {
            port = (int)args[0];
            nickname = (string)args[1];

            DiscoveredNodes = new HashSet<Node>();

            // Get the local address on the entire network
            try
            {
                Logger.WriteVerbose(Logger.GROUP_NET, "Getting DNS host entry");
                IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                foreach (IPAddress addr in addressList)
                {
                    // discard IPv6 addresses
                    if (addr.IsIPv6LinkLocal || addr.IsIPv6Multicast || addr.IsIPv6SiteLocal || addr.IsIPv6Teredo)
                        continue;
                    else
                    {
                        thisAddr = addr;
                        break;
                    }
                }
                Logger.WriteVerbose(Logger.GROUP_NET, "Got network IP address, hello {0}", thisAddr);
            }
            catch (IndexOutOfRangeException)
            {
                Logger.WriteError(Logger.GROUP_NET, "Failed to start discovery worker thread due to DNS resolve error");
                return false;
            }

            // Reset this so that it will force the emit discovery signal
            lastBcast = 0;

            return true;
        }

        /// <summary>
		/// The logic of the discoveryworker.
		/// </summary>
		/// <param name="port">The port to run on</param>
		/// <param name="nickname">The nickname to emit a discovery signal as</param> 
		protected override void Run ()
		{
			Logger.WriteInfo (Logger.GROUP_NET, "Starting discovery worker");

			while (!StopSignal)
			{
				const int SLEEP_MS = 50;
				ulong timeStart = TimeUtils.GetUnixTimestampMs ();

				EmitDiscovery ();
				CheckMessages ();
				PruneNodes ();

				ulong timeEnd = TimeUtils.GetUnixTimestampMs ();
				ulong timeDelta = timeEnd - timeStart;

				// Sleep
				if (timeDelta < SLEEP_MS)
					Thread.Sleep ((int)(SLEEP_MS - timeDelta));
				else
					Logger.WriteWarning (Logger.GROUP_NET,
						"Overload on discovery worker time delta ({0} ms slower than the {1} ms allotted)",
						timeDelta, SLEEP_MS);
			}
		}
	}
}

