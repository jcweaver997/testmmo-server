using System;

namespace poopstory2_server
{
    class Program
    {
        static void Main(string[] args)
        {
            NetworkHandler nh = new NetworkHandler();
            nh.Start(6823);
        }
    }
}
