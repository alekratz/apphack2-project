using System;

namespace transf.Log
{
	/// <summary>
	/// Log level used by the <see cref="Logger"/>.
	/// </summary>
	internal enum LogLevel : int
	{
		Verbose = 1,
		Debug = 2,
		Info = 3,
		Warning = 4,
		Error = 5
	}
}

