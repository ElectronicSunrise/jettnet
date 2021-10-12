using System;

namespace jettnet.server
{

    class ServerProgram
    {


        static void Main(string[] args)
        {
            Console.WriteLine("Starting Server...");

            var server = new JettServer();

            server.Start();

            server.RegisterMessage<MessageTest>((msg) => Console.WriteLine(msg.Username));

            while (true)
            {
                var msg = Console.ReadLine();

                switch (msg)
                {
                    case "ping":
                        server.SendTo(new ArraySegment<byte>(new byte[] { 1 }), -1, 69);
                        break;
                    case "pong":
                        server.SendTo(new ArraySegment<byte>(new byte[] { 2 }), -1, 69);
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
