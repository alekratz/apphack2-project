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
        private FileSystemWatcher fileSystemWatcher;

        public FileSystem(string baseDirectory)
        {
            RelativeDirectory = baseDirectory;
            AbsoluteDirectory = Path.GetFullPath(baseDirectory);
            Logger.WriteDebug(Logger.GROUP_FS, "Created FileSystem with base directory {0}", RelativeDirectory);
            ScanDirectory();
            Logger.WriteDebug(Logger.GROUP_FS, "Registered {0} files", files.Count);

            // Create filesystemwatcher and register events
            fileSystemWatcher = new FileSystemWatcher(AbsoluteDirectory);
            fileSystemWatcher.Filter = "*";
            fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            fileSystemWatcher.Created += fileSystemWatcher_Created;
            fileSystemWatcher.Deleted += fileSystemWatcher_Deleted;
            fileSystemWatcher.Changed += fileSystemWatcher_Changed;
            fileSystemWatcher.Renamed += fileSystemWatcher_Renamed;
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        void fileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            Logger.WriteDebug(Logger.GROUP_FS, "File {0} renamed (full path {1})", e.Name, e.FullPath);
        }

        void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Logger.WriteDebug(Logger.GROUP_FS, "File {0} changed (full path {1})", e.Name, e.FullPath);
        }

        void fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Logger.WriteDebug(Logger.GROUP_FS, "File {0} deleted (full path {1})", e.Name, e.FullPath);
        }

        void fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            Logger.WriteDebug(Logger.GROUP_FS, "File {0} created (full path {1})", e.Name, e.FullPath);
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
