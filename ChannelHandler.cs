using poopstory2_server.NetData;
using poopstory2_server.NetTypes;
using poopstory2_server.NetUtil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Locked<ChannelInfo[]> channels;
        private Task[] channelTask;
        private Locked<Dictionary<int, PlayerInfo>> playerData;
        private Locked<List<PlayerInfo>[]> channelPlayerData;
        private Locked<Dictionary<NetworkClient, PlayerInfo>> clientData;
        private float tickTime = 1000f/10f;


        public ChannelHandler()
        {
            Task.Run(CreateChannels);

        }

        private async Task CreateChannels()
        {
            playerData = new Locked<Dictionary<int, PlayerInfo>>(new Dictionary<int, PlayerInfo>());
            clientData = new Locked<Dictionary<NetworkClient, PlayerInfo>>(new Dictionary<NetworkClient, PlayerInfo>());
            channelPlayerData = new Locked<List<PlayerInfo>[]>(new List<PlayerInfo>[MAX_CHANNELS]);
            channels = new Locked<ChannelInfo[]>(new ChannelInfo[MAX_CHANNELS]);
            channelTask = new Task[MAX_CHANNELS];


            using (var l = await channels.WaitAsync())
            {
                for (int i = 0; i < MAX_CHANNELS; i++)
                {
                    l.Value[i] = new ChannelInfo { players = 0, channelStatus = ChannelStatus.Open, maxPlayers = MAX_PLAYERS };
                }
            }
            using (var l = await channelPlayerData.WaitAsync())
            {
                for (int i = 0; i < MAX_CHANNELS; i++)
                {
                    l.Value[i] = new List<PlayerInfo>();
                    channelTask[i] = ChannelUpdates(i);
                }
            }
        }


        public async Task SelectChannel(NetworkClient client, string name)
        {
            using (var channelData = await channels.WaitAsync())
            {
                _ = client.SendAsync(new NetworkDataChannelList(channelData.Value));
            }
            NetworkData nd = await client.ReadAsync();
            if (nd is NetworkDataChannelSelect sel)
            {
                using (var channelData = await channels.WaitAsync())
                {
                    if (sel.channel >= MAX_CHANNELS || sel.channel < 0)
                    {
                        client.Close();
                        return;
                    }
                    else
                    {
                        if (channelData.Value[sel.channel].channelStatus != ChannelStatus.Open)
                        {
                            client.Close();
                            return;
                        }
                    }
                }
                
                await AddPlayer(new PlayerInfo() { client = client, playerData = new PlayerData() { name = name, channel = sel.channel } });
            }
            else
            {
                client.Close();
            }

        }

        private async Task AddPlayer(PlayerInfo pi)
        {
            pi.client.OnClientClose += ()=> { _=PlayerClose(pi); };
            pi.playerData.id = playerIndex;
            playerIndex++;
            pi.playerData.telemetry.pid = pi.playerData.id;
            await pi.client.SendAsync(new NetworkDataPlayerInfo(pi.playerData));
            pi.client.OnMessageReceived += OnMessage;
            using (var l = await playerData.WaitAsync())
            {
                l.Value.Add(pi.playerData.id, pi);
            }
            using (var l = await channels.WaitAsync())
            {
                l.Value[pi.playerData.channel].players++;
                Console.WriteLine("added player count " + l.Value[pi.playerData.channel].players);
            }
            using (var l = await channelPlayerData.WaitAsync())
            {
                l.Value[pi.playerData.channel].Add(pi);
            }
            using (var l = await clientData.WaitAsync())
            {
                l.Value.Add(pi.client, pi);
            }
        }

        private async Task PlayerClose(PlayerInfo pi)
        {
            await RemovePlayer(pi);
        }

        private async Task OnMessage(NetworkClient nc, NetworkData nd, CancellationToken token)
        {
            PlayerInfo pi;
            using (var clientdata = await clientData.WaitAsync())
            {
                if (!clientdata.Value.ContainsKey(nc)) { return; }
                pi = clientdata.Value[nc];
            }
            if (nd is NetworkDataTelemetry tel)
            {
                using (var clientdata = await clientData.WaitAsync())
                {
                    clientdata.Value[nc].playerData.telemetry = tel.Telemetry;
                    clientdata.Value[nc].playerData.telemetry.pid = pi.playerData.id;
                }
            }
            if (nd is NetworkDataPlayerInfoRequest req)
            {
                using (var playerdata = await playerData.WaitAsync())
                {
                    if (playerdata.Value.ContainsKey(req.pid))
                    {
                        _ = nc.SendAsync(new NetworkDataPlayerInfo(playerdata.Value[req.pid].playerData));
                    }
                    else
                    {
                        _ = nc.SendAsync(new NetworkDataPlayerInfo(new PlayerData() { id = req.pid, name = "" }));
                    }
                }
            }

        }

        private async Task RemovePlayer(PlayerInfo pi)
        {
            
            using (var l = await playerData.WaitAsync())
            {
                l.Value.Remove(pi.playerData.id);
            }
            int playersLeft = 0;
            using (var l = await channels.WaitAsync())
            {
                l.Value[pi.playerData.channel].players--;
                playersLeft = l.Value[pi.playerData.channel].players;
                Console.WriteLine("removed player count " + l.Value[pi.playerData.channel].players);
            }
            
            using (var l = await channelPlayerData.WaitAsync())
            {
                l.Value[pi.playerData.channel].Remove(pi);
                Parallel.ForEach(l.Value[pi.playerData.channel], new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1,playersLeft/20) }, player => {
                    Console.WriteLine($"sending pl {pi.playerData.id} to {player.playerData.id}");
                    player.client.QueueSend(new NetworkDataPlayerLeft(pi.playerData.id));
                });
            }
            using (var l = await clientData.WaitAsync())
            {
                l.Value.Remove(pi.client);
            }

        }

        private async Task ChannelUpdates(object channelIndexObject)
        {
            int channel = (int)channelIndexObject;
            Stopwatch sw = new Stopwatch();
            Stopwatch sw2 = new Stopwatch();
            
            while (true)
            {
                sw.Restart();
                using (var cpd = await channelPlayerData.WaitAsync())
                {
                    sw2.Restart();
                    int count = Math.Max(1,cpd.Value[channel].Count);
                    Parallel.ForEach(cpd.Value[channel],new ParallelOptions { MaxDegreeOfParallelism = count }, player => {
                        foreach (var otherPlayer in cpd.Value[channel])
                        {
                            PlayerTelemetry t = otherPlayer.playerData.telemetry;
                            t.pid = otherPlayer.playerData.id;
                            player.client.QueueSend(new NetworkDataTelemetry(t));
                        }
                    });
                    Console.WriteLine($"frame time {sw2.ElapsedMilliseconds} ms, players {cpd.Value[channel].Count}");
                }
                //Console.WriteLine($"server fps {1000.0f/(sw.ElapsedMilliseconds==0?1:sw.ElapsedMilliseconds)}");
                await Task.Delay((int)Math.Max(20f,tickTime-sw.ElapsedMilliseconds));
            }
        }




    }
}
