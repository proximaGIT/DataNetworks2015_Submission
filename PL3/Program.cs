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

            //please enter valid port number here, only for testing
            int programPort = 42015;
            if (args.Length > 0)
            {
                try
                {
                    programPort = int.Parse(args[0]);
                    if ((programPort > 65535) || (programPort < 1024))
                    {
                        programPort = 42015;
                        throw new Exception();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid port");
                    Console.WriteLine("Running with default port: 42015");
                }
            }


            //Console.WriteLine("Enter the port number of your server: ");
            //Int32 portnumber = 0;
            //try
            //{
            //    portnumber = Convert.ToInt32(Console.ReadLine()); 
            //}
            //catch (Exception excp)
            //{
            //    Console.WriteLine("Invalid Port");
            //}

            //Use this when you want port number to be always 42015
            //Int32 portnumber = 42015;
            Console.WriteLine("Running With Port: " + programPort);
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, programPort);
            WebSocketServer server = new WebSocketServer(ipEndPoint);

            //ConsoleWait.ConsoleConnect += server.OnConsoleConnect;
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            Task web_srvr_t = new Task(() => server.StartListening(token), token);
            web_srvr_t.Start();

            // Wait for the user to type the exit command
            string line;
            Console.WriteLine("Group " + GroupNumber.ToString(CultureInfo.InvariantCulture) + "'s dnChat server is running.");
            Console.WriteLine("Type \"exit\" to stop the server.");
            do
            {
                Console.Write("> ");
                line = Console.ReadLine();

                if (line.Split(' ')[0] == "connect")
                {
                    Console.WriteLine("Connecting to the Server");
                    server.ConnectToServer(line);
                }

            }
            while (line != "exit");

            tokenSource.Cancel();

            // Write code here to:
            // - Have the server stop listening for incoming connections.
            // - Cleanly close all current connections to clients.
            server.CloseAllConnection();
            Thread.Sleep(1000); //Give enough time to close all

        }
    }
}
