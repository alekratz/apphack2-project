using System;
using System.Net;
using System.IO;
using transf.FileSystem;
using transf.Utils;
using transf.Log;

namespace transf.Net
{
    class RemoteFileEntry
        : IFileSystemEntry
    {
        public string BaseName { get { return Path.GetFileName(RelativePath); } }

        public string RelativePath { get; private set; }

        public string AbsolutePath { get; private set; }

        public string HashString { get; private set; }

        public Node Node { get; private set; }

        public IPAddress RemoteAddress { get { return Node.RemoteAddress; } }

        public RemoteFileEntry(Node node, string relativePath, string absolutePath, string hashString)
        {
            Node = node;
            RelativePath = relativePath;
            AbsolutePath = absolutePath;
            HashString = hashString;
        }

    }
}

