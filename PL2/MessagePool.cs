using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/**
 * Group 7
 * Muhammad Lathif Pambudi <s8mupamb@stud.uni-saarland.de> (2556858)
 * Praharsha Sirsi <s8prsirs@stud.uni-saarland.de> (2557724)
**/

namespace ChatServerTemplate
{

    class MessagePool
    {
        public String msg_id;
        public String user_id;

        public MessagePool(String msg_id, String user_id)
        {
            this.msg_id = msg_id;
            this.user_id = user_id;
        }
    }
}
