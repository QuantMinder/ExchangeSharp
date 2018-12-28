using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp.API.Common
{
    public class IoSocketWrapper : IWebSocket
    {
        public event Action<IWebSocket> Connected;
        public event Action<IWebSocket> Disconnected;
        private bool disposed;

        public TimeSpan ConnectInterval { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan KeepAlive { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        event WebSocketConnectionDelegate IWebSocket.Connected
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event WebSocketConnectionDelegate IWebSocket.Disconnected
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public void Dispose()
        {
            disposed = true;

        }

        public Task<bool> SendMessageAsync(object message)
        {
            throw new NotImplementedException();
        }
    }
}
