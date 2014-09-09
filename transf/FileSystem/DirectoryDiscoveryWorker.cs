using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using transf.Net;
using transf.Utils;

// TODO : maybe change this to the Net namespace
namespace transf.FileSystem
{
    /// <summary>
    /// Aids in the discovery of remote directories.
    /// </summary>
    class DirectoryDiscoveryWorker
        : WorkerThread
    {
        public DirectoryEntry LocalRootEntry { get; set; }

        public DirectoryDiscoveryWorker(DirectoryEntry rootEntry)
        {
            LocalRootEntry = rootEntry;
        }

        /// <summary>
        /// Sends a list of every file based on the RootEntry property.
        /// </summary>
        /// <param name="remoteAddr">Address to send this message to.</param>
        private void SendDirectoryListing(IPAddress remoteAddr)
        {
            List<byte> buffer = new List<byte>();
            FileEntry[] fEntries = LocalRootEntry.Tree;
            // each file path will be followed by its hash and its size as eight bytes
            foreach (FileEntry fEntry in fEntries)
            {
                buffer.AddRange(Encoding.ASCII.GetBytes(fEntry.RelativePath));
                buffer.Add(0);
                buffer.AddRange(fEntry.HashBytes);
                buffer.AddRange(BitConverter.GetBytes(fEntry.Size));
            }

            Message message = Message.CreateOutgoingMessage(MessageType.Direct,
                                  remoteAddr, Opcode.DirectoryListing, buffer.ToArray());
            MessageWorker.Instance.SendMessage(message);
        }

        /// <summary>
        /// Requests the directory listings from remote users if it hasn't been
        /// updated in a while.
        /// </summary>
        private void RequestDirectoryListings()
        {
            ulong now = TimeUtils.GetUnixTimestampMs();
            // get the node list
            var nodeList = DiscoveryWorker.Instance.DiscoveredNodes;
            // Get the list of nodes that need to be updated, where it's been over 
            // MAX_DIRECTORY_TIMEOUT milliseconds since the last directory listing,
            // and where it's been over MAX_DIRECTORY_TIMEOUT milliseconds since the
            // last request was made.
            var outOfDateNodes = nodeList.Where(
                node => now - node.LastDirectoryListing > Node.MAX_DIRECTORY_TIMEOUT && 
                now - node.LastDirectoryRequest > Node.MAX_DIRECTORY_TIMEOUT);
            // go through them and request directory listings from each
            foreach (var node in outOfDateNodes)
            {
                // create the message
                // consider making it a datagram?
                Message message = Message.CreateOutgoingMessage(
                    MessageType.Direct, node.RemoteAddress, Opcode.RequestDirectoryListing, new byte[] { });
                node.LastDirectoryRequest = now;
                MessageWorker.Instance.SendMessage(message);
            }
        }

        /// <summary>
        /// Receives all messages concerning directory discovery and handles them
        /// </summary>
        private void ReceiveMessages()
        {
            Message dMsg;
            // check for messages that match the directory request
            while((dMsg = MessageWorker.Instance.NextMessage(
                msg => msg.Opcode == Opcode.RequestDirectoryListing)) != null)
            {
                // if there's a request for the directory listing, send the directory listing
                SendDirectoryListing(dMsg.RemoteAddress);
            }

            // Check for new directory listing messages
            while ((dMsg = MessageWorker.Instance.NextMessage(
                msg => msg.Opcode == Opcode.DirectoryListing)) != null)
            {
                // make sure it's got a valid header
                if (!dMsg.HasValidHeader())
                    continue;
                ulong naow = TimeUtils.GetUnixTimestampMs();
                // grab nodes from the discovery worker
                Node node = DiscoveryWorker.Instance.DiscoveredNodes.FirstOrDefault(
                                n => n.RemoteAddress == dMsg.RemoteAddress);
                // if this node didn't announce itself, then make a new one and
                // store it
                if (node == null)
                {
                    node = new Node()
                    {
                        LastCheckin = naow,
                        LastDirectoryListing = naow,
                        Nickname = "",
                        RemoteAddress = dMsg.RemoteAddress
                    };
                    DiscoveryWorker.Instance.DiscoveredNodes.Add(node);
                }

                node.DirectoryListing.Clear(); // clear it out

                // Read all of the directories that are available
                // relative path, null byte, 16 bytes of hash, 8 bytes of file length
                while (dMsg.Available > 0)
                {
                    string relativePath = dMsg.NextString();
                    byte[] hash = new byte[16];
                    dMsg.Next(ref hash, 16);
                    byte[] sizeBytes = new byte[8];
                    dMsg.Next(ref sizeBytes, 8);
                    ulong size = BitConverter.ToUInt64(sizeBytes, 0);

                    StringBuilder hex = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                        hex.AppendFormat("{0:x2}", b);
                    RemoteFileEntry entry = new RemoteFileEntry(node, relativePath, relativePath, hex.ToString());
                    node.DirectoryListing.Add(entry);
                }
            }
        }

        protected override bool Initialize(params object[] args)
        {
            return true;
        }

        protected override void Run()
        {
            while (!StopSignal)
            {
                ReceiveMessages();
                RequestDirectoryListings();

                Thread.Sleep(50);
            }
        }

    }
}

