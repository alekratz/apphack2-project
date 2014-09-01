using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using Nito;
using transf.Log;

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
        private TcpClient directClient;
        private Deque<Message> directSendQueue;
        private Deque<Message> directRecvQueue;
        private HashSet<Socket> connectedClients; // the list of connected clients
        private int port;

        /// <summary>
        /// Gets the number of datagrams queued and available for reading
        /// </summary>
        public int DatagramsAvailable { get { return datagramRecvQueue.Count; } }

        public MessageWorker()
        {
            datagramSendQueue = new Deque<Message>();
            datagramRecvQueue = new Deque<Message>();
            directSendQueue = new Deque<Message>();
            directRecvQueue = new Deque<Message>();
        }

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
        /// Gets the next datagram in the queue based on any delimiters specified.
        /// </summary>
        /// <param name="from">Specifies who the next datagram returned should be from.</param>
        /// <returns>The next datagram in the queue. If none, returns null.</returns>
        public byte[] NextDatagram(IPAddress from = null)
        {
            lock (datagramRecvQueue)
            {
                // nothing is available
                if (datagramRecvQueue.Count == 0)
                    return null;
                if (from == null)
                {
                    return datagramRecvQueue.RemoveFromFront().Item2;
                }
                else
                {
                    // find the first item in the queue that matches the address
                    for (int i = 0; i < datagramRecvQueue.Count; i++)
                    {
                        if (datagramRecvQueue[i].Item1 == from)
                        {
                            byte[] msg = datagramRecvQueue[i].Item2;
                            datagramRecvQueue.RemoveAt(i);
                            return msg;
                        }
                    }
                    // wasn't found, return null
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the next direct message in the queue based on any delimiters specified.
        /// </summary>
        /// <param name="from">Specifies who the next direct message returned should be from.</param>
        /// <returns>The next direct message in the queue. If none, returns null.</returns>
        public byte[] NextDirectMessage(IPAddress from = null)
        {
            lock (directRecvQueue)
            {
                if (directRecvQueue.Count == 0)
                    return null;
                if (from == null)
                    return directRecvQueue.RemoveFromFront().Item2;
                else
                {
                    for (int i = 0; i < directRecvQueue.Count; i++)
                    {
                        if (directRecvQueue[i].Item1 == from)
                        {
                            byte[] msg = directRecvQueue[i].Item2;
                            directRecvQueue.RemoveAt(i);
                            return msg;
                        }
                    }
                    return null;
                }
            }
        }

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
                        acceptSocket = directClient.Client.Accept();
                        // add it to the list of connected clients
                        connectedClients.Add(acceptSocket); 
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
                }
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
                    }
                    catch (SocketException ex)
                    {
                        Logger.WriteError(Logger.GROUP_NET, "Could not send message to {0} on port {1}", to.Address, to.Port);
                        Logger.WriteError(Logger.GROUP_NET, ex.Message);
                    }
                }
            }
        }

        protected override void Run(object arg)
        {
            Debug.Assert(arg != null, "Arguments for MessageWorker.Start() must not be null");
            object[] args = (object[])arg;
            Debug.Assert(args.Length == 1, "1 argument required for DiscoveryWorker.Start(), (int)");
            port = (int)args[0];

            // Bind both of the sockets to the port
            datagramClient = new UdpClient(port)
            {
                DontFragment = true,
                EnableBroadcast = true,
                //ExclusiveAddressUse = true,
                MulticastLoopback = false
            };

            directClient = new TcpClient(new IPEndPoint(IPAddress.Loopback, port));
            directClient.Client.Blocking = false;

            while(!StopSignal)
            {
                const int SLEEP_TIME_MS = 500;
                ReceiveMessages();
                SendMessages();
                Thread.Sleep(SLEEP_TIME_MS);
            }
        }
    }
}
