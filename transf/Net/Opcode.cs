using System;

namespace transf.Net
{
    /// <summary>
    /// A representation of an opcode
    /// </summary>
    enum Opcode : ushort
    {
        /// <summary>
        /// A null opcode that doesn't denote anything.
        /// </summary>
        None = 0,
        /// <summary>
        /// Denotes the message was a broadcast message announcing the existence of itself.
        /// </summary>
        Discovery = 1,
        /// <summary>
        /// Request for the listing of a directory.
        /// </summary>
        RequestDirectoryListing = 10,
        /// <summary>
        /// Denotes that a directory listing will follow.
        /// </summary>
        DirectoryListing = 11,
    }
}
