using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using transf.Log;
using transf.Utils;

namespace transf
{
    class FileSystem
    {
        /// <summary>
        /// The path to the filesystem, relative to the working directory.
        /// </summary>
        public string RelativeDirectory { get; private set; }
        /// <summary>
        /// The absolute path to the filesystem.
        /// </summary>
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
            // File was renamed, so remove it and add a new one
            if (files.RemoveWhere(f => f.AbsolutePath == e.OldFullPath) > 0)
            {
                FileEntry fEntry = new FileEntry(this, e.FullPath);
                files.Add(fEntry);
                Logger.WriteVerbose(Logger.GROUP_FS, "{0} renamed to {1}", e.OldName, e.Name);
            }
            else
            {
                Logger.WriteWarning(Logger.GROUP_FS, "Untracked file {0} renamed to {1}, ignoring", e.OldName, e.Name);
            }
        }

        void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // File was renamed, so remove it and add a new one
            var file = files.FirstOrDefault(f => f.AbsolutePath == e.FullPath);
            if (file != null)
            {
                try
                {
                    file.CalculateHash();
                    Logger.WriteVerbose(Logger.GROUP_FS, "{0} was modified, new hash {1}", e.Name, file.HashString);
                }
                catch(IOException)
                {
                    // TODO : rather than removing, put the exception in FileEntry.CaclulateHash() and set a 
                    // flag that shows that the hash is out of date
                    Logger.WriteError(Logger.GROUP_FS, "Could not recalculate the hash of {0}, removing file", e.Name);
                    files.RemoveWhere(f => f.AbsolutePath == e.FullPath);
                }
            }
            else
            {
                Logger.WriteWarning(Logger.GROUP_FS, "Untracked file {0} was modified, ignoring", e.Name);
            }
        }

        void fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            // File was removed, try to remove it from our registry
            if (files.RemoveWhere(f => f.AbsolutePath == e.FullPath) > 0)
                Logger.WriteVerbose(Logger.GROUP_FS, "Unregistered {0}", e.Name);
            else
                Logger.WriteWarning(Logger.GROUP_FS, "Untracked file {0} deleted, ignoring", e.Name);
        }

        void fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            // File was created, try to register it
            try
            {
                //Logger.WriteDebug(Logger.GROUP_FS, "Full path: {0}", e.FullPath);
                //Logger.WriteDebug(Logger.GROUP_FS, "Relative path: {0}", IOUtils.GetRelativePath(e.FullPath, AbsoluteDirectory));
                FileEntry fEntry = new FileEntry(this, e.FullPath);
                files.Add(fEntry);
                Logger.WriteVerbose(Logger.GROUP_FS, "Added: {0} {1}", fEntry.HashString, fEntry.RelativePath);
            }
            catch (FileNotFoundException)
            {
                Logger.WriteWarning(Logger.GROUP_FS, "Could not open {0}, omitting file", e.Name);
            }
        }

        /// <summary>
        /// Scans all of the files in the filesystem and gets their hashes.
        /// </summary>
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
                    Logger.WriteVerbose(Logger.GROUP_FS, "Added: {0} {1}", fEntry.HashString, fEntry.BaseName);
                }
                catch(FileNotFoundException)
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
