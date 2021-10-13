using jettnet.logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace jettnet
{
    public class JettMessenger
    {
        private Dictionary<int, Action<JettReader>> _messageHandlers = new Dictionary<int, Action<JettReader>>();
        private Dictionary<int, Action> _pendingReceiveCallbacks = new Dictionary<int, Action>();
        private Dictionary<Type, IJettMessage> _messageReaders;
        
        private Socket _socket;
        private Logger _logger;

        private bool _isServer;

        private int _recvCounter { get { _counter++; return _counter; } set { _counter = value; } }
        private int _counter; 

        public JettMessenger(Socket socket, Logger logger, bool serverMessenger) 
        {
            _messageReaders = GetReadersAndWritersForMessages();
            _socket = socket;
            _logger = logger;
            _recvCounter = int.MinValue;

            _isServer = serverMessenger;
        }

        public void RegisterDelegateInternal(string msgName, Action<JettReader> readDelegate) => RegisterDelegateInternal(msgName.ToID(), readDelegate);

        public void RegisterDelegateInternal(int msgId, Action<JettReader> readDelegate)
        {
            _messageHandlers[msgId] = (reader) =>
            {
                try
                {
                    readDelegate.Invoke(reader);
                }
                catch
                {
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {msgId}", LogLevel.Error);
                }
            };
        }

        public void SendDelegateToClient(int msgId, int connectionId, Action<JettWriter> writeDelegate, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get())
            {
                writer.WriteByte((byte)Messages.Message);
                writer.WriteInt(msgId);
                writeDelegate.Invoke(writer);

                _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connectionId, channel);
            }
        }

        public void SendDelegateToServer(int msgId, Action<JettWriter> writeDelegate, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get())
            {
                writer.WriteByte((byte)Messages.Message);
                writer.WriteInt(msgId);
                writeDelegate.Invoke(writer);

                _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
            }
        }

        public void SendToClient(IJettMessage msg, int connectionId, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get())
            {
                writer.WriteByte((byte)Messages.Message);
                writer.WriteInt(msg.GetType().Name.ToID());
                msg.Serialize(writer);

                _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connectionId, channel);
            }
        }

        public void SendToServer(IJettMessage msg, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get())
            {
                writer.WriteByte((byte)Messages.Message);
                writer.WriteInt(msg.GetType().Name.ToID());
                msg.Serialize(writer);

                //int serialNumber = _recvCounter;

                //_pendingReceiveCallbacks.Add(serialNumber, msg.MessageReceived);

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
            int id = typeof(T).Name.ToID();

            _messageHandlers[id] = (reader) =>
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
        }

        public void HandleIncomingMessage(JettReader reader)
        {
            int messageId = reader.ReadInt();

            _messageHandlers[messageId].Invoke(reader);
        }
    }
}
