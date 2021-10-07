using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace jettnet
{
    public class JettMessenger
    {
        private Dictionary<int, Action<IJettMessage>> _messageHandlers = new Dictionary<int, Action<IJettMessage>>();
        private Queue<IJettMessage> _pendingMessages = new Queue<IJettMessage>();
        private static MD5 _crypto = MD5.Create();
        private Socket _socket;

        public JettMessenger(Socket socket) 
        {
            _socket = socket;
        }

        public void RegisterInternal<T>(Action<IJettMessage> handler) where T : struct, IJettMessage
        {
            short id = GetMessageId(nameof(T));
            _messageHandlers[id] = handler;        
        }

        private short GetMessageId(string funcName)
        { 
            var result = _crypto.ComputeHash(Encoding.UTF8.GetBytes(funcName));
            return BitConverter.ToInt16(result, 0);
        }
    }
}
