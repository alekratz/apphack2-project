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

    }
}
