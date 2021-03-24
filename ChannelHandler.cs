using poopstory2_server.NetData;
using poopstory2_server.NetTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace poopstory2_server
{
    class PlayerInfo
    {
        public NetworkClient client;
        public PlayerData playerData;
    }

    public class ChannelHandler
    {
        private const int MAX_CHANNELS = 1;
        private const int MAX_PLAYERS = 1000;
        private int playerIndex = 1;
        private ChannelInfo[] channels;
        private Thread[] channelThread;
        private Dictionary<int, PlayerInfo> playerData;
        private List<PlayerInfo>[] channelPlayerData;
        private Dictionary<NetworkClient, PlayerInfo> clientData;


        public ChannelHandler()
        {
            CreateChannels();

        }

        private void CreateChannels()
        {
            playerData = new Dictionary<int, PlayerInfo>();
            clientData = new Dictionary<NetworkClient, PlayerInfo>();
            channelPlayerData = new List<PlayerInfo>[MAX_CHANNELS];
            channels = new ChannelInfo[MAX_CHANNELS];
            channelThread = new Thread[MAX_CHANNELS];

            lock (channelPlayerData)
            {
                for (int i = 0; i < MAX_CHANNELS; i++)
                {
                    channels[i] = new ChannelInfo { players = 0, channelStatus = ChannelStatus.Open, maxPlayers = MAX_PLAYERS };
                    channelPlayerData[i] = new List<PlayerInfo>();
                    channelThread[i] = new Thread(ChannelThread);
                    channelThread[i].Start(i);
                }
            }


        }


        public void SelectChannel(NetworkClient client, string name)
        {
            client.Send(new NetworkDataChannelList(channels));
            Console.WriteLine("sent channels");
            NetworkData nd = client.ReadBlock();
            if (nd is NetworkDataChannelSelect sel)
            {
                Console.WriteLine($"selected channel {sel.channel}");

                if (sel.channel>=MAX_CHANNELS)
                {
                    client.Close();
                    return;
                }
                AddPlayer(new PlayerInfo() { client = client, playerData = new PlayerData() { name = name, channel = sel.channel } });
            }
            else
            {
                client.Close();
            }

        }

        private void AddPlayer(PlayerInfo pi)
        {
            pi.client.OnClientClose += PlayerClose;
            pi.playerData.id = playerIndex;
            playerIndex++;
            pi.playerData.telemetry.pid = pi.playerData.id;
            Console.WriteLine($"Adding player with id {pi.playerData.id}");
            pi.client.Send(new NetworkDataPlayerInfo(pi.playerData));
            Console.WriteLine($"sent player info");
            lock (playerData)
            {
                playerData.Add(pi.playerData.id,pi);
            }
            lock (channels)
            {
                channels[pi.playerData.channel].players++;
            }
            lock (channelPlayerData[pi.playerData.channel])
            {
                channelPlayerData[pi.playerData.channel].Add(pi);
            }
            lock (clientData)
            {
                clientData.Add(pi.client,pi);
            }
            pi.client.OnMessageReceived += OnMessage;

        }

        private void PlayerClose(NetworkClient nc)
        {
            PlayerInfo pi;
            lock (clientData)
            {
                pi = clientData[nc];
            }
            RemovePlayer(pi);
        }

        private void OnMessage(NetworkClient nc, NetworkData nd)
        {
            lock(clientData)
            {
                if (!clientData.ContainsKey(nc)){return;}
                PlayerInfo pi = clientData[nc];

                if(nd is NetworkDataTelemetry tel)
                {
                    clientData[nc].playerData.telemetry = tel.Telemetry;
                    clientData[nc].playerData.telemetry.pid = pi.playerData.id;
                }
                if(nd is NetworkDataPlayerInfoRequest req)
                {
                    lock(playerData){
                        if (playerData.ContainsKey(req.pid))
                        {
                            nc.BeginSend(new NetworkDataPlayerInfo(playerData[req.pid].playerData));
                        }
                        else
                        {
                            nc.BeginSend(new NetworkDataPlayerInfo(new PlayerData() {id = req.pid, name = "" }));
                        }
                    }
                }
            }
            
        }

        private void RemovePlayer(PlayerInfo pi)
        {
            lock (playerData)
            {
                playerData.Remove(pi.playerData.id);
            }
            lock (channels)
            {
                channels[pi.playerData.channel].players--;
            }
            lock (channelPlayerData[pi.playerData.channel])
            {
                channelPlayerData[pi.playerData.channel].Remove(pi);
            }
            lock (clientData)
            {
                clientData.Remove(pi.client);
            }
        }

        private void ChannelThread(object channelIndexObject)
        {
            int channel = (int)channelIndexObject;
            while (true)
            {
                lock (channelPlayerData[channel])
                {
                    foreach (var player in channelPlayerData[channel])
                    {
                        foreach (var otherPlayer in channelPlayerData[channel])
                        {
                            PlayerTelemetry t = otherPlayer.playerData.telemetry;
                            t.pid = otherPlayer.playerData.id;
                            player.client.BeginSend(new NetworkDataTelemetry(t));
                        }
                    }
                }

                Thread.Sleep(500);
            }
        }




    }
}
