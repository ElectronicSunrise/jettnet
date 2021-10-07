using System;

namespace jettnet.client
{
    class ClientProgram
    {
        static void Main(string[] args)
        {
            var client = new JettClient();

            client.Connect("127.0.0.1", 7777);

            while (!client.Connected) { }

            while (true)
            {
                var msg = Console.ReadLine();

                switch (msg)
                {
                    case "ping":
                        client.Send(new ArraySegment<byte>(new byte[] { 1 }), 0);
                        break;
                    case "pong":
                        client.Send(new ArraySegment<byte>(new byte[] { 2 }), 0);
                        break;

                    default:

                        Console.WriteLine("No valid input");
                        break;
                }
            }
        }
    }
}
 