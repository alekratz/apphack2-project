using System;

namespace transf
{
    public interface IFileSystemEntry
    {
        string BaseName { get; }
        string RelativePath { get; }
        string AbsolutePath { get; }
    }
}