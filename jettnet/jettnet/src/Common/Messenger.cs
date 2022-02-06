using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using jettnet.core;
using jettnet.mirage.bitpacking;

namespace jettnet
{
    public class Messenger
    {
        private readonly Logger _logger;

        private readonly Dictionary<int, Action<JettReader, JettConnection>> _messageHandlers =
            new Dictionary<int, Action<JettReader, JettConnection>>();

        private readonly Dictionary<Type, IJettMessage> _messageReaders;

        private readonly Socket _socket;

        public readonly JettWriterPool WriterPool;
        public readonly JettReaderPool ReaderPool;

        public Messenger(Socket socket, Logger logger, params string[] extraMessageAssemblies)
        {
            _messageReaders = GetReadersAndWritersForMessages(extraMessageAssemblies);
            _socket         = socket;
            _logger         = logger;
            
            WriterPool = new JettWriterPool(logger);
            ReaderPool = new JettReaderPool(logger);
            
#if UNITY_64
            UnityEngine.Application.runInBackground = true;
#endif
        }

        #region Sending

        public void SendManyMessages(IJettMessage msg, IEnumerable<int> connIds, int channel = JettConstants.ReliableChannel)
        {
            using (PooledJettWriter writer = WriterPool.Get())
            {
                SerializeMessage(msg, writer);

                ArraySegment<byte> payload = writer.ToArraySegment();

                using (var enumerator = connIds.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        _socket.ServerSend(payload, enumerator.Current, channel);
                    }
                }
            }
        }

        public void SendManyDelegates(int msgId, Action<JettWriter> writeDelegate, IEnumerable<int> connIds, int channel = JettConstants.ReliableChannel)
        {
            using (PooledJettWriter writer = WriterPool.Get())
            {
                SerializeDelegate(msgId, writeDelegate, writer);

                ArraySegment<byte> payload = writer.ToArraySegment();

                using (var enumerator = connIds.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        _socket.ServerSend(payload, enumerator.Current, channel);
                    }
                }
            }
        }

        public void SendDelegate(int msgId, Action<JettWriter> writeDelegate, bool isServer, int connId, int channel = JettConstants.ReliableChannel)
        {
            using (PooledJettWriter writer = WriterPool.Get())
            {
                SerializeDelegate(msgId, writeDelegate, writer);
                
                ArraySegment<byte> payload = writer.ToArraySegment();

                if (!isServer)
                    _socket.ClientSend(payload, channel);
                else
                    _socket.ServerSend(payload, connId, channel);
            }
        }

        public void SendMessage(IJettMessage msg, int connId, bool isServer, int channel = JettConstants.ReliableChannel)
        {
            using (PooledJettWriter writer = WriterPool.Get())
            {
                SerializeMessage(msg, writer);
                
                ArraySegment<byte> payload = writer.ToArraySegment();

                if (!isServer)
                    _socket.ClientSend(payload, channel);
                else
                    _socket.ServerSend(payload, connId, channel);
            }
        }

        #endregion Sending

        #region Registering

        public void RegisterInternal<T>(Action<T, JettConnection> handler) where T : struct, IJettMessage
        {
            Type type = typeof(T);
            int  id   = type.FullName.ToId();

            _messageHandlers[id] = (reader, connData) =>
            {
                try
                {
                    T msg = ((IJettMessage<T>) _messageReaders[type]).Deserialize(reader);

                    handler.Invoke(msg, connData);
                }
                catch (Exception e)
                {
                    _logger.Log(
                                $"Failed to deserialize and invoke the handler for message: {type.Name}, Exception: {e}",
                                LogLevel.Error);
                }
            };
        }

        public void RegisterDelegateInternal(string msgName, Action<JettReader, JettConnection> readDelegate)
        {
            RegisterDelegateInternal(msgName.ToId(), readDelegate);
        }

        public void RegisterDelegateInternal(int msgId, Action<JettReader, JettConnection> readDelegate)
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
            writer.WriteInt32(msgId);

            // write user conn
            writeDelegate.Invoke(writer);
        }

        private void SerializeMessage(IJettMessage msg, PooledJettWriter writer)
        {
            // id
            writer.WriteInt32(msg.GetType().FullName.ToId());

            // user conn
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

        public void HandleIncomingMessage(JettReader reader, JettConnection conn)
        {
            int messageId = reader.ReadInt32();

            if (_messageHandlers.TryGetValue(messageId, out var handler))
            {
                handler.Invoke(reader, conn);
            }
            else
            {
                _logger.Log("Client sent invalid conn, removing them from server...", LogLevel.Error);
                _socket.DisconnectClient(conn);
            }
        }

        #endregion Handlers
    }
}