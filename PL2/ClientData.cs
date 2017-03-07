using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

/**
 * Group 7
 * Muhammad Lathif Pambudi <s8mupamb@stud.uni-saarland.de> (2556858)
 * Praharsha Sirsi <s8prsirs@stud.uni-saarland.de> (2557724)
**/

namespace ChatServerTemplate
{
    enum States
    {
        CONNECTED,
        AUTHENTICATED,
        DISCONNECTED,
    };

    class ClientData
    {    
        public TcpClient socket;
        public String id;
        public States client_state;
        public NetworkStream stream;
        public String username;
        public String password;

        public ClientData(TcpClient socket, String id, States client_state, NetworkStream stream, String username, String password)
        {
            this.socket = socket;
            this.id = id;
            this.client_state = client_state;
            this.stream = stream;
            this.username = username;
            this.password = password;
        }

    }
}
