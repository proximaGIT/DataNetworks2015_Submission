using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/**
 * Group 7
 * Muhammad Lathif Pambudi <s8mupamb@stud.uni-saarland.de> (2556858)
 * Praharsha Sirsi <s8prsirs@stud.uni-saarland.de> (2557724)
**/

namespace ChatServerTemplate
{
	class Program
	{
		const int GroupNumber = 7; // replace with your group number       

		static void Main(string[] args)
		{
            
            // Write code here to:
			// - Create a WebSocketServer instance on the dnChat default port.
			// - Subscribe to the server's Connected event to handle new clients.
			// - Let the server start listening for incoming connections.
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 42015);
            WebSocketServer server = new WebSocketServer(ipEndPoint);            
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            Task web_srvr_t = new Task(() => server.StartListening(token), token);
            web_srvr_t.Start();
           
            
			// Wait for the user to type the exit command
			string line;
			Console.WriteLine("Group " + GroupNumber.ToString(CultureInfo.InvariantCulture) + "'s dnChat server is running.");
			Console.WriteLine("Type \"exit\" to stop the server.");
			do { Console.Write("> "); }
			while((line = Console.ReadLine()) != "exit");
            tokenSource.Cancel();
            
			// Write code here to:
			// - Have the server stop listening for incoming connections.
			// - Cleanly close all current connections to clients.
            server.CloseAllConnection();
		}
	}
}
