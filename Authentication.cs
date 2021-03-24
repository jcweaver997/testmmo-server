using poopstory2_server.NetData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace poopstory2_server
{
    class AuthenticationData
    {

    }
    class Authentication
    {
        private NetworkClient client;

        public Authentication(NetworkClient client)
        {
            this.client = client;
        }

        public bool Authenticate(out string username)
        {
            var nd = client.ReadBlock();
            if (nd is NetworkDataLogin l)
            {
                Console.WriteLine($"u: {l.username} p: {l.password}");
                username = l.username;
                return true;

            }
            else
            {
                username = "";
                Console.WriteLine("auth failed with "+nd);
                return false;
            }
        }






    }
}
