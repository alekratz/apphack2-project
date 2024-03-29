﻿using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;
using transf.Log;
using transf.Net;
using transf.FileSystem;

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
			// This should be the first thing that's done
			Logger.Instance = new Logger (Console.Out);
			Logger.Instance.LogLevel = LogLevel.Verbose; // up the verbosity
            //Logger.Instance.EnabledGroups.Remove(Logger.GROUP_NET);

			const int PORT = 44444;
			string nickname = GetNickname ();
			Logger.WriteDebug (Logger.GROUP_APP, "Using nickname {0}", nickname);

            DirectoryEntry fSystem = new DirectoryEntry("../.."); // make this more neutral
            // Start a discovery worker and message worker
            MessageWorker msgWorker = MessageWorker.Instance;
            DiscoveryWorker discWorker = DiscoveryWorker.Instance;
            DirectoryDiscoveryWorker dirDiscWorker = new DirectoryDiscoveryWorker(fSystem);
            if (!msgWorker.Start(PORT))
            {
                Logger.WriteError(Logger.GROUP_APP, "Couldn't start message worker, exiting");
                return;
            }
            if (!discWorker.Start(nickname))
            {
                Logger.WriteError(Logger.GROUP_APP, "Couldn't start discovery worker, exiting");
                msgWorker.Stop();
                return;
            }
            if (!dirDiscWorker.Start())
            {
                Logger.WriteError(Logger.GROUP_APP, "Couldn't start directory discovery worker, exiting");
                discWorker.Stop();
                msgWorker.Stop();
                return;
            }
            Console.WriteLine("Found {0} files in the filesystem", fSystem.TreeFileCount);
            dirDiscWorker.Join();
		}
	}
}
