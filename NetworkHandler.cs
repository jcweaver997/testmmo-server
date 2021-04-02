using poopstory2_server.NetData;
using poopstory2_server.NetUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace poopstory2_server
{
    /// <summary>
    /// Handles the network connections for one game channel.
    /// </summary>
    public class NetworkHandler
    {
        public ChannelHandler channelHandler;
        public NetworkServer ns;

        public void Start(int port)
        {
            channelHandler = new ChannelHandler();
            ns = new NetworkServer(port);
            ns.OnNewClient += (nc) =>
            {
                ClientHandler ch = new ClientHandler(nc);
                ch.onConnectionInitialized += ()=>Task.Run(()=>channelHandler.SelectChannel(ch.client,ch.auth.name));
            };

            ns.Start();

        }

     
    }
}
