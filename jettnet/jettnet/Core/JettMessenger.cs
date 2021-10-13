using jettnet.logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace jettnet
{
    public class JettMessenger
    {
        private Dictionary<int, Action<JettReader>> _messageHandlers = new Dictionary<int, Action<JettReader>>();
        private Dictionary<Type, IJettMessage> _messageReaders;

        private Queue<IJettMessage> _pendingMessages = new Queue<IJettMessage>();

        private static MD5 _crypto = MD5.Create();
        
        private Socket _socket;
        private Logger _logger;

        private bool _isServer;

        public JettMessenger(Socket socket, Logger logger, bool serverMessenger) 
        {
            _messageReaders = GetReadersAndWritersForMessages();
            _socket = socket;
            _logger = logger;

            _isServer = serverMessenger;
        }

        public void SendToClient(IJettMessage msg, int connectionId, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get())
            {
                writer.WriteByte((byte)Messages.Message);
                writer.WriteInt(GetMessageId(msg.GetType().Name));

                msg.Serialize(writer);
                _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connectionId, channel);
            }
        }

        public void SendToServer(IJettMessage msg, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get())
            {
                writer.WriteByte((byte)Messages.Message);
                writer.WriteInt(GetMessageId(msg.GetType().Name));

                msg.Serialize(writer);
                _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
            }
        }

        private static Dictionary<Type, IJettMessage> GetReadersAndWritersForMessages()
        {
            var type = typeof(IJettMessage);

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(x => type.IsAssignableFrom(x)).ToArray();

            Dictionary<Type, IJettMessage> foundPairs = new Dictionary<Type, IJettMessage>();

            foreach (var item in types)
            {
                // ignore the default interface and the generic interfaces
                if (item.ContainsGenericParameters || item == type)
                    continue;

                var instance = Activator.CreateInstance(item) as IJettMessage;

                foundPairs.Add(item, instance);
            }

            return foundPairs;
        }

        public void RegisterInternal<T>(Action<T> handler) where T : struct, IJettMessage
        {
            int id = GetMessageId(typeof(T).Name);

            _messageHandlers[id] = CreateMessageDelegate(handler);
        }

        private Action<JettReader> CreateMessageDelegate<T>(Action<T> handler) where T : struct, IJettMessage
        {
            Action<JettReader> del = (reader) =>
            {
                try
                {
                    var type = typeof(T);

                    T msg = (_messageReaders[type] as IJettMessage<T>).Deserialize(reader);

                    handler.Invoke(msg);
                }
                catch
                {
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {nameof(T)}", LogLevel.Error);
                }
            };

            return del;
        }

        public void HandleIncomingMessage(JettReader reader)
        {
            int messageId = reader.ReadInt();

            _messageHandlers[messageId].Invoke(reader);
        }

        private int GetMessageId(string funcName)
        { 
            var result = _crypto.ComputeHash(Encoding.UTF8.GetBytes(funcName));
            return BitConverter.ToInt32(result, 0);
        }
    }
}
