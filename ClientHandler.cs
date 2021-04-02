using poopstory2_server.NetData;
using poopstory2_server.NetUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace poopstory2_server
{
    public class ClientHandler
    {

        public delegate void ClientHandlerEvent();
        public event ClientHandlerEvent onConnectionInitialized, onClose;
        public NetworkClient client;
        public Authentication auth;
        private Task initConnection;


        public ClientHandler(NetworkClient client)
        {
            this.client = client;
            this.client.OnClientClose += Close;
            initConnection = Task.Run(InitConnection);
        }

        public void Close()
        {
            client.Close();
            var s = onClose;
            if (s!=null)
            {
                s();
            }
        }


        private async Task InitConnection()
        {
            await CheckVersion();

            auth = new Authentication(client);
            bool success = await auth.AuthenticateAsync();
            if (!success)
            {
                client.Close();
                return;
            }

            var s = onConnectionInitialized;
            if (s != null)
            {
                s();
            }
        }

        private async Task CheckVersion()
        {
            await client.SendAsync(new NetworkDataVersion(NetworkServer.VERSION));
            NetworkData nd = await client.ReadAsync();
            if (nd is NetworkDataVersion v)
            {
                if (v.version != NetworkServer.VERSION)
                {
                    client.Close();
                }
            }
            else
            {
                client.Close();
            }
        }


    }
}
