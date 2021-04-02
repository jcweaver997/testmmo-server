using poopstory2_server.NetData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace poopstory2_server.NetUtil
{
    public class NetworkServer
    {
        public const string VERSION = "1.0";
        public delegate void OnClientHandler(NetworkClient nc);
        public OnClientHandler OnNewClient, OnClientClose;
        Task listenTask;
        TcpListener listener;
        int port;

        public NetworkServer(int port)
        {
            NetworkDataTypes.Initialize();
            this.port = port;
        }

        public void Start()
        {
            listener = new TcpListener(new IPEndPoint(IPAddress.Any, this.port));
            listenTask = Task.Run(Listen);
        }


        private async Task Listen()
        {
            try
            {
                listener.Start();
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
                Close();
                return;
            };

            Console.WriteLine("Waiting for clients");
            while (true)
            {

                var newClient = await AcceptClient();

                if (newClient == null) break;
                var nc = new NetworkClient(newClient);
                nc.OnClientClose += () => { HandleClientClose(nc); };
                AddClient(nc);
            }
        }

        private void AddClient(NetworkClient nc)
        {
            var s = OnNewClient;
            if (s != null)
            {
                s(nc);
            }
        }

        private async Task<TcpClient> AcceptClient()
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                Close();
                return null;
            }
            return client;
        }

        public void Close()
        {
            if (listener == null) return;
            try
            {
                listener.Stop();
            }
            catch (SocketException e) { Console.WriteLine(e.Message); };

            listener = null;
            try
            {
                listenTask.Wait();
            }
            catch (InvalidOperationException e) { Console.WriteLine(e.Message); };

            listenTask = null;
        }

        public void HandleClientClose(NetworkClient client)
        {
            var s = OnClientClose;
            if (s != null)
            {
                s(client);
            }
        }

        ~NetworkServer()
        {
            Close();
        }
    }
}
