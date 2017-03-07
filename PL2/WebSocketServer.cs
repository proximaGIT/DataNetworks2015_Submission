using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/**
 * Group 7
 * Muhammad Lathif Pambudi <s8mupamb@stud.uni-saarland.de> (2556858)
 * Praharsha Sirsi <s8prsirs@stud.uni-saarland.de> (2557724)
**/

namespace ChatServerTemplate
{
   
    sealed class WebSocketServer
    {
        // Raise this event whenever a client is connected and has completed the WebSocket handshake
        // to allow other parts of the program to subscribe to the WebSocketClient's events.
        public event WebSocketClientEventHandler Connected = delegate { };

        public List<WebSocketClient> client_pool = new List<WebSocketClient>();
        public static List<MessagePool> message_pool = new List<MessagePool>();

        private readonly IPEndPoint ipEndPoint;

        public WebSocketServer(IPEndPoint ipEndPoint)
        {
            if (ipEndPoint == null) throw new ArgumentNullException("ipEndPoint");
            this.ipEndPoint = ipEndPoint;
        }

        public void StartListening(CancellationToken token)
        {
            // Start listening for incoming TCP connections on ipEndPoint and handle the connections.
            var tcp_listener = new TcpListener(ipEndPoint.Address, ipEndPoint.Port);
            tcp_listener.Start(3); //Limit new connections by using backlog of 3
            //System.Threading.Tasks.Task<TcpClient> tcp_client = tcp_listener.AcceptTcpClientAsync();
            while (true)
            {
                TcpClient tcp_client = tcp_listener.AcceptTcpClient();
                if (tcp_client.Connected)
                {
                    HandleClient(tcp_client);
                }
                if (token.IsCancellationRequested && client_pool.Count == 0) {
                    StopListening(tcp_client, tcp_listener);
                    return;
                }
            }

        }

        public void StopListening(TcpClient tcp_client, TcpListener tcp_listener)
        {
            tcp_client.Close();
            tcp_listener.Stop();
            return;
            // Stop listening for incoming TCP connections.

        }

