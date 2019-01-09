using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public sealed partial class ExchangeBitMEXTestAPI : ExchangeBitMEXAPI
    {
        static ExchangeBitMEXTestAPI()
        {
            ExchangeGlobalCurrencyReplacements[typeof(ExchangeBitMEXTestAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("XBT", "BTC"),
                new KeyValuePair<string, string>("XBt", "BTC")
            };
        }

        public ExchangeBitMEXTestAPI() : base()
        {
            BaseUrl = "https://testnet.bitmex.com/api/v1";
            BaseUrlWebSocket = "wss://testnet.bitmex.com/realtime";
        }
    }
}
