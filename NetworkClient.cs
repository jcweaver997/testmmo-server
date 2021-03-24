using poopstory2_server.NetData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace poopstory2_server
{
    public class NetworkClient
    {
        public delegate void MessageReceivedHandler(NetworkClient nc, NetworkData nd);
        public delegate void NetworkClientEvent(NetworkClient nc);
        public event NetworkClientEvent OnClientClose;
        public event MessageReceivedHandler OnMessageReceived;

        public TcpClient tcpClient;
        public ConcurrentQueue<NetworkData> messageQueue;

        private WaitObject<NetworkData> waitMessage;
        private Thread listenThread, mainThread, keepAliveThread;
        private Authentication auth;
        private NetworkHandler networkHandler;

        public NetworkClient(TcpClient tcpClient, NetworkHandler handler)
        {
            this.tcpClient = tcpClient;
            networkHandler = handler;
            messageQueue = new ConcurrentQueue<NetworkData>();
            waitMessage = new WaitObject<NetworkData>();
            listenThread = new Thread(Listen);
            listenThread.Start();
            keepAliveThread = new Thread(KeepAlive);
            keepAliveThread.Start();
            mainThread = new Thread(Main);
            mainThread.Start();

        }

        private void Main()
        {
            CheckVersion();
            auth = new Authentication(this);
            string name;
            bool success = auth.Authenticate(out name);
            if (!success)
            {
                Close();
                return;
            }
            networkHandler.channelHandler.SelectChannel(this,name);

        }

        private void CheckVersion()
        {
            Send(new NetworkDataVersion(NetworkHandler.VERSION));
            if (ReadBlock() is NetworkDataVersion v)
            {
                if (v.version != NetworkHandler.VERSION)
                {
                    Close();
                }
                Console.WriteLine("Client version "+v.version);
            }
            else
            {
                Close();
            }
        }

        private void Listen()
        {
            byte[] buffer = new byte[6000];
            int index = 0;
            NetworkStream ns = tcpClient.GetStream();
            while (true)
            {
                try
                {
                    int l = ns.Read(buffer, index, NetworkData.MAX_SIZE-index);
                    index += l;
                    if (l == 0)
                    {
                        Close();
                        return;
                    }
                    if ((int)index >4)
                    {
                        int dataLength = BitConverter.ToInt32(buffer,0);
                        if (dataLength > NetworkData.MAX_SIZE) { Console.Error.WriteLine("data length longer than MAX_SIZE."); Close(); return; }
                        if ((int)index >= dataLength)
                        {
                            int extraBytes = (int)index - dataLength;
                            byte[] data = new byte[dataLength];
                            Buffer.BlockCopy(buffer,0,data,0,dataLength);
                            Buffer.BlockCopy(buffer,dataLength,buffer,0,extraBytes);
                            index = 0;
                            Queue(NetworkData.Parse(data));
                        }
                    }
                }
                catch (Exception e) { Console.WriteLine(e.Message); Close(); return; }
            }
        }

        private void KeepAlive()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(5000);
                    Send(new NetworkDataKeepAlive());
                }catch(ThreadInterruptedException){ return; };

            }
        }


        private void Queue(NetworkData nd)
        {
            if (nd == null) return;
            if(nd is NetworkDataKeepAlive keepAlive)
            {
                return;
            }
            if (waitMessage.Give(nd)) return;
            var onMessageReceived = OnMessageReceived;
            if (onMessageReceived != null)
            {
                onMessageReceived(this, nd);
            }
            else
            {
                messageQueue.Enqueue(nd);
            }

        }

        public NetworkData ReadBlock()
        {
            NetworkData nd;
            if (messageQueue.TryDequeue(out nd)) return nd;
            try
            {
                nd = waitMessage.Wait();
            }
            catch (Exception) { Close(); }

            return nd;
        }

        public void Send(NetworkData nd)
        {
            if (nd == null) { return; }
            byte[] bytes = nd.GetBytes();
            try
            {
                tcpClient.GetStream().Write(bytes, 0, bytes.Length);
            }
            catch (Exception ) { Close(); };

        }

        public void BeginSend(NetworkData nd)
        {
            if (nd == null) { return; }
            byte[] bytes = nd.GetBytes();
            try
            {
                tcpClient.GetStream().BeginWrite(bytes, 0, bytes.Length, null, null);
            }
            catch (Exception) { Close(); };
        }

        public void Close()
        {
            listenThread.Interrupt();
            mainThread.Interrupt();
            keepAliveThread.Interrupt();
            tcpClient.Close();
            var s = OnClientClose;
            if (s!= null)
            {
                s(this);
            }
        }



    }
}
