using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.API.Common;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public class ExchangeBTCMarketsAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.btcmarkets.net";
        public override string BaseUrlWebSocket { get; set; } = "https://socket.btcmarkets.net";
        public override string Name => ExchangeName.BTCMarkets;

        // start date is not supported.
        // tickers are in descending order, so most recent ticker appears first
        protected override async Task OnGetHistoricalTickersAsync(
            Func<IEnumerable<ExchangeTicker>, bool> callback,
            string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            var gSymbol = ExchangeMarketSymbolToGlobalMarketSymbolWithSeparator(symbol);
            var split = gSymbol.Split(GlobalMarketSymbolSeparator);
            var instrument = split[1];
            var currency = split[0];

            var url = $@"/v2/market/{instrument}/{currency}/tickByTime/day";

            if (endDate != null)
            {
                var sinceMilliseconds = new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds();
                url += $@"?since={sinceMilliseconds}";
            }

            var allTickers = new List<ExchangeTicker>();

            while (true)
            {
                var tickers = new List<ExchangeTicker>();
                JToken obj = await MakeJsonRequestAsync<JToken>(url);

                foreach (JToken child in obj["ticks"])
                {
                    var ticker = ParseHistoricalTicker(instrument, currency, child);
                    if (allTickers.FirstOrDefault(x => x.Volume.Timestamp.Equals(ticker.Volume.Timestamp)) != null)
                    {
                        continue;
                    }

                    tickers.Add(ticker);
                    allTickers.Add(ticker);
                }

                url = obj["paging"]["older"].ToStringInvariant();

                if (string.IsNullOrEmpty(url.ToStringInvariant())
                    || tickers.Count == 0
                    || !callback(tickers))
                {
                    return;
                }
            }
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            var markets = GetMarketSymbolsMetadataAsync().GetAwaiter().GetResult();
            var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (var market in markets)
            {
                var ticker = await OnGetTickersAsync(market.BaseCurrency, market.QuoteCurrency);
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(market.MarketSymbol, ticker));
            }

            return tickers;
        }

        protected async Task<ExchangeTicker> OnGetTickersAsync(string instrument, string currency)
        {
            string symbol;
            JToken obj = await MakeJsonRequestAsync<JToken>($@"/market/{instrument}/{currency}/tick");
            symbol = obj["symbol"].ToStringInvariant();
            return ParseTicker(obj);
        }

        protected override IWebSocket OnGetTickersWebSocket(
            Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            if (callback == null)
            {
                return null;
            }

            IEnumerable<ExchangeMarket> markets = GetMarketSymbolsMetadataAsync().GetAwaiter().GetResult();
            foreach (var market in markets)
            {
                OnGetTickersWebSocket((ExchangeTicker ticker) =>
                {
                    var tickerList = new List<KeyValuePair<string, ExchangeTicker>>
                    {
                        new KeyValuePair<string, ExchangeTicker>(ticker.Volume.BaseCurrency, ticker)
                    };
                    callback(tickerList);
                }, market.BaseCurrency, market.QuoteCurrency);
            }
            
            return new IoSocketWrapper(); // TODO: implement wrapper
        }


        protected override IWebSocket OnGetTickersWebSocket(
            Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
        {
            return OnGetTickersWebSocket(callback);
        }

        protected override IWebSocket OnGetTickersWebSocket(
            Action<ExchangeTicker> callback,
            string baseCurrency, string quoteCurrency)
        {
            if (callback == null)
            {
                return null;
            }
            
            var eventName = "newTicker";
            var channelName = "Ticker-BTCMarkets-" + baseCurrency + "-" + quoteCurrency;
            var socket = ConnectIoSocket(BaseUrlWebSocket, eventName, channelName, (data) =>
            {
                JToken token = JToken.FromObject(data);
                ExchangeTicker ticker = ParseTicker(token);

                if (ticker != null)
                {
                    callback(ticker);
                }
            });

            return socket;
        }

        public ExchangeTicker ParseHistoricalTicker(string instrument, string currency, JToken token)
        {
            var symbol = instrument + "-" + currency;
            var baseVolume = token["volume"].ConvertInvariant<decimal>() * 0.00000001m;
            var ask = token["close"].ConvertInvariant<decimal>() * 0.00000001m;
            var bid = token["close"].ConvertInvariant<decimal>() * 0.00000001m;
            var ticker = new ExchangeTicker()
            {
                Ask = ask,
                Bid = bid,
                Last = ask,
                Volume = new ExchangeVolume
                {
                    BaseCurrencyVolume = baseVolume,
                    BaseCurrency = symbol,
                    QuoteCurrencyVolume = baseVolume * (ask + bid) / 2m,
                    QuoteCurrency = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["timestamp"]
                        .ConvertInvariant<long>())
                }
            };

            return ticker;
        }

        public ExchangeTicker ParseTicker(JToken token)
        {
            var baseCurrency = ExchangeCurrencyToGlobalCurrency(token["instrument"].ToStringInvariant());
            var quoteCurrency = ExchangeCurrencyToGlobalCurrency(token["currency"].ToStringInvariant());

            var baseVolume = token["volume24h"].ConvertInvariant<decimal>() * 0.00000001m;
            var ask = token["bestAsk"].ConvertInvariant<decimal>() * 0.00000001m;
            var bid = token["bestBid"].ConvertInvariant<decimal>() * 0.00000001m;
            var ticker = new ExchangeTicker()
            {
                Ask = ask,
                Bid = bid,
                Last = token["lastPrice"].ConvertInvariant<decimal>() * 0.00000001m,
                Volume = new ExchangeVolume
                {
                    BaseCurrencyVolume = baseVolume,
                    BaseCurrency = baseCurrency,
                    QuoteCurrencyVolume = baseVolume * (ask + bid) / 2m,
                    QuoteCurrency = quoteCurrency,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["timestamp"]
                        .ConvertInvariant<long>())
                }
            };

            return ticker;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            /*
 {
    "success": true,
    "errorCode": null,
    "errorMessage": null,
    "markets": [
        {
            "instrument": "BTC",
            "currency": "AUD"
        },
        {
            "instrument": "LTC",
            "currency": "AUD"
        }
    ]
}
             */
            var markets = new List<ExchangeMarket>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/v2/market/active");

            foreach (JToken child in obj["markets"])
            {
                var instrument = child["instrument"].ToStringUpperInvariant();
                var currency = child["currency"].ToStringUpperInvariant();
                var market = new ExchangeMarket
                {
                    MarketSymbol = instrument + "-" + currency,
                    IsActive = true,
                    QuoteCurrency = currency,
                    BaseCurrency = instrument
                };
                markets.Add(market);
            }

            return markets;
        }

        public override string ExchangeMarketSymbolToGlobalMarketSymbol(string symbol)
        {
            var split = symbol.Split('-');
            return split[1] + GlobalMarketSymbolSeparator + split[0];
        }

        public partial class ExchangeName
        {
            public const string BTCMarkets = "BTCMarkets";
        }
    }
}
