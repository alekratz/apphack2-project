using System;
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
    /// Announces the directories to anyone who asks. Runs as a worker thread.
    /// </summary>
    class DirectoryAnnouncer
        : WorkerThread
    {
        public DirectoryEntry RootEntry { get; set; }

        public DirectoryAnnouncer(DirectoryEntry rootEntry)
        {
            RootEntry = rootEntry;
        }

        /// <summary>
        /// Sends a list of every file based on the RootEntry property.
        /// </summary>
        /// <param name="remoteAddr">Address to send this message to.</param>
        private void SendDirectoryListing(IPAddress remoteAddr)
        {
            List<byte> buffer = new List<byte>();
            FileEntry[] fEntries = RootEntry.Tree;
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

        protected override bool Initialize(params object[] args)
        {
            return true;
        }

        protected override void Run()
        {
            while (!StopSignal)
            {
                // check for messages that match the directory request
                Message dMsg = MessageWorker.Instance.NextMessage(
                                   msg => msg.Opcode == Opcode.RequestDirectoryListing);
                
                if (dMsg != null)
                {
                    // if there's a request for the directory listing, send the directory listing
                    SendDirectoryListing(dMsg.RemoteAddress);
                }

                Thread.Sleep(50);
            }
        }

    }
}

