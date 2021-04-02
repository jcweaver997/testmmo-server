using poopstory2_server.NetData;
using poopstory2_server.NetUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace poopstory2_server
{
    public class Authentication
    {
        public string name;
        private NetworkClient client;

        public Authentication(NetworkClient client)
        {
            this.client = client;
        }

        public async Task<bool> AuthenticateAsync()
        {
            var nd = await client.ReadAsync();
            if (nd is NetworkDataLogin l)
            {
                name = l.username;
                return true;
            }
            else
            {
                return false;
            }
        }






    }
}
