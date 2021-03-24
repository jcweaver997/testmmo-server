using poopstory2_server.NetData;
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
        public const string VERSION = "1.0";
        Thread listenThread;
        TcpListener listener;
        int port;
        public NetworkInfo networkInfo;
        public ChannelHandler channelHandler;

        public void Start(int port)
        {
            channelHandler = new ChannelHandler();
            NetworkDataTypes.Initialize();
            if (listener != null)
            {
                Close();
            }
            this.port = port;
            networkInfo = new NetworkInfo()
            {
                clients = new List<NetworkClient>()
            };


            listener = new TcpListener(new IPEndPoint(IPAddress.Any,this.port));
            listenThread = new Thread(Listen);
            listenThread.Start();

        }

        public void Listen()
        {
            listener.Start();
            Console.WriteLine("Waiting for clients");
            while (true)
            {
                var newClient = listener.AcceptTcpClient();
                var nc = new NetworkClient(newClient,this);
                nc.OnClientClose += HandleClientClose;
                networkInfo.clients.Add(nc);
                
            }
        }

        public void Close()
        {
            listenThread.Interrupt();
            listener.Stop();

            listener = null;
            listenThread = null;
        }

        public void HandleClientClose(NetworkClient client)
        {
            networkInfo.clients.Remove(client);
            Console.WriteLine("Client closed connection");
        }

        ~NetworkHandler()
        {
            Close();
        }
    }
}
