using jettnet.logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace jettnet
{
    public class JettMessenger
    {
        private Dictionary<int, Action<JettReader, ConnectionData>> _messageHandlers = new Dictionary<int, Action<JettReader, ConnectionData>>();
        private Dictionary<Type, IJettMessage> _messageReaders;

        private ConcurrentQueue<Action> _clientCallbackQueue = new ConcurrentQueue<Action>();
        private ConcurrentQueue<ServerCallback> _serverCallbackQueue = new ConcurrentQueue<ServerCallback>();
        private ConcurrentQueue<MsgHandlerCallback> _msgHandlerCallbackQueue = new ConcurrentQueue<MsgHandlerCallback>();

        private Dictionary<int, MsgResponseCallback> _pendingResponseCallbacks = new Dictionary<int, MsgResponseCallback>();
        private ConcurrentQueue<MsgResponseCallback> _msgResponseQueue = new ConcurrentQueue<MsgResponseCallback>();

        private Socket _socket;
        private Logger _logger;
        private Counter _counter;

        private bool _isServer;

        public JettMessenger(Socket socket, Logger logger, bool isServer, params string[] messageAsms)
        {
            _messageReaders = GetReadersAndWritersForMessages(messageAsms);
            _socket = socket;
            _logger = logger;
            _isServer = isServer;
            _counter = new Counter();

#if UNITY_64
            UnityEngine.Application.runInBackground = true;
#endif
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

            while (_msgResponseQueue.TryDequeue(out MsgResponseCallback cb))
                cb.ResponseCallback.Invoke();
        }

        public void QueueClientCallback(Action cb) => _clientCallbackQueue.Enqueue(cb);

        public void QueueServerCallback(ServerCallback cb) => _serverCallbackQueue.Enqueue(cb);

        #region Sending

        public void SendDelegate(int msgId, Action<JettWriter> writeDelegate, bool isServer, int connId, Action responseCallback = null, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.Message))
            {
                SerializeDelegate(msgId, writeDelegate, writer, responseCallback);

                if (!isServer)
                    _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
                else
                    _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connId, channel);
            }
        }

        public void SendMessage(IJettMessage msg, int connectionId, bool isServer, Action responseCallback = null, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.Message))
            {
                SerializeMessage(msg, writer, responseCallback);

                if (!isServer)
                    _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
                else
                    _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connectionId, channel);
            }
        }

        private void HandleResponse(JettReader reader, int connId)
        {
            bool hasCallback = reader.ReadBool();

            if (hasCallback)
            {
                int serialNumber = reader.ReadInt();

                // tell peer we received their msg
                using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.MessageReceived))
                {
                    writer.WriteInt(serialNumber);

                    if (!_isServer)
                        _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), JettChannels.Reliable);
                    else
                        _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connId, JettChannels.Reliable);
                }
            }
        }

        #endregion Sending

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
                catch (Exception e)
                {
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {typeof(T).Name}, Exception: {e}", LogLevel.Error);
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
                catch (Exception e)
                {
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {msgId}, Exception: {e}", LogLevel.Error);
                }
            };
        }

        #endregion Registering

        #region Handlers

        private void SerializeDelegate(int msgId, Action<JettWriter> writeDelegate, PooledJettWriter writer, Action response)
        {
            bool hasCallback = response != null;

            // write msg id
            writer.WriteInt(msgId);

            // write user data
            writeDelegate.Invoke(writer);

            writer.WriteBool(hasCallback);

            if (hasCallback)
            {
                int serialNumber = _counter.Next();

                _pendingResponseCallbacks.Add(serialNumber, new MsgResponseCallback { ResponseCallback = response });

                writer.WriteInt(serialNumber);
            }
        }

        private void SerializeMessage(IJettMessage msg, PooledJettWriter writer, Action response)
        {
            bool hasCallback = response != null;

            // id
            writer.WriteInt(msg.GetType().Name.ToID());

            // user data
            msg.Serialize(writer);

            writer.WriteBool(hasCallback);

            if (hasCallback)
            {
                int serialNumber = _counter.Next();

                _pendingResponseCallbacks.Add(serialNumber, new MsgResponseCallback { ResponseCallback = response });

                writer.WriteInt(serialNumber);
            }
        }

        private static Dictionary<Type, IJettMessage> GetReadersAndWritersForMessages(params string[] extraAsms)
        {
            var type = typeof(IJettMessage);

            List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            foreach (var item in extraAsms)
            {
                var asm = Assembly.Load(item);

                if (item != null)
                    assemblies.Add(asm);
            }

            var types = assemblies
                .SelectMany(s => s.GetTypes())
                .Where(x => type.IsAssignableFrom(x)).ToList();

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

            HandleResponse(reader, data.ClientId);
        }

        public void HandleIncomingResponseMessage(JettReader reader, ConnectionData data)
        {
            int serialNumber = reader.ReadInt();

            var response = _pendingResponseCallbacks[serialNumber];

            _msgResponseQueue.Enqueue(response);
        }

        #endregion Handlers
    }
}