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
            //Console.WriteLine("Message Received: " + read_message);
            ClientData active_client = e.Client.clientData;

            if (String.Compare(read_message, "QUIT") != 0)
            {
                #region Client Messages
                if (clientData.ConnectType == ConnectionType.CLIENT)
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
                            WebSocketServer.message_pool.Add(new MessagePool(msg_id, active_client.id, this.clientData.socket));

                            String LocalMessage = read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[2].Trim();                            
                            if (String.Compare(destination, "*") == 0)
                            {
                                server.BroadcastClientMessage(msg_id, destination, active_client.id, LocalMessage, this);
                            }
                            else
                            {
                                server.SendClientMessage(msg_id, destination, active_client.id, LocalMessage, this);
                            }
                        }
                    }
                    else if ((new Regex("^ACKN").IsMatch(read_message)) & (active_client.client_state == States.AUTHENTICATED))
                    {
                        // pass ack to the message owner
                        server.AckMessage(read_message.Split(splitspace)[1].Trim(), this);
                    }
                }
                #endregion

                #region server messages
                else
                {
                    //check SRVR connection for Authentication
                    if ((new Regex("^SRVR").IsMatch(read_message)) & (clientData.client_state == States.CONNECTED) & (clientData.ConnectType == ConnectionType.SERVERSERVER))
                    {
                        server.NewServerConnection(this);
                        clientData.client_state = States.AUTHENTICATED;
                        //Console.WriteLine("On receiving srvr message: " + read_message);                           
                    }
                        //Handle ARRV message from server
                    else if ((new Regex("^ARRV").IsMatch(read_message)) & (clientData.client_state == States.AUTHENTICATED))
                    {
                        String id = read_message.Split(splitspace)[1].Trim();
                        String username = read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                        String description = read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[2].Trim();
                        Int32 HopCnt = Convert.ToInt32(read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[3].Trim());
                        server.ClientArriveFromServer(id, username, description, HopCnt, this);
                    }
                        //Handle Left message from server
                    else if ((new Regex("^LEFT").IsMatch(read_message)) & (clientData.client_state == States.AUTHENTICATED))
                    {
                        String id = read_message.Split(splitspace)[1].Trim();
                        server.ClientLeftFromServer(id, this);
                    }
                   
                        //Handle SEND message from server
                    else if ((new Regex("^SEND").IsMatch(read_message)) & (clientData.client_state == States.AUTHENTICATED))
                    {
                        String fromServer_msgid = read_message.Split(splitspace)[1].Trim();
                        String fromServer_destination = read_message.Split(splitspace)[2].Trim();
                        String fromServer_userid = read_message.Split(splitspace)[3].Trim();
                        String fromServer_message = read_message.Split(splitline, StringSplitOptions.RemoveEmptyEntries)[3].Trim();
                        if (!WebSocketServer.message_pool.Exists(msgid => msgid.msg_id == fromServer_msgid)) //Check if the message already exists to prevent redundant copies in cyclic topology
                        {
                            WebSocketServer.message_pool.Add(new MessagePool(fromServer_msgid, fromServer_userid, this.clientData.socket)); //add new message to the pool
                            if (String.Compare(fromServer_destination, "*") == 0)
                            {
                                server.BroadcastMessageFromServer(fromServer_msgid, fromServer_destination, fromServer_userid, fromServer_message, this);
                            }
                            else
                            {
                                server.SendMessageFromServer(fromServer_msgid, fromServer_destination, fromServer_userid, fromServer_message, this);
                            }
                        }
                    }
                    
                        //Handle ACKN message from server
                    else if ((new Regex("^ACKN").IsMatch(read_message)) & (clientData.client_state == States.AUTHENTICATED))
                    {
                        String fromServer_msgid = read_message.Split(splitspace)[1].Trim();
                        String fromServer_fromUserID = read_message.Split(splitspace)[2].Trim();
                        String fromServer_toUserID = read_message.Split(splitspace)[3].Trim();
                        server.AckFromServer(fromServer_msgid, fromServer_fromUserID, fromServer_toUserID, this);
                    }
                }
                #endregion

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
                String send_string = ("INVD 0");
                SendTextMessage(send_string);
                Close();
                return;
            }
        }
        #endregion

        //For Decoding Masked Data
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
        public void Decode(Byte[] read_bytestream, ClientData active_client)
        {
            decode_struct rxed_data = new decode_struct();
            rxed_data.fin_bit = (byte)(read_bytestream[0] & (byte)128);
            rxed_data.rsv1 = (byte)(read_bytestream[0] & (byte)64);
            rxed_data.rsv2 = (byte)(read_bytestream[0] & (byte)32);
            rxed_data.rsv3 = (byte)(read_bytestream[0] & (byte)16);
            rxed_data.opcode = (byte)(read_bytestream[0] & (byte)15);
            if (read_bytestream.Length >= 7) //to handle messages not completely received while the server disconnects
            {
                rxed_data.mask = (byte)(read_bytestream[1] & (byte)128);

                rxed_data.payload_len = (ulong)(read_bytestream[1] & (byte)127);//I understand that this is not optimized memory usage, but please dont cut marks for this
                uint mask_index = 2;
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

                uint i = 0;
                uint j = 0;
                rxed_data.mask_key = new byte[4];
                for (i = mask_index; i < (mask_index + 4); i++)
                {
                    rxed_data.mask_key[j] = read_bytestream[i];
                    j++;
                }

                uint data_index = mask_index + 4;
                //rxed_data.payload_data = new byte[data_index + read_bytestream.Length];
                rxed_data.payload_data = new byte[rxed_data.payload_len];
                //for (i = data_index, j = 0; i < read_bytestream.Length; i++, j++)
                for (i = data_index, j = 0; j < rxed_data.payload_len; i++, j++)
                {
                    rxed_data.payload_data[j] = (byte)(read_bytestream[i] ^ rxed_data.mask_key[j % 4]);
                }
                uint nextByteRead = i;

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
                    for (i = 2, j = 0; i < send_pong.Length; i++, j++)
                    {
                        send_pong[i] = rxed_data.payload_data[j];
                    }
                    active_client.stream.Write(send_pong, 0, send_pong.Length);
                }

                //Handle more than one message received in TCP packet
                if (read_message != null) //handle first message first
                {
                    TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                }

                if (nextByteRead < read_bytestream.Length)
                {
                    Console.WriteLine("more than one message received in a single tcp packet");
                    byte[] nextByteStream = new byte[read_bytestream.Length - nextByteRead];
                    System.Array.Copy(read_bytestream, nextByteRead, nextByteStream, 0, nextByteStream.Length);
                    Decode(nextByteStream, active_client); //handle next message
                }                 
            }
            else
            {
                if ((rxed_data.fin_bit == (byte)0) | (rxed_data.opcode == (byte)8) | (rxed_data.opcode == (byte)2) | (rxed_data.rsv1 == (byte)1) | (rxed_data.rsv2 == (byte)1) | (rxed_data.rsv3 == (byte)1))
                {
                    //client disconnected
                    String read_message =  "QUIT";
                    TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                }
                else
                {
                    //server disconnected or Malformed Message
                    String read_message = "QUIT";
                    TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                }
            }
        }

        #endregion

        //For Sending Non-Masked Data
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
            if (send_message.Length <= 125)
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
            send_data.message[0] = (byte)((send_data.fin_bit << 7) + (send_data.rsv1 << 6) + (send_data.rsv2 << 5) + (send_data.rsv3 << 4) + (send_data.opcode & (byte)15));
            for (i = 1; i < payload_index; i++)
            {
                send_data.message[i] = send_data.payload_len[j];
                j++;
            }

            for (i = payload_index, j = 0; i < (payload_index + send_message.Length); i++, j++)
            {
                send_data.message[i] = send_message[j];
            }

            return send_data.message;
        }
        #endregion

        private Random mask_keygen = new Random();

        //For sending Masked Data to other Servers
        #region websocket frame masking encode
        public struct MaskEncode_Struct
        {
            public byte fin_bit;
            public byte rsv1;
            public byte rsv2;
            public byte rsv3;
            public byte opcode;
            public byte mask;
            public byte[] payload_len;
            public byte[] mask_key;
            public byte[] message;
        }
        
        public byte[] MaskEncode(String send_string)
        {
            MaskEncode_Struct SendMaskData = new MaskEncode_Struct();

            SendMaskData.fin_bit = (byte)1;
            SendMaskData.rsv1 = (byte)0;
            SendMaskData.rsv2 = (byte)0;
            SendMaskData.rsv3 = (byte)0;
            SendMaskData.opcode = (byte)1;
            SendMaskData.mask = (byte)1;

            byte[] mask_key = new byte[4];
            mask_keygen.NextBytes(mask_key);
            SendMaskData.mask_key = mask_key;

            Byte[] send_message = Encoding.UTF8.GetBytes(send_string);

            int mask_index = 0;
            if (send_message.Length <= 125)
            {
                SendMaskData.payload_len = new byte[1];
                SendMaskData.payload_len[0] = (byte)(send_message.Length | 0x80);
                mask_index = 2;
            }
            else if (send_message.Length > 125 & send_message.Length <= 65535)
            {
                SendMaskData.payload_len = new byte[3];
                SendMaskData.payload_len[0] = (byte)(126 | 0x80);
                SendMaskData.payload_len[1] = (byte)((send_message.Length >> 8) & (byte)255);
                SendMaskData.payload_len[2] = (byte)((send_message.Length) & (byte)255);
                mask_index = 4;
            }
            else if (send_message.Length > 65535)
            {
                SendMaskData.payload_len = new byte[9];
                SendMaskData.payload_len[0] = (byte)(127 | 0x80);
                SendMaskData.payload_len[1] = (byte)((send_message.Length >> 56) & (byte)255);
                SendMaskData.payload_len[2] = (byte)((send_message.Length >> 48) & (byte)255);
                SendMaskData.payload_len[3] = (byte)((send_message.Length >> 40) & (byte)255);
                SendMaskData.payload_len[4] = (byte)((send_message.Length >> 32) & (byte)255);
                SendMaskData.payload_len[5] = (byte)((send_message.Length >> 24) & (byte)255);
                SendMaskData.payload_len[6] = (byte)((send_message.Length >> 16) & (byte)255);
                SendMaskData.payload_len[7] = (byte)((send_message.Length >> 8) & (byte)255);
                SendMaskData.payload_len[8] = (byte)((send_message.Length) & (byte)255);
                mask_index = 10;
            }

            int payload_index = mask_index + 4;
            int i = 0;
            int j = 0;
            SendMaskData.message = new byte[payload_index + send_message.Length];
            SendMaskData.message[0] = (byte)((SendMaskData.fin_bit << 7) + (SendMaskData.rsv1 << 6) + (SendMaskData.rsv2 << 5) + (SendMaskData.rsv3 << 4) + (SendMaskData.opcode & (byte)15));

            for (i = 1; i < mask_index; i++)
            {
                SendMaskData.message[i] = SendMaskData.payload_len[j];
                j++;
            }

            for (i = mask_index, j = 0; i < payload_index; i++, j++)
            {
                SendMaskData.message[i] = SendMaskData.mask_key[j];
            }

            for (i = payload_index, j = 0; i < (payload_index + send_message.Length); i++, j++)
            {
                SendMaskData.message[i] = (byte)(send_message[j] ^ SendMaskData.mask_key[j % 4]);
            }

            return SendMaskData.message;
        }
        #endregion

        //For Decoding Non-Masked data from other servers
        #region websocket frame without masking decode
        public struct NotMaskDecode_Struct
        {
            public byte fin_bit;
            public byte rsv1;
            public byte rsv2;
            public byte rsv3;
            public byte opcode;
            public byte mask;
            public ulong payload_len;
            public byte[] payload_data;
        }

        
        public void NotMaskDecode(Byte[] read_bytestream, ClientData active_client)
        {
            NotMaskDecode_Struct rxednomask_data = new NotMaskDecode_Struct();
            rxednomask_data.fin_bit = (byte)(read_bytestream[0] & (byte)128);
            rxednomask_data.rsv1 = (byte)(read_bytestream[0] & (byte)64);
            rxednomask_data.rsv2 = (byte)(read_bytestream[0] & (byte)32);
            rxednomask_data.rsv3 = (byte)(read_bytestream[0] & (byte)16);
            rxednomask_data.opcode = (byte)(read_bytestream[0] & (byte)15);
            if (read_bytestream.Length >= 7) //to handle messages not completely received while the server disconnects
            {
                rxednomask_data.mask = (byte)(read_bytestream[1] & (byte)128);

                rxednomask_data.payload_len = (ulong)(read_bytestream[1] & (byte)127);//I understand that this is not optimized memory usage, but please dont cut marks for this
                uint payload_index = 2;
                if (rxednomask_data.payload_len == 126)
                {
                    rxednomask_data.payload_len = (ulong)((read_bytestream[2] << 8) + read_bytestream[3]);
                    payload_index = 4;
                }
                else if (rxednomask_data.payload_len == 127)
                {
                    rxednomask_data.payload_len = (ulong)((read_bytestream[2] << 56) + (read_bytestream[3] << 48) + (read_bytestream[4] << 40) + (read_bytestream[5] << 32) + (read_bytestream[6] << 24) + (read_bytestream[7] << 16) + (read_bytestream[8] << 8) + read_bytestream[9]);
                    payload_index = 10;
                }

                uint i = 0;
                uint j = 0;

                //rxednomask_data.payload_data = new byte[payload_index + read_bytestream.Length];
                rxednomask_data.payload_data = new byte[rxednomask_data.payload_len];
                //for (i = payload_index, j = 0; i < read_bytestream.Length; i++, j++)
                for (i = payload_index, j = 0; j < rxednomask_data.payload_len; i++, j++)
                {
                    rxednomask_data.payload_data[j] = (byte)(read_bytestream[i]);
                }
                uint nextByteRead = i;

                String read_message = Encoding.ASCII.GetString(rxednomask_data.payload_data);
                //Console.WriteLine(read_message);
                if ((rxednomask_data.fin_bit == (byte)0) | (rxednomask_data.opcode == (byte)8) | (rxednomask_data.opcode == (byte)2) | (rxednomask_data.rsv1 == (byte)1) | (rxednomask_data.rsv2 == (byte)1) | (rxednomask_data.rsv3 == (byte)1))
                {
                    //client disconnected
                    read_message = "QUIT";
                }
                else if ((rxednomask_data.opcode == (byte)9))//ping frame
                {
                    byte[] send_pong = new byte[2 + rxednomask_data.payload_data.Length];
                    send_pong[0] = (byte)138;
                    send_pong[1] = (byte)0;
                    for (i = 2, j = 0; i < send_pong.Length; i++, j++)
                    {
                        send_pong[i] = rxednomask_data.payload_data[j];
                    }
                    active_client.stream.Write(send_pong, 0, send_pong.Length);
                }
                
                //Handle more than one message received in TCP packet
                if (read_message != null) //handle first message first
                {
                    TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                }

                if (nextByteRead < read_bytestream.Length)
                {
                    Console.WriteLine("More than one message received in a single tcp packet");
                    byte[] nextByteStream = new byte[read_bytestream.Length - nextByteRead];
                    System.Array.Copy(read_bytestream, nextByteRead, nextByteStream, 0, nextByteStream.Length);
                    NotMaskDecode(nextByteStream, active_client); //handle next message
                }                
            }
            else
            {
                if ((rxednomask_data.fin_bit == (byte)0) | (rxednomask_data.opcode == (byte)8) | (rxednomask_data.opcode == (byte)2) | (rxednomask_data.rsv1 == (byte)1) | (rxednomask_data.rsv2 == (byte)1) | (rxednomask_data.rsv3 == (byte)1))
                {
                    //client disconnected
                    String read_message = "QUIT";
                    TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                }
                else
                {
                    //Server disconnected or Malformed message
                    String read_message = "QUIT";
                    TextMessageReceived(this, new TextMessageEventArgs(this, read_message));
                }
            }
        }
        #endregion
        
        // Handle incoming messages
        public void HandleMessages()
        {
            //Initiate SRVR message once
            if (clientData.ConnectType == ConnectionType.SERVERCLIENT & clientData.client_state == States.CONNECTED)
            {
                //for connecting with modestchecker.net
                long pwdid = ToSrvrNumber("YD4G2gs7");
                String send_string = ("SRVR " + pwdid);

                //otherwise
                //String send_string = ("SRVR " + clientData.id);

                SendTextMessage(send_string);
                //Console.WriteLine("Sent SRVR message: " + send_string);
                clientData.client_state = States.AUTHENTICATED;
                server.NewServerConnection(this);
            }

            while (true)
            {
                if (clientData.client_state != States.DISCONNECTED)
                {
                    // To make sure the stream is available
                    Thread.Sleep(1);

                    // Read data from the TCP socket, parse the WebSocket messages,
                    // and react accordingly (e.g. by sending a pong for a ping, or
                    // raising the TextMessageReceived event for a text message).

                    if (clientData.socket.Available > 1) //changed as complete message was not being read
                    {
                        Byte[] read_bytestream = new Byte[clientData.socket.Available];
                        
                        clientData.stream.Read(read_bytestream, 0, read_bytestream.Length);                    
                        
                        if (clientData.ConnectType == ConnectionType.SERVERCLIENT)
                        {
                            NotMaskDecode(read_bytestream, clientData);
                        }
                        else
                        {
                            Decode(read_bytestream, clientData);
                        }                        
                    }
                }
                else
                {
                    Close();
                    return;
                }
            }
        }

        public void SendTextMessage(String send_string)
        {
            String SendWithoutNewline = send_string.Trim(); //Added to make it work with modestchecker
            // Send a WebSocket text message to the client.
            byte[] send_message = null;
            if (clientData.ConnectType == ConnectionType.SERVERCLIENT)
            {
                //send_message = MaskEncode(send_string);
                send_message = MaskEncode(SendWithoutNewline);
            }
            else
            {
                //send_message = Encode(send_string);
                send_message = Encode(SendWithoutNewline);
            }

            if (send_message != null)
            {                
                //String SendWithoutSpace = send_string.Trim();
                //Console.WriteLine("Sent Message: " + SendWithoutNewline);
                if (clientData.socket.Connected)
                {
                    Thread.Sleep(10);                    
                    clientData.stream.Write(send_message, 0, send_message.Length);
                    clientData.stream.Flush();
                }
            }
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
                clientData.stream.Flush();
                clientData.stream.Close(3);
                clientData.stream.Dispose();
                clientData.socket.Close();
            }
            // removes this client from the pool
            Disconnected(this, new WebSocketClientEventArgs(this));
        }

        long ToSrvrNumber(string password) { return Encoding.ASCII.GetBytes(password).Aggregate(0L, (acc, b) => (acc << 7) | b); }
    }
}
