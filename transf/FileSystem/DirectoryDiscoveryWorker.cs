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
        // TODO : scrap this and rename it to DirectoryDiscoveryWorker
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
            dMsg = MessageWorker.Instance.NextMessage(msg => msg.Opcode == Opcode.RequestDirectoryListing);
            if (dMsg != null)
            {
                // if there's a request for the directory listing, send the directory listing
                SendDirectoryListing(dMsg.RemoteAddress);
            }

            // Check for new directory listing messages
            dMsg = MessageWorker.Instance.NextMessage(msg => msg.Opcode == Opcode.DirectoryListing);
            if (dMsg != null)
            {
                // New directory listings from nodes
                // TODO : record these
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

