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
        private Dictionary<short, Action<JettReader>> _messageHandlers = new Dictionary<short, Action<JettReader>>();
        private Dictionary<Type, IJettMessage> _messageReaders;

        private Queue<IJettMessage> _pendingMessages = new Queue<IJettMessage>();

        private static MD5 _crypto = MD5.Create();
        
        private Socket _socket;
        private Logger _logger;

        public JettMessenger(Socket socket, Logger logger) 
        {
            _messageReaders = GetReadersAndWritersForMessages();
            _socket = socket;
            _logger = logger;
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
            short id = GetMessageId(nameof(T));

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

        private short GetMessageId(string funcName)
        { 
            var result = _crypto.ComputeHash(Encoding.UTF8.GetBytes(funcName));
            return BitConverter.ToInt16(result, 0);
        }
    }
}
