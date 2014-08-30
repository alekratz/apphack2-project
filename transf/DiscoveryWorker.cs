using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using transf.Log;

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

		private HashSet<Node> discoveredNodes;
		private UdpClient dgramClient;
		private IPAddress thisAddr;
		private int port;
		private string nickname;
		private ulong lastBcast;

		public DiscoveryWorker ()
		{
		}

		/// <summary>
		/// Emits the discovery signal for this worker
		/// </summary>
		private void EmitDiscovery()
		{
			ulong lastBcastDelta = Utils.GetUnixTimestampMs () - lastBcast;
			if (lastBcastDelta >= BCAST_PERIOD_MS)
			{
				Logger.WriteDebug (Logger.GROUP_NET, "Emitting discovery signal");
				// Create a broadcast packet
				// starts with the 4-byte magic number and a nickname
				byte[] packet = new byte[4 + nickname.Length];
				BitConverter.GetBytes (MAGIC).CopyTo (packet, 0);
				Encoding.ASCII.GetBytes (nickname).CopyTo (packet, 4);
				// Send it to the broadcast address
				dgramClient.Send (packet, packet.Length, new IPEndPoint (IPAddress.Broadcast, port));
				lastBcast = Utils.GetUnixTimestampMs ();
			}
		}

		/// <summary>
		/// Checks to see if there are any datagrams that have been sent for discovery.
		/// </summary>
		private void CheckMessages()
		{
			while (dgramClient.Available > 0)
			{
				IPEndPoint remoteEndpoint = new IPEndPoint (0, 0);
				// Get the data
				byte[] msg = dgramClient.Receive (ref remoteEndpoint);
				if (msg.Length < 4) // it's not got at least four bytes, so ignore it
					continue;
				// get the first four bytes and compare it to the magic number
				uint magic = BitConverter.ToUInt32 (msg, 0);
				if (magic != MAGIC) // if it's not the magic number, continue
					continue;
				if (remoteEndpoint.Address.Equals(thisAddr)) // if it's us, continue
					continue;

				byte[] noMagicMsg = new byte[msg.Length - 4];
				Array.Copy (msg, 4, noMagicMsg, 0, noMagicMsg.Length);
				string remoteNickname = Encoding.ASCII.GetString (noMagicMsg);

				Node remoteNode = new Node () 
				{
					Nickname = remoteNickname,
					RemoteAddress = remoteEndpoint.Address,
					LastCheckin = Utils.GetUnixTimestampMs ()
				};

				// If it couldn't be added, then it exists in the set already
				// remove it and add the new updated one
				if (!discoveredNodes.Add (remoteNode))
				{
					discoveredNodes.Remove (remoteNode);
					discoveredNodes.Add (remoteNode);
				}
			}
		}

		/// <summary>
		/// Removes any nodes that have timed out.
		/// </summary>
		private void PruneNodes()
		{
			List<Node> toRemove = new List<Node> ();
			foreach (Node node in discoveredNodes)
			{
				if (node.HasTimedOut ())
					toRemove.Add (node);
			}

			foreach (Node node in toRemove)
				discoveredNodes.Remove (node);
		}

		/// <summary>
		/// The logic of the discoveryworker.
		/// </summary>
		/// <param name="port">The port to run on</param>
		/// <param name="nickname">The nickname to emit a discovery signal as</param> 
		protected override void Run (object arg)
		{
			Debug.Assert (arg != null, "Arguments for DiscoveryWorker() must not be null");
			object[] args = (object[])arg;
			Debug.Assert (args.Length == 2, "2 arguments required for DiscoveryWorker.Start(), (int, string)");
			port = (int)args [0];
			nickname = (string)args [1];

			discoveredNodes = new HashSet<Node> ();

			Logger.WriteInfo (Logger.GROUP_NET, "Starting discovery worker");
			// Get the local address on the entire network
			try
			{
				Logger.WriteVerbose(Logger.GROUP_NET, "Getting DNS host entry");
				thisAddr = Dns.GetHostEntry (Dns.GetHostName()).AddressList[0];
				Logger.WriteVerbose(Logger.GROUP_NET, "Got network IP address, hello {0}", thisAddr);
			}
			catch (IndexOutOfRangeException)
			{
				Logger.WriteError (Logger.GROUP_NET, "Failed to start discovery worker thread due to DNS resolve error");
				return;
			}

			// Create the datagram client
			try
			{
				Logger.WriteVerbose(Logger.GROUP_NET, "Binding datagram socket to port {0}", port);
				dgramClient = new UdpClient (port)
				{
					DontFragment = true,
					EnableBroadcast = true,
					//ExclusiveAddressUse = true,
					MulticastLoopback = false
				};
			}
			catch(SocketException ex)
			{
				// TODO : log this
				Logger.WriteError(Logger.GROUP_NET, "Failed to start discovery worker thread due to socket error");
				Logger.WriteError(Logger.GROUP_NET, ex.Message);
				return;
			}

			// Reset this so that it will force the emit discovery signal
			lastBcast = 0;

			while (!StopSignal)
			{
				const int SLEEP_MS = 50;
				ulong timeStart = Utils.GetUnixTimestampMs ();

				EmitDiscovery ();
				CheckMessages ();
				PruneNodes ();

				ulong timeEnd = Utils.GetUnixTimestampMs ();
				ulong timeDelta = timeEnd - timeStart;

				// Sleep
				if (timeDelta < SLEEP_MS)
					Thread.Sleep ((int)(SLEEP_MS - timeDelta));
				else
					Console.WriteLine ("WARNING: Overload on time delta (DiscoveryWorker)");
			}
		}
	}
}

