using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Murmur;

namespace transf
{
    class FileEntry
    {
        /// <summary>
        /// Gets the name of the file itself.
        /// </summary>
        public string BaseName { get; private set; }
        /// <summary>
        /// Gets the full path to the file relative to the filesystem it's a part of.
        /// </summary>
        public string RelativePath { get; private set; }
        /// <summary>
        /// Gets the absolute path to the file.
        /// </summary>
        public string AbsolutePath { get; private set; }
        /// <summary>
        /// Gets whether this file exists.
        /// </summary>
        public bool Exists { get { return File.Exists(AbsolutePath); } }

        public string HashString { get; private set; }

        public byte[] HashBytes { get; private set; }

        /// <summary>
        /// Creates a new FileEntry object with a specified file and filesystem as its root.
        /// </summary>
        /// <param name="fileSystem">The filesystem object that is the root of the file.</param>
        /// <param name="relativePath">The relative path to this file from the filesystem object.</param>
        public FileEntry(FileSystem fileSystem, string relativePath)
        {
            BaseName = Path.GetFileName(relativePath);
            RelativePath = relativePath;
            AbsolutePath = Path.GetFullPath(relativePath);
            CalculateHash();
        }

        /// <summary>
        /// Calculates the murmur3 hash for the given file.
        /// </summary>
        public void CalculateHash()
        {
            Murmur128 hasher = MurmurHash.Create128();

            FileStream fileStream = File.OpenRead(AbsolutePath);
            HashBytes = hasher.ComputeHash(fileStream);
            StringBuilder hex = new StringBuilder(HashBytes.Length * 2);
            foreach (byte b in HashBytes)
                hex.AppendFormat("{0:x2}", b);
            HashString = hex.ToString();
            fileStream.Close();
        }
    }
}
