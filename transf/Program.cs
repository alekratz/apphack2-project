using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;

namespace transf
{
	class Program
	{
		/// <summary>
		/// Prompts for a nickname until a valid nickname is found.
		/// </summary>
		/// <returns>The valid nickname retrieved from user input</returns>
		public static string GetNickname()
		{
			string prompt = "Type a nickname (4-16 chars, alphanum only): ";
			string nickname = "";
			do
			{
				Console.Write (prompt);
				nickname = Console.ReadLine();
			} while(!Regex.IsMatch (nickname, "[a-zA-Z0-9]{4,16}"));
			return nickname;
		}

		public static void Main (string[] args)
		{
			const int PORT = 44444;
			string nickname = GetNickname ();

			Console.WriteLine ("Started listening for datagrams on port {0}", PORT);
			Console.WriteLine ("Nickname: {0}", nickname);

			DiscoveryWorker discWorker = new DiscoveryWorker ();
			discWorker.Start (PORT, nickname);
			discWorker.Join ();
		}
	}
}
