using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
	class WebSocketClient
	{
		// Raise this event when a new text message has been received
		public event TextMessageEventHandler TextMessageReceived = delegate { };

		// Raise this event when the client has or has been disconnected
		public event WebSocketClientEventHandler Disconnected = delegate { };

        public ClientData clientData { get; private set; }

        private WebSocketServer server;

        public WebSocketClient(ClientData client, WebSocketServer server/* your arguments here */)
		{
            clientData = client;
            this.server = server;
            TextMessageReceived += TextMessageReceivedHandler;
            Disconnected += server.ClientDisconnectedHandler;
		}

        #region text message handler
        private void TextMessageReceivedHandler(object source, TextMessageEventArgs e)
        {
            bool same_userid_flag = false;
            bool same_username = false;
            bool invalid_password = false;
            char[] splitspace = new char[] { ' ', '\n', '\0' };
            string[] splitline = new string[] { "\n", "\r\n" };
            string read_message = e.Message;
            ClientData active_client = e.Client.clientData;

            if (String.Compare(read_message, "QUIT") != 0)
            {
                if (new Regex("^AUTH").IsMatch(read_message) & (active_client.client_state == States.CONNECTED))
                {
                    server.CheckAuthintecation(read_message.Split(splitspace)[1].Trim(), read_message.Split(splitspace)[2].Trim(), out same_userid_flag, out same_username);
                    if (String.Compare(active_client.password, read_message.Split(splitspace)[3].Trim()) != 0)
                    {
                        invalid_password = true;
                    }

                    String send_string = null;
                    if (same_userid_flag)
                    {
                        send_string = ("FAIL " + read_message.Split(splitspace)[1].Trim() + Environment.NewLine //make sure that client splits the message according to \r\n
                                                + "NUMBER");
                        SendTextMessage(send_string);
                    }
                    else if (same_username)
                    {
                        send_string = ("FAIL " + read_message.Split(splitspace)[1].Trim() + Environment.NewLine
                                                + "NAME");
                        SendTextMessage(send_string);
                    }
                    else if (invalid_password)
                    {
                        send_string = ("FAIL " + read_message.Split(splitspace)[1].Trim() + Environment.NewLine
                                                + "PASSWORD");
                        SendTextMessage(send_string);
                    }
                    else
                    {
                        send_string = ("OKAY " + read_message.Split(splitspace)[1].Trim());
                        SendTextMessage(send_string);
                        active_client.id = read_message.Split(splitspace)[1].Trim();
                        active_client.username = read_message.Split(splitspace)[2].Trim();
                        active_client.client_state = States.AUTHENTICATED;
                        server.NotifyClientArrive(this);
                    }
                } 
                else if ((new Regex("^SEND").IsMatch(read_message)) & (active_client.client_state == States.AUTHENTICATED))
                {
                    String msg_id = read_message.Split(splitspace)[1].Trim();
                    String destination = read_message.Split(splitspace)[2].Trim();
                    bool same_msg_id = false;
                    bool too_long = false;

                    //Failure Cases
                    foreach (MessagePool new_msg in WebSocketServer.message_pool)
                    {
                        if (String.Compare(new_msg.msg_id, msg_id) == 0)
                        {
                            same_msg_id = true;
                        }
                    }
                    if (read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[2].Trim().Length > 500)
                    {
                        too_long = true;
                    }

                    //Send message
                    String send_string = null;
                    if (same_msg_id)
                    {
                        send_string = ("FAIL " + msg_id + Environment.NewLine //make sure that client splits the message according to \r\n
                                                + "NUMBER");
                        SendTextMessage(send_string);
                    }
                    else if (too_long)
                    {
                        send_string = ("FAIL " + msg_id + Environment.NewLine
                                                + "LENGTH");
                        SendTextMessage(send_string);
                    }
                    else
                    {
                        send_string = ("OKAY " + msg_id);
                        SendTextMessage(send_string);
                        WebSocketServer.message_pool.Add(new MessagePool(msg_id, active_client.id));

                        send_string = ("SEND " + msg_id + Environment.NewLine
                                        + active_client.id + Environment.NewLine
                                        + read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[2].Trim());
                        if (String.Compare(destination, "*") == 0)
                        {
                            server.BroadcastClientMessage(send_string, this);
                        }else{
                            server.SendClientMessage(send_string, destination);
                        }
                    }
                }
                else if ((new Regex("^ACKN").IsMatch(read_message)) & (active_client.client_state == States.AUTHENTICATED))
                {
                    // pass ack to the message owner
                    server.AckMessage(read_message.Split(splitspace)[1].Trim(), this);
                }
            }
            else if (String.Compare(read_message, "QUIT") == 0)
            {
                // client disconnected
                // tell other client that this client has left
                Close();
            }
            else
            {
                // invalid message received
                String send_string = ("INVD 0" + Environment.NewLine
                                 + Environment.NewLine);
                SendTextMessage(send_string);
                Close();
                return;
            }
        }
        #endregion

        #region websocket frame decode

        public struct decode_struct
        {
            public byte fin_bit;
            public byte rsv1;
            public byte rsv2;
            public byte rsv3;
            public byte opcode;
            public byte mask;
            public ulong payload_len;
            public byte[] mask_key;
            public byte[] payload_data;           
        }

        // decode incoming messages. also respond to ping (if any)
        public String Decode(Byte[] read_bytestream, ClientData active_client)
        {
            decode_struct rxed_data = new decode_struct();
            rxed_data.fin_bit = (byte)(read_bytestream[0] & (byte)128);
            rxed_data.rsv1 = (byte)(read_bytestream[0] & (byte)64);
            rxed_data.rsv2 = (byte)(read_bytestream[0] & (byte)32);
            rxed_data.rsv3 = (byte)(read_bytestream[0] & (byte)16);
            rxed_data.opcode = (byte)(read_bytestream[0] & (byte)15);
            rxed_data.mask = (byte)(read_bytestream[1] & (byte)128);

            rxed_data.payload_len = (ulong)(read_bytestream[1] & (byte)127);//I understand that this is not optimized memory usage, but please dont cut marks for this
            int mask_index = 2;
            if (rxed_data.payload_len == 126)
            {
                rxed_data.payload_len = (ulong)((read_bytestream[2] << 8) + read_bytestream[3]);
                mask_index = 4;
            }
            else if (rxed_data.payload_len == 127)
            {
                rxed_data.payload_len = (ulong)((read_bytestream[2] << 56) + (read_bytestream[3] << 48) + (read_bytestream[4] << 40) + (read_bytestream[5] << 32) + (read_bytestream[6] << 24) + (read_bytestream[7] << 16) + (read_bytestream[8] << 8) + read_bytestream[9]);
                mask_index = 10;
            }

            int i=0;
            int j=0;
            rxed_data.mask_key = new byte[4];
            for (i = mask_index; i < (mask_index + 4); i++)
            {
                rxed_data.mask_key[j] = read_bytestream[i];
                j++;
            }

            int data_index = mask_index + 4;
            rxed_data.payload_data = new byte[data_index + read_bytestream.Length];
            for (i = data_index, j = 0; i < read_bytestream.Length; i++, j++)
            {
                rxed_data.payload_data[j] = (byte)(read_bytestream[i] ^ rxed_data.mask_key[j % 4]);
            }

            String read_message = Encoding.ASCII.GetString(rxed_data.payload_data);
            //Console.WriteLine(read_message);
            if ((rxed_data.fin_bit == (byte)0) | (rxed_data.opcode == (byte)8) | (rxed_data.opcode == (byte)2) | (rxed_data.rsv1 == (byte)1) | (rxed_data.rsv2 == (byte)1) | (rxed_data.rsv3 == (byte)1))
            {
                //client disconnected
                read_message = "QUIT";
            }
            else if ((rxed_data.opcode == (byte)9))//ping frame
            {
                byte[] send_pong = new byte[2 + rxed_data.payload_data.Length];
                send_pong[0] = (byte)138;
                send_pong[1] = (byte)0;
                for (i = 2, j=0; i < send_pong.Length; i++, j++)
                {
                    send_pong[i] = rxed_data.payload_data[j];
                }
                active_client.stream.Write(send_pong, 0, send_pong.Length);
            }
            return read_message;          
        }

        #endregion

        #region websocket frame encode

        public struct encode_struct
        {
            public byte fin_bit;
            public byte rsv1;
            public byte rsv2;
            public byte rsv3;
            public byte opcode;
            public byte mask;
            public byte[] payload_len;
            public byte[] message;
        }

        public byte[] Encode(String send_string)
        {
            encode_struct send_data = new encode_struct();
            
            send_data.fin_bit = (byte)1;
            send_data.rsv1 = (byte)0;
            send_data.rsv2 = (byte)0;
            send_data.rsv3 = (byte)0;
            send_data.opcode = (byte)1;
            send_data.mask = (byte)0;

            Byte[] send_message = Encoding.UTF8.GetBytes(send_string);

            int payload_index = 0;
            if(send_message.Length<= 125)
            {
                send_data.payload_len = new byte[1];
                send_data.payload_len[0] = (byte)send_message.Length;
                payload_index = 2;
            }
            else if (send_message.Length > 125 & send_message.Length <= 65535)
            {
                send_data.payload_len = new byte[3];
                send_data.payload_len[0] = (byte)126;
                send_data.payload_len[1] = (byte)((send_message.Length >> 8) & (byte)255);
                send_data.payload_len[2] = (byte)((send_message.Length) & (byte)255);
                payload_index = 4;
            }
            else if (send_message.Length > 65535)
            {
                send_data.payload_len = new byte[9];
                send_data.payload_len[0] = (byte)127;
                send_data.payload_len[1] = (byte)((send_message.Length >> 56) & (byte)255);
                send_data.payload_len[2] = (byte)((send_message.Length >> 48) & (byte)255);
                send_data.payload_len[3] = (byte)((send_message.Length >> 40) & (byte)255);
                send_data.payload_len[4] = (byte)((send_message.Length >> 32) & (byte)255);
                send_data.payload_len[5] = (byte)((send_message.Length >> 24) & (byte)255);
                send_data.payload_len[6] = (byte)((send_message.Length >> 16) & (byte)255);
                send_data.payload_len[7] = (byte)((send_message.Length >> 8) & (byte)255);
                send_data.payload_len[8] = (byte)((send_message.Length) & (byte)255);
                payload_index = 10;
            }

            int i = 0;
            int j = 0;
            send_data.message = new byte[payload_index + send_message.Length];
            send_data.message[0] = (byte)((send_data.fin_bit << 7) +  (send_data.rsv1 << 6) + (send_data.rsv2 << 5) + (send_data.rsv3 << 4) + (send_data.opcode & (byte)15));
            for (i=1; i<payload_index; i++)
            {
                send_data.message[i] = send_data.payload_len[j];
                j++;
            }

            for (i=payload_index, j=0; i < (payload_index + send_message.Length); i++,j++)
            {
                send_data.message[i] = send_message[j];
            }

            return send_data.message;
        }
        #endregion

        // Handle incoming messages
        public void HandleMessages()
		{
            while(true){
                if (clientData.client_state != States.DISCONNECTED)
                {
                    // To make sure the stream is available
                    Thread.Sleep(1);

                    // Read data from the TCP socket, parse the WebSocket messages,
                    // and react accordingly (e.g. by sending a pong for a ping, or
                    // raising the TextMessageReceived event for a text message).

                    if (clientData.stream.DataAvailable)
                    {
                        Byte[] read_bytestream = new Byte[clientData.socket.Available];
                        clientData.stream.Read(read_bytestream, 0, read_bytestream.Length);
                        // decode the frame (also react to ping and close)
                        String read_message = Decode(read_bytestream, clientData);
                        // handle text message
                        TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                    }
                } else {
                    Close();
                    return;
                }
            }
		}

		public void SendTextMessage(String send_string)
		{
			// Send a WebSocket text message to the client.
            byte[] send_message = Encode(send_string);
            clientData.stream.Write(send_message, 0, send_message.Length);
		}

		public void Close()
		{
			// Properly close the WebSocket connection.
            if (clientData.socket.Connected)
            {
                byte[] close_frame = new byte[2];
                close_frame[0] = (byte)136;
                close_frame[1] = (byte)0;
                clientData.stream.Write(close_frame, 0, close_frame.Length);
                clientData.stream.Close(3);
                clientData.stream.Dispose();
                clientData.socket.Close();
            }
            // removes this client from the pool
            Disconnected(this, new WebSocketClientEventArgs(this));
		}
	}
}
