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

        public class HopStore
        {
            public TcpClient TcpConn;
            public String ClientId;
            public String ClientName;
            public String ClientDesc;
            public Int32 HopCnt;
        }

        public List<HopStore> hoplist = new List<HopStore>();


        Random rndm_gen = new Random();

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
                Thread.Sleep(1); //Always use sleep to prevent locking
                TcpClient tcp_client = tcp_listener.AcceptTcpClient();
                if (tcp_client.Connected)
                {
                    HandleClient(tcp_client);
                }
                if (token.IsCancellationRequested && client_pool.Count == 0)
                {
                    StopListening(tcp_client, tcp_listener);
                    return;
                }
            }

        }

        public void ConnectToServer(String line)
        {
            char[] splitspace = new char[] { ' ', '\n', '\0' };
            String[] splitinput = line.Split(splitspace);
            IPAddress ipaddress;
            if (splitinput.Length > 1)
            {
                try
                {
                    //Check only ipv4 adresses or host names having ipv4 adresses
                    IPAddress[] ipv4Addresses = Array.FindAll(Dns.GetHostEntry(splitinput[1]).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
                    ipaddress = ipv4Addresses[0];
                    int port = 42015;
                    if (splitinput.Length > 2)
                    {
                        try
                        {
                            port = Convert.ToInt32(splitinput[2]); //Overwrite port number if valid
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("{0} Invalid Port", e);
                        }
                    }

                    try
                    {
                        IPEndPoint ipendpoint_srvr = new IPEndPoint(ipaddress, port);
                        Console.WriteLine("{0} ", ipendpoint_srvr);                        
                        TcpClient tcp_client_srvr = new TcpClient();                        
                        tcp_client_srvr.Connect(ipendpoint_srvr); //Create the connection and connect
                        HandleServer(tcp_client_srvr, ipendpoint_srvr);                                    
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Cannot Establish Connection");
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid Connection");
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

        public void HandleServer(TcpClient tcp_client, IPEndPoint ipendpoint)
        {
            try
            {
                NetworkStream stream = tcp_client.GetStream();
                Byte[] webkey = new Byte[16];
                rndm_gen.NextBytes(webkey);

                //Send handshake
                Byte[] send_string = Encoding.UTF8.GetBytes("GET ws://" + ipendpoint.ToString() + "/ HTTP/1.1" + "\r\n"
                                                                        + "Host: " + ipendpoint.ToString() + "\r\n"
                                                                        + "Upgrade: websocket" + "\r\n"
                                                                        + "Connection: Upgrade" + "\r\n"
                                                                        + "Sec-Websocket-Key: " + Convert.ToBase64String(webkey) + "\r\n"
                                                                        + "Sec-WebSocket-Version: 13" + "\r\n"
                                                                        + "\r\n");
                
                stream.Write(send_string, 0, send_string.Length); //Check firewall settings, possible blocking by firewall
                stream.Flush();

                String Sec_Websocket_accept = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(Convert.ToBase64String(webkey) + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                Thread.Sleep(1);
                String read_string = null;
                do
                {
                    Byte[] read_bytestream = new Byte[tcp_client.Available];
                    stream.Read(read_bytestream, 0, read_bytestream.Length);
                    read_string = Encoding.UTF8.GetString(read_bytestream);
                    //Console.WriteLine(read_string);
                } while (tcp_client.Available > 1);

                //Check Received Handshake
                if (read_string != null)
                {
                    if ((new Regex("HTTP/1.1 (.*)").Match(read_string).Groups[1].Value.Trim() == "101 Switching Protocols")
                        & (new Regex("Upgrade: (.*)").Match(read_string).Groups[1].Value.Trim() == "websocket")
                        & (new Regex("Connection: (.*)").Match(read_string).Groups[1].Value.Trim() == "Upgrade")
                        & !(new Regex("Sec-Websocket-Extensions: ").IsMatch(read_string))
                        & !(new Regex("Sec-Websocket-Protocol: ").IsMatch(read_string)))
                    {
                        int rndm_key = rndm_gen.Next(1, 536870912); //any random number between 1 and 2^29

                        ClientData cp = new ClientData(tcp_client, Convert.ToString(rndm_key), States.CONNECTED, stream, null, null, ConnectionType.SERVERCLIENT);
                        WebSocketClient wsc = new WebSocketClient(cp, this);
                        client_pool.Add(wsc);
                        var tokenSource = new CancellationTokenSource();
                        var token = tokenSource.Token;
                        // handle incoming messages from client in a separate thread
                        Task client_exchange = new Task(() => wsc.HandleMessages(), token);
                        client_exchange.Start();

                        //Console.WriteLine("Connected to the Server with ID = " + rndm_key);
                        Console.WriteLine("Connected to the Server");

                    }
                    else
                    {
                        Console.WriteLine("Cannot connect to the Server");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot connect to the Server");
            }
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
            Thread.Sleep(100); //added extra delay for new connections
            String read_string = null;
            if (stream.DataAvailable)
            {
                Byte[] read_bytestream = new Byte[tcp_client.Available];
                stream.Read(read_bytestream, 0, read_bytestream.Length);
                read_string = Encoding.UTF8.GetString(read_bytestream);
                //Console.WriteLine(read_string);
            }


            if (read_string != null)
            {
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
                        stream.Flush();
                        //end reply
                        ClientData cp = null;

                        if (new Regex("Origin:").IsMatch(read_string)) 
                        {
                            cp = new ClientData(tcp_client, null, States.CONNECTED, stream, null, password, ConnectionType.CLIENT);
                        }
                        else //Server connections wont have origin field as defined in RFC
                        {
                            int rndm_key = rndm_gen.Next(1, 536870912);
                            cp = new ClientData(tcp_client, Convert.ToString(rndm_key), States.CONNECTED, stream, null, password, ConnectionType.SERVERSERVER);
                        }

                        if (cp != null)
                        {
                            WebSocketClient wsc = new WebSocketClient(cp, this);
                            client_pool.Add(wsc);
                            var tokenSource = new CancellationTokenSource();
                            var token = tokenSource.Token;
                            // handle incoming messages from client in a separate thread
                            Task client_exchange = new Task(() => wsc.HandleMessages(), token);
                            client_exchange.Start();
                        }
                    }
                    else
                    {
                        Byte[] send_string = Encoding.UTF8.GetBytes("HTTP/1.1 426 Upgrade Required" + Environment.NewLine
                                                                    + "Sec-WebSocket-Version: 13" + Environment.NewLine
                                                                    + Environment.NewLine);
                        stream.Write(send_string, 0, send_string.Length);
                        stream.Flush();
                    }

                }
                else
                {
                    Byte[] send_string = Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request" + Environment.NewLine
                                                                 + Environment.NewLine);
                    stream.Write(send_string, 0, send_string.Length);
                    stream.Flush();
                }
            }

        }

        public void NotifyClientLeft(WebSocketClient client)
        {
            // tell other client that user has left          
            ClientData clientData = client.clientData;
            if (client.clientData.ConnectType == ConnectionType.CLIENT)
            {
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
            else //handle server disconnections
            {
                List<HopStore> temp_hoplist = hoplist;

                for (int i = hoplist.Count - 1; i >= 0; i--) //iterating in reverse for lists

                // foreach (HopStore ClientHop in temp_hoplist)
                {
                    if (client.clientData.socket.Equals(hoplist[i].TcpConn) && hoplist[i].HopCnt >= 1) //remove all clients connected through the disconnected server
                    {
                        foreach (WebSocketClient wsc in client_pool)
                        {
                            ClientData temp_client = wsc.clientData;
                            if ((String.Compare(temp_client.id, clientData.id) != 0) && (temp_client.client_state == States.AUTHENTICATED) && (clientData.client_state == States.AUTHENTICATED))
                            {
                                String send_string = ("LEFT " + hoplist[i].ClientId + Environment.NewLine
                                               + Environment.NewLine);
                                wsc.SendTextMessage(send_string);
                            }
                        }
                        hoplist.RemoveAt(i);
                    }
                }
            }
        }

        public void NotifyClientArrive(WebSocketClient new_client)
        {
            
            ClientData active_client = new_client.clientData;
            //tell new user all the hopstore connections
            foreach (HopStore ClientHop in hoplist)
            {
                string send_string = ("ARRV " + ClientHop.ClientId + Environment.NewLine
                            + ClientHop.ClientName + Environment.NewLine
                            + ClientHop.ClientDesc + Environment.NewLine
                            + Environment.NewLine);
                new_client.SendTextMessage(send_string);
            }
            HopStore newConn = new HopStore() { TcpConn = active_client.socket, ClientId = active_client.id, ClientName = active_client.username, ClientDesc = "Group 7", HopCnt = 0 };
            hoplist.Add(newConn);


            foreach (WebSocketClient temp_client in client_pool)
            {
                // tell other client that there is a new user logged in
                if ((String.Compare(active_client.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType == ConnectionType.CLIENT))
                {
                    string send_string = ("ARRV " + active_client.id + Environment.NewLine
                            + active_client.username + Environment.NewLine
                            + "Group 7" + Environment.NewLine
                            + Environment.NewLine);
                    temp_client.SendTextMessage(send_string);
                }
                    //tell other servers that there is a new user logged in
                else if ((String.Compare(active_client.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType == ConnectionType.SERVERCLIENT) | (temp_client.clientData.ConnectType == ConnectionType.SERVERSERVER)) //added for PL3
                {
                    string send_string = ("ARRV " + active_client.id + Environment.NewLine
                            + active_client.username + Environment.NewLine
                            + "Group 7" + Environment.NewLine
                            + "0" + "\r\n"
                            + Environment.NewLine);
                    temp_client.SendTextMessage(send_string);
                }
            }
        }

        public void SendClientMessage(String MsgID, String Destination, String UserID, String message, WebSocketClient source)
        {
            if (hoplist.Exists(OtherIDs => OtherIDs.ClientId == UserID) && hoplist.Exists(destID => destID.ClientId == Destination))
            {
                HopStore forClient = hoplist.Single(forID => forID.ClientId == Destination);
                //local user exists
                if (forClient.HopCnt == 0)
                {
                    foreach (WebSocketClient temp_client in client_pool)
                    {
                        if ((String.Compare(temp_client.clientData.id, forClient.ClientId) == 0) && (temp_client.clientData.ConnectType == ConnectionType.CLIENT) && (temp_client.clientData.client_state == States.AUTHENTICATED))
                        {
                            String send_string = ("SEND " + MsgID + Environment.NewLine
                                                  + UserID + Environment.NewLine
                                                  + message + Environment.NewLine
                                                  + Environment.NewLine);
                            temp_client.SendTextMessage(send_string);
                        }
                    }
                }

                //user connected elsewhere
                else
                {
                    foreach (WebSocketClient temp_client in client_pool)
                    {
                        if ((temp_client.clientData.socket.Equals(forClient.TcpConn)) && (temp_client.clientData.ConnectType != ConnectionType.CLIENT) && (temp_client.clientData.client_state == States.AUTHENTICATED))
                        {
                            String send_string = ("SEND " + MsgID + Environment.NewLine
                                            + Destination + Environment.NewLine
                                            + UserID + Environment.NewLine
                                            + message + Environment.NewLine
                                            + Environment.NewLine);
                            temp_client.SendTextMessage(send_string);
                        }
                    }
                }
            }
        }

        public void BroadcastClientMessage(String MsgID, String Destination, String UserID, String message, WebSocketClient source)
        {
            // broadcast a message to all client except the source
            foreach (WebSocketClient wsc in client_pool)
            {
                ClientData temp_client = wsc.clientData;
                //Send to all local clients
                if ((String.Compare(temp_client.id, source.clientData.id) != 0) && (temp_client.client_state == States.AUTHENTICATED) && (temp_client.ConnectType == ConnectionType.CLIENT))
                {
                    String send_string = ("SEND " + MsgID + Environment.NewLine
                                          + UserID + Environment.NewLine
                                          + message + Environment.NewLine
                                          + Environment.NewLine);
                    wsc.SendTextMessage(send_string);
                }

                    //forward to servers
                else if ((String.Compare(source.clientData.id, temp_client.id) != 0) & (temp_client.ConnectType != ConnectionType.CLIENT) && (temp_client.client_state == States.AUTHENTICATED))
                {
                    String send_string = ("SEND " + MsgID + Environment.NewLine
                                        + Destination + Environment.NewLine
                                        + UserID + Environment.NewLine
                                        + message + Environment.NewLine
                                        + Environment.NewLine);
                    wsc.SendTextMessage(send_string);
                }
            }
        }

        public void AckMessage(string messageId, WebSocketClient fromClient)
        {
            // fromClient send an ack to the message owner
            if (message_pool.Exists(MsgId => MsgId.msg_id == messageId))
            {
                MessagePool MsgfromClient = message_pool.Single(MsgId => MsgId.msg_id == messageId);
                //check local pool for the client of the message
                if (client_pool.Exists(UserId => UserId.clientData.id == MsgfromClient.user_id))
                {
                    WebSocketClient toUser = client_pool.Single(UserId => UserId.clientData.id == MsgfromClient.user_id);
                    //Send to local client
                    if (toUser.clientData.client_state == States.AUTHENTICATED && toUser.clientData.ConnectType == ConnectionType.CLIENT)
                    {
                        string send_string = ("ACKN " + messageId + Environment.NewLine
                                       + fromClient.clientData.id + Environment.NewLine
                                       + Environment.NewLine);
                        toUser.SendTextMessage(send_string);
                    }
                }

                //check hopstore for the client
                else if (hoplist.Exists(UserId => UserId.ClientId == MsgfromClient.user_id))
                {
                    HopStore HopToUser = hoplist.Single(UserId => UserId.ClientId == MsgfromClient.user_id);
                    WebSocketClient toUser = client_pool.Single(UserTCP => UserTCP.clientData.socket.Equals(HopToUser.TcpConn));
                    //Send to the server along the path
                    if (toUser.clientData.client_state == States.AUTHENTICATED && toUser.clientData.ConnectType != ConnectionType.CLIENT)
                    {
                        string send_string = ("ACKN " + messageId + Environment.NewLine
                                        + fromClient.clientData.id + Environment.NewLine
                                        + MsgfromClient.user_id + Environment.NewLine
                                        + Environment.NewLine);
                        toUser.SendTextMessage(send_string);
                    }
                }
            }            
        }

        internal void ClientDisconnectedHandler(object source, WebSocketClientEventArgs e)
        {
            //remove the client from the pool 
            Int32 index_num = client_pool.IndexOf(e.Client);
            if (index_num >= 0)
            {
                client_pool.RemoveAt(index_num);
            }

            //check against server disconnections
            if (e.Client.clientData != null)
            {
                try
                {
                    if (e.Client.clientData.ConnectType == ConnectionType.CLIENT)
                    {
                        HopStore RemoveLocalClient = hoplist.Find(x => x.ClientId == e.Client.clientData.id);
                        hoplist.Remove(RemoveLocalClient);
                    }

                    if (e.Client.clientData.client_state == States.AUTHENTICATED)
                    {
                        // in case the client didnt log out
                        NotifyClientLeft(e.Client);
                    }

                    e.Client.clientData.client_state = States.DISCONNECTED;
                }
                catch (Exception excp)
                {
                    //Console.WriteLine(excp.ToString());
                }
            }
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

        public void CloseAllConnection()
        {
            foreach (WebSocketClient client in client_pool)
            {
                // kill all client tasks, send close frame, and close the sockets
                client.clientData.client_state = States.DISCONNECTED;
            }
        }

        public void ClientArriveFromServer(String id, String username, String description, Int32 HopCnt, WebSocketClient fromClient)
        {
            HopStore newClient = new HopStore() { TcpConn = fromClient.clientData.socket, ClientId = id, ClientName = username, ClientDesc = description, HopCnt = (HopCnt + 1) };
            Int32 indexNum = -1;
            try
            {
                HopStore checkClient = hoplist.Single(UserID => UserID.ClientId == id);
                indexNum = hoplist.IndexOf(checkClient);
            }
            catch { }

            if (newClient.HopCnt < 16)
            {
                if (!hoplist.Exists(UserID => UserID.ClientId == id)) //if the id is not in the list
                {
                    //add new client to the list                    
                    hoplist.Add(newClient);

                    foreach (WebSocketClient temp_client in client_pool)
                    {
                        //update the clients
                        if ((String.Compare(fromClient.clientData.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType == ConnectionType.CLIENT))
                        {
                            string send_string = ("ARRV " + id + Environment.NewLine
                                    + username + Environment.NewLine
                                    + description + Environment.NewLine
                                    + Environment.NewLine);
                            temp_client.SendTextMessage(send_string);
                        }

                        //update the servers
                        else if ((String.Compare(fromClient.clientData.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType != ConnectionType.CLIENT))
                        {
                            HopCnt = HopCnt + 1;
                            string send_string = ("ARRV " + id + Environment.NewLine
                                    + username + Environment.NewLine
                                    + description + Environment.NewLine
                                    + HopCnt + Environment.NewLine
                                    + Environment.NewLine);
                            temp_client.SendTextMessage(send_string);
                        }
                    }
                }
                else if (indexNum >= 0)
                {
                    if (newClient.HopCnt < hoplist[indexNum].HopCnt) //if id is in the list compare hop count
                    {
                        HopStore removeClient = hoplist.Single(removeID => removeID.ClientId == hoplist[indexNum].ClientId);
                        hoplist.Remove(removeClient);
                        //replace the client with new hop value                                       
                        hoplist.Add(newClient);

                        foreach (WebSocketClient temp_client in client_pool)
                        {
                            //update only servers with new hop value
                            if ((String.Compare(fromClient.clientData.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType != ConnectionType.CLIENT))
                            {
                                HopCnt = HopCnt + 1;
                                string send_string = ("ARRV " + id + Environment.NewLine
                                        + username + Environment.NewLine
                                        + description + Environment.NewLine
                                        + HopCnt + Environment.NewLine
                                        + Environment.NewLine);
                                temp_client.SendTextMessage(send_string);
                            }
                        }
                    }
                }
            }
            else //if the hop count is greater than or equal to 16
            {
                ClientLeftFromServer(id, fromClient);
            }
        }

        public void NewServerConnection(WebSocketClient newServer)
        {
            //Send existing clients to the new server
            foreach (HopStore ClientHop in hoplist)
            {
                string send_string = ("ARRV " + ClientHop.ClientId + Environment.NewLine
                                    + ClientHop.ClientName + Environment.NewLine
                                    + ClientHop.ClientDesc + Environment.NewLine
                                    + ClientHop.HopCnt + Environment.NewLine
                                    + Environment.NewLine);
                newServer.SendTextMessage(send_string);
            }
        }

        public void ClientLeftFromServer(String id, WebSocketClient fromServer)
        {
            if (hoplist.Exists(UserID => UserID.ClientId == id))
            {
                foreach (WebSocketClient temp_client in client_pool)
                {
                    //update the clients
                    if ((String.Compare(fromServer.clientData.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType == ConnectionType.CLIENT))
                    {
                        string send_string = ("LEFT " + id + Environment.NewLine
                                + Environment.NewLine);
                        temp_client.SendTextMessage(send_string);
                    }

                    //update the servers
                    else if ((String.Compare(fromServer.clientData.id, temp_client.clientData.id) != 0) & (temp_client.clientData.ConnectType != ConnectionType.CLIENT))
                    {
                        string send_string = ("LEFT " + id + Environment.NewLine
                                + Environment.NewLine);
                        temp_client.SendTextMessage(send_string);
                    }
                }

                //Guard against cyclic topology and server disconnects
                try
                {
                    HopStore removeClient = hoplist.Single(removeID => removeID.ClientId == id);
                    hoplist.Remove(removeClient);
                }
                catch (Exception excp)
                {
                    //Console.WriteLine(" ");
                    //Console.WriteLine("Hard Disconnect: " + excp.ToString());
                }
            }
        }

        public void BroadcastMessageFromServer(String msgid, String destination, String userid, String message, WebSocketClient fromServer)
        {
            // broadcast a message 
            if (hoplist.Exists(UserID => UserID.ClientId == userid))
            {
                foreach (WebSocketClient wsc in client_pool)
                {
                    ClientData temp_client = wsc.clientData;
                    //Send message to local clients
                    if ((String.Compare(temp_client.id, fromServer.clientData.id) != 0) && (temp_client.ConnectType == ConnectionType.CLIENT) && (temp_client.client_state == States.AUTHENTICATED))
                    {
                        String send_string = ("SEND " + msgid + Environment.NewLine
                                          + userid + Environment.NewLine
                                          + message + Environment.NewLine
                                          + Environment.NewLine);
                        wsc.SendTextMessage(send_string);
                    }

                    //Forward to other servers
                    else if ((String.Compare(fromServer.clientData.id, temp_client.id) != 0) & (temp_client.ConnectType != ConnectionType.CLIENT) && (temp_client.client_state == States.AUTHENTICATED))
                    {
                        String send_string = ("SEND " + msgid + Environment.NewLine
                                            + destination + Environment.NewLine
                                            + userid + Environment.NewLine
                                            + message + Environment.NewLine
                                            + Environment.NewLine);
                        wsc.SendTextMessage(send_string);
                    }
                }
            }
        }

        public void SendMessageFromServer(String msgid, String destination, String userid, String message, WebSocketClient fromServer)
        {
            if (hoplist.Exists(UserID => UserID.ClientId == userid) && hoplist.Exists(destID => destID.ClientId == destination))
            {
                HopStore forClient = hoplist.Single(forID => forID.ClientId == destination);
                //Check if client is locally available
                if (forClient.HopCnt == 0)
                {
                    foreach (WebSocketClient temp_client in client_pool)
                    {
                        if ((String.Compare(temp_client.clientData.id, forClient.ClientId) == 0) && (temp_client.clientData.ConnectType == ConnectionType.CLIENT) && (temp_client.clientData.client_state == States.AUTHENTICATED))
                        {
                            String send_string = ("SEND " + msgid + Environment.NewLine
                                                  + userid + Environment.NewLine
                                                  + message + Environment.NewLine
                                                  + Environment.NewLine);
                            temp_client.SendTextMessage(send_string);
                        }
                    }
                }
                else
                {
                    foreach (WebSocketClient temp_client in client_pool)
                    {
                        //Send the message along the path
                        if ((temp_client.clientData.socket.Equals(forClient.TcpConn)) && (temp_client.clientData.ConnectType != ConnectionType.CLIENT) && (temp_client.clientData.client_state == States.AUTHENTICATED))
                        {
                            String send_string = ("SEND " + msgid + Environment.NewLine
                                            + destination + Environment.NewLine
                                            + userid + Environment.NewLine
                                            + message + Environment.NewLine
                                            + Environment.NewLine);
                            temp_client.SendTextMessage(send_string);
                        }
                    }
                }
            }
        }

        public void AckFromServer(String messageId, String fromUserID, String toUserID, WebSocketClient fromServer)
        {
            if (message_pool.Exists(MsgId => MsgId.msg_id == messageId))
            {
                MessagePool MsgfromClient = message_pool.Single(MsgId => MsgId.msg_id == messageId);
                if (String.Compare(MsgfromClient.user_id, toUserID) == 0)
                {
                    //check local pool for the client of the message                    
                    if (client_pool.Exists(UserId => UserId.clientData.id == MsgfromClient.user_id))
                    {
                        WebSocketClient toUser = client_pool.Single(UserId => UserId.clientData.id == MsgfromClient.user_id);
                        //Send to local client
                        if (toUser.clientData.client_state == States.AUTHENTICATED && toUser.clientData.ConnectType == ConnectionType.CLIENT)
                        {
                            string send_string = ("ACKN " + messageId + Environment.NewLine
                                           + fromUserID + Environment.NewLine
                                           + Environment.NewLine);
                            toUser.SendTextMessage(send_string);
                        }
                    }

                    //check hopstore for the client
                    else if (hoplist.Exists(UserId => UserId.ClientId == MsgfromClient.user_id))
                    {
                        HopStore HopToUser = hoplist.Single(UserId => UserId.ClientId == MsgfromClient.user_id);
                        WebSocketClient toUser = client_pool.Single(UserTCP => UserTCP.clientData.socket.Equals(HopToUser.TcpConn));
                        if (toUser.clientData.client_state == States.AUTHENTICATED && toUser.clientData.ConnectType != ConnectionType.CLIENT)
                        {
                            string send_string = ("ACKN " + messageId + Environment.NewLine
                                            + fromUserID + Environment.NewLine
                                            + toUserID + Environment.NewLine
                                            + Environment.NewLine);
                            toUser.SendTextMessage(send_string);
                        }
                    }
                }
            }
        }

    }
}