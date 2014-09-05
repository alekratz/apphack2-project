using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using transf.Log;

namespace transf
{
    class FileSystem
    {
        public string RelativeDirectory { get; private set; }
        public string AbsoluteDirectory { get; private set; }

        private HashSet<FileEntry> files = new HashSet<FileEntry>();

        public FileSystem(string baseDirectory)
        {
            RelativeDirectory = baseDirectory;
            AbsoluteDirectory = Path.GetFullPath(baseDirectory);
            Logger.WriteDebug(Logger.GROUP_FS, "Created FileSystem with base directory {0}", RelativeDirectory);
            ScanDirectory();
            Logger.WriteDebug(Logger.GROUP_FS, "Registered {0} files", files.Count);
        }

        public void ScanDirectory()
        {
            files.Clear();
            Dive(RelativeDirectory);
        }

        /// <summary>
        /// Recursively scans the filesystem
        /// </summary>
        private void Dive(string root)
        {
            // Go through files
            string[] strFiles = Directory.GetFiles(root);
            foreach (string file in strFiles)
            {
                try
                {
                    FileEntry fEntry = new FileEntry(this, file);
                    files.Add(fEntry);
                    Logger.WriteVerbose(Logger.GROUP_FS, "Added: {0} {1}", fEntry.HashString, fEntry.RelativePath);
                }
                catch
                {
                    Logger.WriteWarning(Logger.GROUP_FS, "Could not open {0}, omitting file", file);
                }
            }

            // Go through directories
            string[] strDirectories = Directory.GetDirectories(root);
            foreach (string directory in strDirectories)
            {
                Dive(directory);
            }
        }
    }
}
