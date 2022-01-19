using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using jettnet.logging;

namespace jettnet
{
    public class Messenger
    {
        private readonly Logger _logger;

        private readonly Dictionary<int, Action<JettReader, ConnectionData>> _messageHandlers =
            new Dictionary<int, Action<JettReader, ConnectionData>>();

        private readonly Dictionary<Type, IJettMessage> _messageReaders;

        private readonly Socket _socket;

        public Messenger(Socket socket, Logger logger, params string[] extraMessageAssemblies)
        {
            _messageReaders = GetReadersAndWritersForMessages(extraMessageAssemblies);
            _socket         = socket;
            _logger         = logger;

#if UNITY_64
            UnityEngine.Application.runInBackground = true;
#endif
        }

        #region Sending

        public void SendManyMessages(IJettMessage msg, IEnumerable<int> connIds, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.Message))
            {
                SerializeMessage(msg, writer);

                ArraySegment<byte> payload = new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position);

                using (var enumerator = connIds.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        _socket.ServerSend(payload, enumerator.Current, channel);
                    }
                }
            }
        }

        public void SendManyDelegates(int msgId, Action<JettWriter> writeDelegate, IEnumerable<int> connIds, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.Message))
            {
                SerializeDelegate(msgId, writeDelegate, writer);

                ArraySegment<byte> payload = new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position);

                using (var enumerator = connIds.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        _socket.ServerSend(payload, enumerator.Current, channel);
                    }
                }
            }
        }

        public void SendDelegate(int msgId, Action<JettWriter> writeDelegate, bool isServer, int connId, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.Message))
            {
                SerializeDelegate(msgId, writeDelegate, writer);

                if (!isServer)
                    _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
                else
                    _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connId,
                                       channel);
            }
        }

        public void SendMessage(IJettMessage msg, int connId, bool isServer, int channel = JettChannels.Reliable)
        {
            using (PooledJettWriter writer = JettWriterPool.Get(JettHeader.Message))
            {
                SerializeMessage(msg, writer);

                if (!isServer)
                    _socket.ClientSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), channel);
                else
                    _socket.ServerSend(new ArraySegment<byte>(writer.Buffer.Array, 0, writer.Position), connId,
                                       channel);
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
                    Type type = typeof(T);

                    T msg = ((IJettMessage<T>) _messageReaders[type]).Deserialize(reader);

                    handler.Invoke(msg, connData);
                }
                catch (Exception e)
                {
                    _logger.Log(
                                $"Failed to deserialize and invoke the handler for message: {typeof(T).Name}, Exception: {e}",
                                LogLevel.Error);
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
                    _logger.Log($"Failed to deserialize and invoke the handler for message: {msgId}, Exception: {e}",
                                LogLevel.Error);
                }
            };
        }

        #endregion Registering

        #region Handlers

        private static void SerializeDelegate(int msgId, Action<JettWriter> writeDelegate, PooledJettWriter writer)
        {
            // write msg id
            writer.WriteInt(msgId);

            // write user data
            writeDelegate.Invoke(writer);
        }

        private void SerializeMessage(IJettMessage msg, PooledJettWriter writer)
        {
            // id
            writer.WriteInt(msg.GetType().Name.ToID());

            // user data
            msg.Serialize(writer);
        }

        private static Dictionary<Type, IJettMessage> GetReadersAndWritersForMessages(params string[] extraAssemblies)
        {
            Type type = typeof(IJettMessage);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            for (int i = 0; i < extraAssemblies.Length; i++)
            {
                string   item = extraAssemblies[i];
                Assembly asm  = Assembly.Load(item);

                assemblies.Add(asm);
            }

            var types = assemblies
                        .SelectMany(s => s.GetTypes())
                        .Where(x => type.IsAssignableFrom(x)).ToList();

            var foundPairs = new Dictionary<Type, IJettMessage>();

            foreach (Type item in types)
            {
                // ignore the default interface and the generic interfaces
                if (item.ContainsGenericParameters || item == type)
                    continue;

                // unity handles loading differently
                // so this prevents adding the same type twice
                if (foundPairs.ContainsKey(item))
                    continue;

                IJettMessage instance = Activator.CreateInstance(item) as IJettMessage;

                foundPairs.Add(item, instance);
            }

            return foundPairs;
        }

        public void HandleIncomingMessage(JettReader reader, ConnectionData data)
        {
            int messageId = reader.ReadInt();

            if (_messageHandlers.TryGetValue(messageId, out var handler))
            {
                handler.Invoke(reader, data);
            }
            else
            {
                _logger.Log("Client sent invalid data, removing them from server...", LogLevel.Error);
                _socket.DisconnectClient(data.ClientId);
            }
        }

        #endregion Handlers
    }
}