        private void HandleClient(TcpClient tcp_client/* your arguments here */)
		{
            // Call this method, e.g. from StartListening, to handle a new TCP connection:
			// - Perform the WebSocket handshake.
			// - If successful, create an instance of WebSocketClient for the connection, raise
			//   the Connected event, and then let the client instance handle incoming messages
			//   (by calling the HandleMessages method).
			// It may be a good idea at some point to run this method on a new thread (or a
			// ThreadPool thread) for each new connection to allow multiple simultaneous clients.

            // user can only connects using Group7's password
            String password = "YD4G2gs7";

            NetworkStream stream = tcp_client.GetStream();

            // To make sure the stream is available
            // (in case the stream is still reading from the network)
            Thread.Sleep(1); 

            if (stream.DataAvailable)
            {
                Byte[] read_bytestream = new Byte[tcp_client.Available];
                stream.Read(read_bytestream, 0, read_bytestream.Length);
                String read_string = Encoding.UTF8.GetString(read_bytestream);
                //Console.WriteLine(read_string);

                if (new Regex("^GET").IsMatch(read_string))
                {
                    if (new Regex("Sec-WebSocket-Version: (.*)").Match(read_string).Groups[1].Value.Trim() == "13")
                    {
                        // reply to hanshake
                        Byte[] send_string = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                                                                    + "Upgrade: websocket" + Environment.NewLine
                                                                    + "Connection: Upgrade" + Environment.NewLine
                                                                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(new Regex("Sec-WebSocket-Key: (.*)").Match(read_string).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))) + Environment.NewLine
                                                                    + Environment.NewLine);
                        stream.Write(send_string, 0, send_string.Length);
                        //end reply

                        ClientData cp = new ClientData(tcp_client, null, States.CONNECTED, stream, null, password);
                        WebSocketClient wsc = new WebSocketClient(cp, this);
                        client_pool.Add(wsc);
                        var tokenSource = new CancellationTokenSource();
                        var token = tokenSource.Token;
                        // handle incoming messages from client in a separate thread
                        Task client_exchange = new Task(() => wsc.HandleMessages(), token);
                        client_exchange.Start();
                    }
                    else
                    {
                        Byte[] send_string = Encoding.UTF8.GetBytes("HTTP/1.1 426 Upgrade Required" + Environment.NewLine
                                                                    + "Sec-WebSocket-Version: 13" + Environment.NewLine
                                                                    + Environment.NewLine);
                        stream.Write(send_string, 0, send_string.Length);
                    }

                }
                else
                {
                    Byte[] send_string = Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request" + Environment.NewLine
                                                                 + Environment.NewLine);
                    stream.Write(send_string, 0, send_string.Length);
                }
            }           
            
		}

        public void NotifyClientLeft(WebSocketClient client)
        {
            // tell other client that user has left
            ClientData clientData = client.clientData;
            foreach (WebSocketClient wsc in client_pool)
            {
                ClientData temp_client = wsc.clientData;
                if ((String.Compare(temp_client.id, clientData.id) != 0) && (temp_client.client_state == States.AUTHENTICATED) && (clientData.client_state == States.AUTHENTICATED))
                {
                    String send_string = ("LEFT " + clientData.id + Environment.NewLine
                                            + Environment.NewLine);
                    wsc.SendTextMessage(send_string);
                }
            }
        }

        public void NotifyClientArrive(WebSocketClient new_client)
        {
            // tell other client that there is a new user logged in
            ClientData active_client = new_client.clientData;
            foreach (WebSocketClient temp_client in client_pool)
            {
                if (String.Compare(active_client.id, temp_client.clientData.id) != 0)
                {
                    string send_string = ("ARRV " + active_client.id + Environment.NewLine
                            + active_client.username + Environment.NewLine
                            + "Group 7" + Environment.NewLine
                            + Environment.NewLine);
                    temp_client.SendTextMessage(send_string);

                    // also send arrv to the new user
                    if (temp_client.clientData.client_state == States.AUTHENTICATED)
                    {
                        send_string = ("ARRV " + temp_client.clientData.id + Environment.NewLine
                            + temp_client.clientData.username + Environment.NewLine
                            + "Group 7" + Environment.NewLine
                            + Environment.NewLine);
                        new_client.SendTextMessage(send_string);
                    }

                }
            }
        }

        public void SendClientMessage(string message, string destClientId)
        {
            // send a text message to a client with id = destClientId
            foreach (WebSocketClient wsc in client_pool)
            {
                ClientData temp_client = wsc.clientData;
                if (String.Compare(temp_client.id, destClientId) == 0)
                {
                    wsc.SendTextMessage(message);
                    return;
                }
            }
        }

        public void BroadcastClientMessage(string message, WebSocketClient source)
        {
            // broadcast a message to all client except the source
            foreach (WebSocketClient wsc in client_pool)
            {
                ClientData temp_client = wsc.clientData;
                if (String.Compare(temp_client.id, source.clientData.id) != 0)
                {
                    wsc.SendTextMessage(message);
                }
            }
        }

        public void AckMessage(string messageId, WebSocketClient fromClient)
        {
            // fromClient send an ack to the message owner
            foreach (MessagePool temp_msg in WebSocketServer.message_pool)
            {
                if (String.Compare(temp_msg.msg_id, messageId) == 0)
                {
                    String tmp_user_id = temp_msg.user_id;
                    foreach (WebSocketClient wsc in client_pool)
                    {
                        ClientData temp_client = wsc.clientData;
                        if ((String.Compare(temp_client.id, tmp_user_id) == 0) && (temp_client.client_state == States.AUTHENTICATED))
                        {
                            string send_string = ("ACKN " + messageId + Environment.NewLine
                                        + fromClient.clientData.id + Environment.NewLine
                                        + Environment.NewLine);
                            wsc.SendTextMessage(send_string);
                        }
                    }
                }
            }
        }

        internal void ClientDisconnectedHandler(object source, WebSocketClientEventArgs e)
        {
            //remove the client from the pool
            client_pool.Remove(e.Client);
            if (e.Client.clientData.client_state == States.AUTHENTICATED)
            {
                // in case the client didnt log out
                NotifyClientLeft(e.Client);
            }
            e.Client.clientData.client_state = States.DISCONNECTED;
        }

        // check whether the username and user id already exist
        public void CheckAuthintecation(string userid, string username, out bool same_userid_flag, out bool same_username)
        {
            same_userid_flag = false;
            same_username = false;
            foreach (WebSocketClient temp_client in client_pool)
            {
                if (String.Compare(temp_client.clientData.id, userid) == 0)
                {
                    same_userid_flag = true;
                }
                if (String.Compare(temp_client.clientData.username, username) == 0)
                {
                    same_username = true;
                }
            }
        }

        public void CloseAllConnection() {
            foreach (WebSocketClient client in client_pool)
            {
                // kill all client tasks, send close frame, and close the sockets
                client.clientData.client_state = States.DISCONNECTED;
            }
        }
    }
}