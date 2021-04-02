using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace poopstory2_server
{
    class ConsoleHandler
    {
        private delegate void CommandHandler(string[] command);
        Dictionary<string, CommandHandler> commandHandlers;
        NetworkHandler networkHandler;
        public ConsoleHandler(NetworkHandler nh)
        {
            networkHandler = nh;
            commandHandlers = new Dictionary<string, CommandHandler>();
            commandHandlers.Add("quit",Quit);
        }


        public void Start()
        {
            while (true)
            {
                string[] command = Console.ReadLine().Split(" ");
                if (command.Length>0)
                {
                    if (commandHandlers.ContainsKey(command[0]))
                    {
                        commandHandlers[command[0]](command);
                    }
                }
            }
        }

        private void Quit(string[] commands)
        {
            Console.WriteLine("Closing...");
            networkHandler.ns.Close();
            Console.WriteLine("Closed");
        }

    }
}
