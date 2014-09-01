using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using Nito;
using transf.Log;
using transf.Utils;

namespace transf
{
    // Some typedefs
    using Message = Tuple<IPAddress, byte[]>;

    class MessageWorker
        : WorkerThread
    {
        private UdpClient datagramClient;
        private Deque<Message> datagramSendQueue;
        private Deque<Message> datagramRecvQueue;
        private TcpListener directClient;
        private Deque<Message> directSendQueue;
        private Deque<Message> directRecvQueue;
        private HashSet<Socket> connectedClients; // the list of connected clients
        private int port;

        /// <summary>
        /// Gets the number of datagrams queued and available for reading
        /// </summary>
        public int DatagramsAvailable { get { return datagramRecvQueue.Count; } }

        #region Singleton members
        private static MessageWorker instance;
        public static MessageWorker Instance
        {
            get
            {
                if (instance == null)
                    instance = new MessageWorker();
                return instance;
            }
        }
        #endregion

        private MessageWorker()
        {
            datagramSendQueue = new Deque<Message>();
            datagramRecvQueue = new Deque<Message>();
            directSendQueue = new Deque<Message>();
            directRecvQueue = new Deque<Message>();

            connectedClients = new HashSet<Socket>();
        }

        #region Read/write methods
        /// <summary>
        /// Enqueues a datagram to be sent to a given address.
        /// </summary>
        /// <param name="data">The data to send</param>
        /// <param name="to">The address to send the data to</param>
        public void SendDatagram(byte[] data, IPAddress to)
        {
            lock (datagramSendQueue)
            {
                datagramSendQueue.AddToBack(new Message(to, data));
            }
        }

        /// <summary>
        /// Enqueues a message to be sent directly to a source.
        /// </summary>
        /// <param name="data">The data to send</param>
        /// <param name="to">The address to send the data to</param>
        public void SendDirect(byte[] data, IPAddress to)
        {
            lock (directSendQueue)
            {
                directSendQueue.AddToBack(new Message(to, data));
            }
        }

        /// <summary>
        /// Gets the next datagram in the queue
        /// </summary>
        /// <returns>The next datagram in the queue. If none, returns null.</returns>
        public byte[] NextDatagram(ref IPAddress sender)
        {
            lock (datagramRecvQueue)
            {
                // nothing is available
                if (datagramRecvQueue.Count == 0)
                    return null;
                Message message = datagramRecvQueue.RemoveFromFront();
                sender = message.Item1;
                return message.Item2;
            }
        }

        /// <summary>
        /// Gets the next datagram in the queue from the specified address
        /// </summary>
        /// <param name="from">Specifies who the next datagram returned should be from.</param>
        /// <returns></returns>
        public byte[] NextDatagramFrom(ref IPAddress sender, IPAddress from)
        {
            lock (datagramRecvQueue)
            {
                // nothing is available
                if (datagramRecvQueue.Count == 0)
                    return null;
                // find the first item in the queue that matches the address
                for (int i = 0; i < datagramRecvQueue.Count; i++)
                {
                    if (datagramRecvQueue[i].Item1 == from)
                    {
                        sender = datagramRecvQueue[i].Item1;
                        byte[] msg = datagramRecvQueue[i].Item2;
                        datagramRecvQueue.RemoveAt(i);
                        return msg;
                    }
                }
                // wasn't found, return null
                return null;
            }
        }

        /// <summary>
        /// Gets the next direct message in the queue based on any delimiters specified.
        /// </summary>
        /// <param name="from">Specifies who the next direct message returned should be from.</param>
        /// <returns>The next direct message in the queue. If none, returns null.</returns>
        public byte[] NextDirectMessage(ref IPAddress sender)
        {
            lock (directRecvQueue)
            {
                if (directRecvQueue.Count == 0)
                    return null;
                Message message = directRecvQueue.RemoveFromFront();
                sender = message.Item1;
                return message.Item2;
            }
        }

        public byte[] NextDirectMessageFrom(ref IPAddress sender, IPAddress from)
        {
            lock (directRecvQueue)
            {
                if (directRecvQueue.Count == 0)
                    return null;
                for (int i = 0; i < directRecvQueue.Count; i++)
                {
                    if (directRecvQueue[i].Item1 == from)
                    {
                        sender = directRecvQueue[i].Item1;
                        byte[] msg = directRecvQueue[i].Item2;
                        directRecvQueue.RemoveAt(i);
                        return msg;
                    }
                }
                return null;
            }
        }
        #endregion

        /// <summary>
        /// Accepts any pending connections to the connectedClients pool.
        /// </summary>
        private void AcceptConnections()
        {
            lock (connectedClients)
            {
                while (true)
                {
                    Socket acceptSocket = null;
                    try
                    {
                        // accept the socket
                        // TODO : change this to AcceptTcpClient()
                        acceptSocket = directClient.AcceptSocket();
                        // add it to the list of connected clients
                        connectedClients.Add(acceptSocket);
                        IPAddress addr = ((IPEndPoint)acceptSocket.RemoteEndPoint).Address;
                        Logger.WriteInfo(Logger.GROUP_NET, "Accepted connection from {0}", addr);
                    }
                    catch (SocketException) { break; } // don't do anything, it fails if no connections are available
                }
            }
        }

        /// <summary>
        /// Checks the datagram client for any available messages, and moves them to the datagram receive queue.
        /// Checks the direct message client for any available messages, and moves them to the direct message receive queue.
        /// </summary>
        private void ReceiveMessages()
        {
            // Datagrams
            lock (datagramRecvQueue)
            {
                // Check datagram for any messages
                while (datagramClient.Available > 0)
                {
                    try
                    {
                        IPEndPoint endpoint = new IPEndPoint(0, 0);
                        byte[] data = datagramClient.Receive(ref endpoint);
                        datagramRecvQueue.AddToBack(new Message(endpoint.Address, data));
                        Logger.WriteVerbose(Logger.GROUP_NET, "Received datagram from {0}, {1} bytes", endpoint.Address, data.Length);
                    }
                    catch (SocketException ex)
                    {
                        Logger.WriteError(Logger.GROUP_NET, "Could not receive datagram message");
                        Logger.WriteError(Logger.GROUP_NET, ex.Message);
                    }
                }
            }

            // Direct messages
            lock (directRecvQueue)
            {
                lock (connectedClients)
                {
                    foreach (Socket client in connectedClients)
                    {
                        NetworkStream stream = new NetworkStream(client);
                        while (stream.DataAvailable)
                        {
                            int readSize;
                            byte[] readSizeBytes = new byte[4];
                            stream.Read(readSizeBytes, 0, 4);
                            readSize = BitConverter.ToInt32(readSizeBytes, 0);

                            byte[] message = new byte[readSize];
                            stream.Read(message, 0, readSize);
                            IPAddress addr = ((IPEndPoint)client.RemoteEndPoint).Address;
                            datagramRecvQueue.AddToBack(new Message(addr, message));
                            Logger.WriteVerbose(Logger.GROUP_NET, "Received direct message from {0}, {1} bytes", addr, readSize);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sends all messages in each of the message queues
        /// </summary>
        private void SendMessages()
        {
            // Datagrams
            lock (datagramSendQueue)
            {
                foreach (Message message in datagramSendQueue)
                {
                    IPAddress toAddr = message.Item1;
                    byte[] data = message.Item2;
                    IPEndPoint to = new IPEndPoint(toAddr, port);
                    datagramClient.Send(data, data.Length, to);
                    Logger.WriteVerbose(Logger.GROUP_NET, "Sent datagram to {0}, {1} bytes", message.Item1, message.Item2.Length);
                }
                datagramSendQueue.Clear();
            }

            // Direct messages
            lock (directSendQueue)
            {
                foreach (Message message in directSendQueue)
                {
                    IPAddress toAddr = message.Item1;
                    byte[] data = message.Item2;
                    IPEndPoint to = new IPEndPoint(toAddr, port);
                    // Use a different client to connect to the desired host
                    TcpClient client = new TcpClient();
                    try
                    {
                        client.Connect(to);
                        client.GetStream().Write(data, 0, data.Length);
                        client.GetStream().Close();
                        Logger.WriteVerbose(Logger.GROUP_NET, "Sent direct message to {0}, {1} bytes", message.Item1, message.Item2.Length);
                    }
                    catch (SocketException ex)
                    {
                        Logger.WriteError(Logger.GROUP_NET, "Could not send message to {0} on port {1}", to.Address, to.Port);
                        Logger.WriteError(Logger.GROUP_NET, ex.Message);
                    }
                }
                directSendQueue.Clear();
            }
        }

        protected override void Run(object arg)
        {
            Debug.Assert(arg != null, "Arguments for MessageWorker.Start() must not be null");
            object[] args = (object[])arg;
            Debug.Assert(args.Length == 1, "1 argument required for DiscoveryWorker.Start(), (int)");
            port = (int)args[0];

            Logger.WriteInfo(Logger.GROUP_NET, "Starting message worker");

            // Bind both of the sockets to the port
            datagramClient = new UdpClient(port)
            {
                DontFragment = true,
                EnableBroadcast = true,
                //ExclusiveAddressUse = true,
                MulticastLoopback = false
            };

            directClient = new TcpListener(new IPEndPoint(IPAddress.Loopback, port));
            directClient.Server.Blocking = false;
            directClient.Start();

            while(!StopSignal)
            {
                const int SLEEP_MS = 500;
                ulong timeStart = TimeUtils.GetUnixTimestampMs();

                AcceptConnections();
                ReceiveMessages();
                SendMessages();

                ulong timeEnd = TimeUtils.GetUnixTimestampMs();
                ulong timeDelta = timeEnd - timeStart;

                // Sleep
                if (timeDelta < SLEEP_MS)
                    Thread.Sleep((int)(SLEEP_MS - timeDelta));
                else if(timeDelta > SLEEP_MS)
                    Logger.WriteWarning(Logger.GROUP_NET,
                        "Overload on message worker time delta ({0} ms slower than the {1} ms allotted)",
                        timeDelta - SLEEP_MS, SLEEP_MS);
            }
        }
    }
}
