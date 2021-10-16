using jettnet.logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace jettnet
{
    public class JettMessenger
    {
        private Dictionary<int, Action<JettReader, ConnectionData>> _messageHandlers = new Dictionary<int, Action<JettReader, ConnectionData>>();
        private Dictionary<int, Action> _pendingReceiveCallbacks = new Dictionary<int, Action>();
        private Dictionary<Type, IJettMessage> _messageReaders;

        private ConcurrentQueue<Action> _clientCallbackQueue = new ConcurrentQueue<Action>();
        private ConcurrentQueue<ServerCallback> _serverCallbackQueue = new ConcurrentQueue<ServerCallback>();
        private ConcurrentQueue<MsgHandlerCallback> _msgHandlerCallbackQueue = new ConcurrentQueue<MsgHandlerCallback>();

        private Socket _socket;
        private Logger _logger;

        private int _recvCounter { get { _counter++; return _counter; } set { _counter = value; } }
        private int _counter;

        private bool _isServer;

        public JettMessenger(Socket socket, Logger logger, bool isServer)
        {
            _messageReaders = GetReadersAndWritersForMessages();
            _socket = socket;
            _logger = logger;
            _recvCounter = int.MinValue;
            _isServer = isServer;
        }

        public void InvokeCallbacks()
        {
            if (!_isServer) 
            {
                while (_clientCallbackQueue.TryDequeue(out Action a))
                    a.Invoke();
            }
            else
            {
                while (_serverCallbackQueue.TryDequeue(out ServerCallback cb))
                    cb.Method.Invoke(cb.Data);
            }

            while (_msgHandlerCallbackQueue.TryDequeue(out MsgHandlerCallback cb))
                cb.Handler.Invoke(cb.Reader, cb.Data);
        }

        public void QueueClientCallback(Action cb) => _clientCallbackQueue.Enqueue(cb);
        public void QueueServerCallback(ServerCallback cb) => _serverCallbackQueue.Enqueue(cb);

        #region Sending

        public void SendDelegate(int msgId, Action<JettWriter> writeDelegate, bool isServer, int connId, Action recvCallback = null, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(Messages.Message))
            {
                SerializeDelegate(msgId, writeDelegate, writer, recvCallback);

                if (!isServer)
                    _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
                else
                    _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connId, channel);
            }
        }

        public void SendMessage(IJettMessage msg, int connectionId, bool isServer, Action recvCallback = null, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(Messages.Message))
            {
                SerializeMessage(msg, writer, recvCallback);

                if (!isServer)
                    _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
                else
                    _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connectionId, channel);
            }
        }

        private void HandleRecvCallback(JettReader reader, int connId)
        {
            bool hasCallback = reader.ReadBool();

            if (hasCallback)
            {
                int serialNumber = reader.ReadInt();

                // tell peer we received their msg
                using(PooledJettWriter writer = JettWriterPool.Get(Messages.MessageReceived))
                {
                    writer.WriteInt(serialNumber);

                    if (!_isServer)
                        _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), JettChannels.Reliable);
                    else
                        _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connId, JettChannels.Reliable);
                }
            }
        }

        #endregion

        #region Registering

        public void RegisterInternal<T>(Action<T, ConnectionData> handler) where T : struct, IJettMessage
        {
            int id = typeof(T).Name.ToID();

            _messageHandlers[id] = (reader, connData) =>
            {
                try
                {
                    var type = typeof(T);

                    T msg = (_messageReaders[type] as IJettMessage<T>).Deserialize(reader);

                    handler.Invoke(msg, connData);
                }
                catch
                {
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {nameof(T)}", LogLevel.Error);
                }
            };
        }

        public void RegisterDelegateInternal(string msgName, Action<JettReader, ConnectionData> readDelegate)
        {
            RegisterDelegateInternal(msgName.ToID(), readDelegate);
        }

        public void RegisterDelegateInternal(int msgId, Action<JettReader, ConnectionData> readDelegate)
        {
            _messageHandlers[msgId] = (reader, connData) =>
            {
                try
                {
                    readDelegate.Invoke(reader, connData);
                }
                catch
                {
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {msgId}", LogLevel.Error);
                }
            };
        }

        #endregion

        #region Handlers

        private void SerializeDelegate(int msgId, Action<JettWriter> writeDelegate, PooledJettWriter writer, Action recvCallback)
        {
            bool hasCallback = recvCallback != null;

            // write msg id
            writer.WriteInt(msgId);
            
            // write user data
            writeDelegate.Invoke(writer);
            
            // write callback
            writer.WriteBool(hasCallback);

            if (hasCallback)
            {
                int serialNumber = _recvCounter;
                _pendingReceiveCallbacks.Add(serialNumber, recvCallback);

                writer.WriteInt(serialNumber);
            }
        }

        private void SerializeMessage(IJettMessage msg, PooledJettWriter writer, Action recvCallback)
        {
            bool hasCallback = recvCallback != null;

            // id
            writer.WriteInt(msg.GetType().Name.ToID());
            
            // user data
            msg.Serialize(writer);

            // cb
            writer.WriteBool(hasCallback);

            if (hasCallback)
            {
                int serialNumber = _recvCounter;
                _pendingReceiveCallbacks.Add(serialNumber, recvCallback);

                writer.WriteInt(serialNumber);
            }
        }

        public void HandleIncomingMessageReceived(JettReader reader)
        {
            int serialNumber = reader.ReadInt();

            if(_pendingReceiveCallbacks.TryGetValue(serialNumber, out Action cb))
            {
                cb?.Invoke();

                _pendingReceiveCallbacks.Remove(serialNumber);
            }
            else
            {
                _logger.Log("Received msg recieved but we have no handler for it! SN: " + serialNumber, LogLevel.Warning);
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

        public void HandleIncomingMessage(JettReader reader, ConnectionData data)
        {
            int messageId = reader.ReadInt();

            var msgHandler = _messageHandlers[messageId];

            _msgHandlerCallbackQueue.Enqueue(new MsgHandlerCallback { Handler = msgHandler, Data = data, Reader = reader });

            HandleRecvCallback(reader, data.ClientId);
        }

        #endregion
    }
}
