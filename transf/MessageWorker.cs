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
using transf.Net;

namespace transf
{
    class MessageWorker
        : WorkerThread
    {
        private UdpClient datagramClient;
        private TcpListener directClient;
        private Deque<Message> recvQueue;
        private Deque<Message> sendQueue;
        private HashSet<Socket> connectedClients; // the list of connected clients
        private int port;

        /// <summary>
        /// Gets the number of datagrams queued and available for reading
        /// </summary>
        public int DatagramsAvailable { get { return recvQueue.Count; } }

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
            recvQueue = new Deque<Message>();
            sendQueue = new Deque<Message>();

            connectedClients = new HashSet<Socket>();
        }

        #region Read/write methods
        /// <summary>
        /// Enqueues a message to be sent.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        public void SendMessage(Message message)
        {
            sendQueue.AddToBack(message);
        }

        /// <summary>
        /// Gets the next datagram in the queue
        /// </summary>
        /// <returns>The next datagram in the queue. If none, returns null.</returns>
        public Message NextDatagram()
        {
            lock (recvQueue)
            {
                for (int i = 0; i < recvQueue.Count; i++)
                {
                    if (recvQueue[i].MessageType == MessageType.Datagram)
                    {
                        Message msg = recvQueue[i];
                        recvQueue.RemoveAt(i);
                        return msg;
                    }
                }
            }
            // didn't return, so it's nothing
            return null;
        }

        /// <summary>
        /// Gets the next datagram in the queue from the specified address
        /// </summary>
        /// <param name="from">Specifies who the next datagram returned should be from.</param>
        /// <returns></returns>
        public Message NextDatagramFrom(IPAddress from)
        {
            lock (recvQueue)
            {
                // find the first item in the queue that matches the address
                for (int i = 0; i < recvQueue.Count; i++)
                {
                    if (recvQueue[i].MessageType == MessageType.Datagram && recvQueue[i].RemoteAddress == from)
                    {
                        Message msg = recvQueue[i];
                        recvQueue.RemoveAt(i);
                        return msg;
                    }
                }
            }
            // wasn't found, return null
            return null;
        }

        /// <summary>
        /// Gets the next direct message in the queue.
        /// </summary>
        /// <returns>The next direct message in the queue. If none, returns null.</returns>
        public Message NextDirectMessage()
        {
            lock (recvQueue)
            {
                if (recvQueue.Count == 0)
                    return null;
                for (int i = 0; i < recvQueue.Count; i++)
                {
                    if (recvQueue[i].MessageType == MessageType.Direct)
                    {
                        Message message = recvQueue[i];
                        recvQueue.RemoveAt(i);
                        return message;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the next direct message in the queue based on any delimiters specified.
        /// </summary>
        /// <param name="from">Specifies who the next direct message returned should be from.</param>
        /// <returns>The next direct message in the queue. If none, returns null.</returns>
        public Message NextDirectMessageFrom(IPAddress from)
        {
            lock (recvQueue)
            {
                if (recvQueue.Count == 0)
                    return null;
                for (int i = 0; i < recvQueue.Count; i++)
                {
                    if (recvQueue[i].MessageType == MessageType.Direct && recvQueue[i].RemoteAddress == from)
                    {
                        Message msg = recvQueue[i];
                        recvQueue.RemoveAt(i);
                        return msg;
                    }
                }
            }
            return null;
        }
        #endregion

        #region Utility methods
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
            lock (recvQueue)
            {
                // Datagrams
                while (datagramClient.Available > 0)
                {
                    try
                    {
                        IPEndPoint endpoint = new IPEndPoint(0, 0);
                        byte[] data = datagramClient.Receive(ref endpoint);
                        recvQueue.AddToBack(new Message(MessageType.Datagram, endpoint.Address, data));
                        Logger.WriteVerbose(Logger.GROUP_NET, "Received datagram from {0}, {1} bytes", endpoint.Address, data.Length);
                    }
                    catch (SocketException ex)
                    {
                        Logger.WriteError(Logger.GROUP_NET, "Could not receive datagram message");
                        Logger.WriteError(Logger.GROUP_NET, ex.Message);
                    }
                }

                // Direct messages
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
                            recvQueue.AddToBack(new Message(MessageType.Direct, addr, message));
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

            lock (sendQueue)
            {
                // Datagrams
                foreach (Message message in sendQueue)
                {
                    IPEndPoint to = new IPEndPoint(message.RemoteAddress, port);
                    switch (message.MessageType)
                    {
                        case MessageType.Broadcast:
                            to.Address = IPAddress.Broadcast; // guarantees that it will be broadcast
                            datagramClient.Send(message.RawMessage, message.RawMessage.Length, to);
                            break;
                        case MessageType.Datagram:
                            datagramClient.Send(message.RawMessage, message.RawMessage.Length, to);
                            break;
                        case MessageType.Direct:
                            TcpClient client = new TcpClient();
                            try
                            {
                                client.Connect(to);
                                client.GetStream().Write(message.RawMessage, 0, message.RawMessage.Length);
                                Logger.WriteVerbose(Logger.GROUP_NET, "Sent direct message to {0}, {1} bytes", message.RemoteAddress, message.RawMessage.Length);
                            }
                            catch (SocketException ex)
                            {
                                Logger.WriteError(Logger.GROUP_NET, "Could not send message to {0} on port {1}", to.Address, to.Port);
                                Logger.WriteError(Logger.GROUP_NET, ex.Message);
                            }
                            break;
                    }
                }
                sendQueue.Clear();
            }
        }
        #endregion

        protected override bool Initialize(params object[] args)
        {
            port = (int)args[0];

            // Bind both of the sockets to the port
            try
            {
                datagramClient = new UdpClient(port)
                {
                    DontFragment = true,
                    EnableBroadcast = true,
                    //ExclusiveAddressUse = true,
                    MulticastLoopback = false
                };
            }
            catch (SocketException ex)
            {
                Logger.WriteError(Logger.GROUP_NET, "Could not bind datagram listener to port {0}, exiting", port);
                Logger.WriteError(Logger.GROUP_NET, ex.Message);
                return false;
            }

            try
            {
                directClient = new TcpListener(new IPEndPoint(IPAddress.Loopback, port));
                directClient.Server.Blocking = false;
                directClient.Start();
            }
            catch (SocketException ex)
            {
                Logger.WriteError(Logger.GROUP_NET, "Could not bind direct message listener to port {0}, exiting", port);
                Logger.WriteError(Logger.GROUP_NET, ex.Message);
                return false;
            }

            return true;
        }

        protected override void Run()
        {
            Logger.WriteInfo(Logger.GROUP_NET, "Starting message worker");

            while(!StopSignal)
            {
                const int SLEEP_MS = 500;
                ulong timeStart = TimeUtils.GetUnixTimestampMs();

                // TODO : add PruneConnections() method
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
