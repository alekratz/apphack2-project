using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace transf
{
    class FileSystem
    {
        public string BaseDirectory { get; private set; }

        private HashSet<string> files;

        public FileSystem(string baseDirectory)
        {
            baseDirectory = BaseDirectory;

        }
    }
}
