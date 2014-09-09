using System;
using System.Collections;
using System.Net;
using System.Text;

namespace transf.Net
{
    enum MessageType
    {
        Direct,
        Datagram,
        Broadcast
    }

    class Message
    {
        public bool IsOutgoing { get; private set; }
        public IPAddress RemoteAddress { get; private set; }
        public byte[] RawMessage { get; private set; }
        public MessageType MessageType { get; private set; }
        public int Available { get { return RawMessage.Length - iteratorIndex; } }
        public Opcode Opcode
        {
            get
            {
                if (RawMessage.Length < 6) // doesn't have the standard size of a message with magic + opcode
                    return Net.Opcode.None;
                // otherwise, get the last two bytes as a ushort
                ushort opcodeNum = BitConverter.ToUInt16(RawMessage, 4);
                return (Net.Opcode)opcodeNum;
            }
            set
            {
                // if the length is less than six, do nothing
                if (RawMessage.Length < 6)
                    return;
                ushort opcodeNum = (ushort)value;
                // write the bytes
                BitConverter.GetBytes(opcodeNum).CopyTo(RawMessage, 4);
            }
        }

        /// <summary>
        /// The "magic number" at the beginning of each of the packets, to denote that we care about it
        /// </summary>
        public const uint MAGIC = 0xf510ba2d;

        // current index when reading the message
        private int iteratorIndex = 0;

        public Message(MessageType messageType, IPAddress remoteAddress, byte[] rawMessage)
        {
            MessageType = messageType;
            RemoteAddress = remoteAddress;
            RawMessage = rawMessage;
            IsOutgoing = false;
        }

        public static Message CreateOutgoingMessage(MessageType type, IPAddress remoteAddress, Opcode opcode, byte[] data)
        {
            // append the opcode and magic number, lol
            int offset = 0;
            int extraLength = (type == MessageType.Direct)
                ? (sizeof(int) + sizeof(uint) + sizeof(short))
                : (sizeof(uint) + sizeof(short));
            byte[] newData = new byte[data.Length + extraLength];
            // if it's a direct message add the length at the beginning too
            if (type == MessageType.Direct)
            {
                BitConverter.GetBytes(newData.Length).CopyTo(newData, offset);
                offset += sizeof(int);
            }
            BitConverter.GetBytes(MAGIC).CopyTo(newData, offset);
            offset += sizeof(uint);
            BitConverter.GetBytes((ushort)opcode).CopyTo(newData, offset);
            offset += sizeof(ushort);
            Array.Copy(data, 0, newData, 6, data.Length);

            Message message = new Message(type, remoteAddress, newData);
            message.IsOutgoing = true;
            return message;
        }

        public void Reset()
        {
            iteratorIndex = 0;
        }

        public int Next(ref byte[] buffer, int count)
        {
            if (iteratorIndex + count > RawMessage.Length)
                count = RawMessage.Length - iteratorIndex + count;
            Array.Copy(RawMessage, iteratorIndex, buffer, 0, count);
            iteratorIndex += count;
            return count;
        }

        /// <summary>
        /// Reads the next string in the series of bytes, up to max number of bytes.
        /// </summary>
        /// <param name="max">The maximum length string to read</param>
        /// <returns></returns>
        public string NextString(int max)
        {
            byte[] buffer = new byte[max];
            int count = Next(ref buffer, max);
            return Encoding.ASCII.GetString(buffer, 0, count);
        }

        /// <summary>
        /// Reads the next string up until a null byte is reached.
        /// </summary>
        /// <returns></returns>
        public string NextString()
        {
            string str = "";
            while (RawMessage[iteratorIndex] > 0)
                str += (char)RawMessage[iteratorIndex++];
            return str;
        }

        public void Skip(int count)
        {
            iteratorIndex += count;
        }

        public bool HasValidHeader()
        {
            if (RawMessage.Length < 4)
                return false;
            uint magic = BitConverter.ToUInt32(RawMessage, 0);
            return magic == MAGIC;
        }
    }
}
