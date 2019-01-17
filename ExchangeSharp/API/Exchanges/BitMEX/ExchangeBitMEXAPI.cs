/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using ExchangeSharp.API.Exchanges.BitMEX.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public partial class ExchangeBitMEXAPI : ExchangeAPI
    {
        static ExchangeBitMEXAPI()
        {
            ExchangeGlobalCurrencyReplacements[typeof(ExchangeBitMEXAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("XBT", "BTC"),
                new KeyValuePair<string, string>("XBt", "BTC")
            };
        }

        private static decimal satoshis = 100000000;

        public bool UseWebSocket = true;
        public override string BaseUrl { get; set; } = "https://www.bitmex.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://www.bitmex.com/realtime";
        //public override string BaseUrl { get; set; } = "https://testnet.bitmex.com/api/v1";
        //public override string BaseUrlWebSocket { get; set; } = "wss://testnet.bitmex.com/realtime";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();

        List<ExchangeTransaction> depositHistory = new List<ExchangeTransaction>();
        List<ExchangeTransaction> withdrawalHistory = new List<ExchangeTransaction>();
        List<ExchangeMarket> exchangeMarkets = new List<ExchangeMarket>();

        public ExchangeBitMEXAPI()
        {
            RequestWindow = TimeSpan.Zero;
            NonceStyle = NonceStyle.ExpiresUnixSeconds;

            // make the nonce go 10 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
            NonceOffset = TimeSpan.FromSeconds(-60.0);

            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;

            RateLimit = new RateGate(300, TimeSpan.FromMinutes(5));
        }

        public override string ExchangeMarketSymbolToGlobalMarketSymbol(string marketSymbol)
        {
            if (this.exchangeMarkets.Count == 0)
            {
                OnGetMarketSymbolsMetadataAsync().GetAwaiter().GetResult();
            }

            var market = this.exchangeMarkets.Where(x => { return x.MarketSymbol.ToUpper() == marketSymbol.ToUpper(); }).FirstOrDefault();
            if (market == null)
            {
                return marketSymbol;
            }

            var baseCurrency = ExchangeGlobalCurrencyReplacements[typeof(ExchangeBitMEXAPI)]
                .FirstOrDefault(x => x.Key == market.BaseCurrency).Value ?? market.BaseCurrency;

            var quoteCurrency = ExchangeGlobalCurrencyReplacements[typeof(ExchangeBitMEXAPI)]
                                   .FirstOrDefault(x => x.Key == market.QuoteCurrency).Value ?? market.QuoteCurrency;

            return quoteCurrency + GlobalMarketSymbolSeparator + baseCurrency;
        }

        public override string GlobalMarketSymbolToExchangeMarketSymbol(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");
                var msg = CryptoUtility.GetJsonForPayload(payload);
                var sign = $"{request.Method}{request.RequestUri.AbsolutePath}{request.RequestUri.Query}{nonce}{msg}";
                string signature = CryptoUtility.SHA256Sign(sign, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));

                request.AddHeader("api-expires", nonce.ToStringInvariant());
                request.AddHeader("api-key", PublicApiKey.ToUnsecureString());
                request.AddHeader("api-signature", signature);

                await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
            }
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }


        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            /*
             {{
  "symbol": ".XRPXBT",
  "rootSymbol": "XRP",
  "state": "Unlisted",
  "typ": "MRCXXX",
  "listing": null,
  "front": null,
  "expiry": null,
  "settle": null,
  "relistInterval": null,
  "inverseLeg": "",
  "sellLeg": "",
  "buyLeg": "",
  "optionStrikePcnt": null,
  "optionStrikeRound": null,
  "optionStrikePrice": null,
  "optionMultiplier": null,
  "positionCurrency": "",
  "underlying": "XRP",
  "quoteCurrency": "XBT",
  "underlyingSymbol": "XRPXBT=",
  "reference": "PLNX",
  "referenceSymbol": "BTC_XRP",
  "calcInterval": null,
  "publishInterval": "2000-01-01T00:01:00Z",
  "publishTime": null,
  "maxOrderQty": null,
  "maxPrice": null,
  "lotSize": null,
  "tickSize": 1E-08,
  "multiplier": null,
  "settlCurrency": "",
  "underlyingToPositionMultiplier": null,
  "underlyingToSettleMultiplier": null,
  "quoteToSettleMultiplier": null,
  "isQuanto": false,
  "isInverse": false,
  "initMargin": null,
  "maintMargin": null,
  "riskLimit": null,
  "riskStep": null,
  "limit": null,
  "capped": false,
  "taxed": false,
  "deleverage": false,
  "makerFee": null,
  "takerFee": null,
  "settlementFee": null,
  "insuranceFee": null,
  "fundingBaseSymbol": "",
  "fundingQuoteSymbol": "",
  "fundingPremiumSymbol": "",
  "fundingTimestamp": null,
  "fundingInterval": null,
  "fundingRate": null,
  "indicativeFundingRate": null,
  "rebalanceTimestamp": null,
  "rebalanceInterval": null,
  "openingTimestamp": null,
  "closingTimestamp": null,
  "sessionInterval": null,
  "prevClosePrice": null,
  "limitDownPrice": null,
  "limitUpPrice": null,
  "bankruptLimitDownPrice": null,
  "bankruptLimitUpPrice": null,
  "prevTotalVolume": null,
  "totalVolume": null,
  "volume": null,
  "volume24h": null,
  "prevTotalTurnover": null,
  "totalTurnover": null,
  "turnover": null,
  "turnover24h": null,
  "prevPrice24h": 7.425E-05,
  "vwap": null,
  "highPrice": null,
  "lowPrice": null,
  "lastPrice": 7.364E-05,
  "lastPriceProtected": null,
  "lastTickDirection": "MinusTick",
  "lastChangePcnt": -0.0082,
  "bidPrice": null,
  "midPrice": null,
  "askPrice": null,
  "impactBidPrice": null,
  "impactMidPrice": null,
  "impactAskPrice": null,
  "hasLiquidity": false,
  "openInterest": 0,
  "openValue": 0,
  "fairMethod": "",
  "fairBasisRate": null,
  "fairBasis": null,
  "fairPrice": null,
  "markMethod": "LastPrice",
  "markPrice": 7.364E-05,
  "indicativeTaxRate": null,
  "indicativeSettlePrice": null,
  "optionUnderlyingPrice": null,
  "settledPrice": null,
  "timestamp": "2018-07-05T13:27:15Z"
}}
             */

            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken allSymbols = await MakeJsonRequestAsync<JToken>("/instrument?count=500&reverse=false");
			foreach (JToken marketSymbolToken in allSymbols)
            {
                var market = new ExchangeMarket
                {
                    MarketSymbol = marketSymbolToken["symbol"].ToStringUpperInvariant(),
                    IsActive = marketSymbolToken["state"].ToStringInvariant().EqualsWithOption("Open"),
                    QuoteCurrency = marketSymbolToken["quoteCurrency"].ToStringUpperInvariant(),
                    BaseCurrency = marketSymbolToken["underlying"].ToStringUpperInvariant(),
                };

                try
                {
                    market.PriceStepSize = marketSymbolToken["tickSize"].ConvertInvariant<decimal>();
                    market.MaxPrice = marketSymbolToken["maxPrice"].ConvertInvariant<decimal>();
                    //market.MinPrice = symbol["minPrice"].ConvertInvariant<decimal>();

                    market.MaxTradeSize = marketSymbolToken["maxOrderQty"].ConvertInvariant<decimal>();
                    //market.MinTradeSize = symbol["minQty"].ConvertInvariant<decimal>();
                    //market.QuantityStepSize = symbol["stepSize"].ConvertInvariant<decimal>();
                }
                catch
                {

                }
                markets.Add(market);
            }

            this.exchangeMarkets.AddRange(markets); // copy internally for reuse later
            return markets;
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
        {
            /*
{"table":"trade","action":"partial","keys":[],
"types":{"timestamp":"timestamp","symbol":"symbol","side":"symbol","size":"long","price":"float","tickDirection":"symbol","trdMatchID":"guid","grossValue":"long","homeNotional":"float","foreignNotional":"float"},
"foreignKeys":{"symbol":"instrument","side":"side"},
"attributes":{"timestamp":"sorted","symbol":"grouped"},
"filter":{"symbol":"XBTUSD"},
"data":[{"timestamp":"2018-07-06T08:31:53.333Z","symbol":"XBTUSD","side":"Buy","size":10000,"price":6520,"tickDirection":"PlusTick","trdMatchID":"a296312f-c9a4-e066-2f9e-7f4cf2751f0a","grossValue":153370000,"homeNotional":1.5337,"foreignNotional":10000}]}
             */

            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

				if (token["error"] != null)
				{
					Logger.Info(token["error"].ToStringInvariant());
					return Task.CompletedTask;
				}
				else if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;
                foreach (var t in data)
                {
                    var marketSymbol = t["symbol"].ToStringInvariant();
                    callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, t.ParseTrade("size", "price", "size", "timestamp", TimestampType.Iso8601, "trdMatchID")));
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
		    await _socket.SendMessageAsync(new { op = "subscribe", args = "trade" });
		}
		else
		{
		    await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "trade:" + this.NormalizeMarketSymbol(s)).ToArray() });
		}
            });
        }

        private ConcurrentDictionary<string, ExchangeOrderBook> cachedBooks = new ConcurrentDictionary<string, ExchangeOrderBook>();
        private bool orderBookWebsocketConnected = false;

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            var callback = new Action<ExchangeOrderBook>(
                delegate (ExchangeOrderBook orderBook)
                {
                    cachedBooks[orderBook.MarketSymbol] = orderBook;
                });

            var symbols = new[] {"XBTUSD", "ETHUSD"};

            if (!orderBookWebsocketConnected)
            {
                ExchangeAPIExtensions.GetFullOrderBookWebSocket(this, callback, 20, symbols);
                orderBookWebsocketConnected = true;
            }
            
            while (this.cachedBooks.Count < symbols.Length)
            {
                await Task.Delay(1000);
            }

            if (!this.cachedBooks.ContainsKey(marketSymbol))
            {
                throw new InvalidOperationException("Invalid market symbol '" + marketSymbol + "'");
            }

            return this.cachedBooks[marketSymbol];
        }

        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 25, params string[] marketSymbols)
        {
            /*
{"info":"Welcome to the BitMEX Realtime API.","version":"2018-06-29T18:05:14.000Z","timestamp":"2018-07-05T14:22:26.267Z","docs":"https://www.bitmex.com/app/wsAPI","limit":{"remaining":39}}
{"success":true,"subscribe":"orderBookL2:XBTUSD","request":{"op":"subscribe","args":["orderBookL2:XBTUSD"]}}
{"table":"orderBookL2","action":"update","data":[{"symbol":"XBTUSD","id":8799343000,"side":"Buy","size":350544}]}
             */

            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
            }
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;

                ExchangeOrderBook book = new ExchangeOrderBook();
                var price = 0m;
                var size = 0m;
                foreach (var d in data)
                {
                    var marketSymbol = d["symbol"].ToStringInvariant();
                    var id = d["id"].ConvertInvariant<long>();
                    if (d["price"] == null)
                    {
                        if (!dict_long_decimal.TryGetValue(id, out price))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        price = d["price"].ConvertInvariant<decimal>();
                        dict_long_decimal[id] = price;
                        dict_decimal_long[price] = id;
                    }

                    var side = d["side"].ToStringInvariant();

                    if (d["size"] == null)
                    {
                        size = 0m;
                    }
                    else
                    {
                        size = d["size"].ConvertInvariant<decimal>();
                    }

                    var depth = new ExchangeOrderPrice { Price = price, Amount = size };

                    if (side.EqualsWithOption("Buy"))
                    {
                        book.Bids[depth.Price] = depth;
                    }
                    else
                    {
                        book.Asks[depth.Price] = depth;
                    }
                    book.MarketSymbol = marketSymbol;
                }

                if (!string.IsNullOrEmpty(book.MarketSymbol))
                {
                    callback(book);
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                // limit to 25 orderbook entries
                await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "orderBookL2_25:" + this.NormalizeMarketSymbol(s)).ToArray() });
            });
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            /*
             [
{"timestamp":"2017-01-01T00:00:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.29,"low":968.29,"close":968.29,"trades":0,"volume":0,"vwap":null,"lastSize":null,"turnover":0,"homeNotional":0,"foreignNotional":0},
{"timestamp":"2017-01-01T00:01:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.76,"low":968.49,"close":968.7,"trades":17,"volume":12993,"vwap":968.72,"lastSize":2000,"turnover":1341256747,"homeNotional":13.412567469999997,"foreignNotional":12993},
             */

            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = PeriodSecondsToString(periodSeconds);
            string url = $"/trade/bucketed?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
            if (startDate != null)
            {
                url += "&startTime=" + startDate.Value.ToString("yyyy-MM-dd");
            }
            if (endDate != null)
            {
                url += "&endTime=" + endDate.Value.ToString("yyyy-MM-dd");
            }
            if (limit != null)
            {
                url += "&count=" + (limit.Value.ToStringInvariant());
            }

            var obj = await MakeJsonRequestAsync<JToken>(url);
            foreach (var t in obj)
            {
                candles.Add(this.ParseCandle(t, marketSymbol, periodSeconds, "open", "high", "low", "close", "timestamp", TimestampType.Iso8601, "volume", "turnover", "vwap"));
            }
            candles.Reverse();

            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            /*
{[
  {
    "account": 93592,
    "currency": "XBt",
    "riskLimit": 1000000000000,
    "prevState": "",
    "state": "",
    "action": "",
    "amount": 141755795,
    "pendingCredit": 0,
    "pendingDebit": 0,
    "confirmedDebit": 0,
    "prevRealisedPnl": 0,
    "prevUnrealisedPnl": 0,
    "grossComm": 0,
    "grossOpenCost": 0,
    "grossOpenPremium": 0,
    "grossExecCost": 0,
    "grossMarkValue": 0,
    "riskValue": 0,
    "taxableMargin": 0,
    "initMargin": 0,
    "maintMargin": 0,
    "sessionMargin": 0,
    "targetExcessMargin": 0,
    "varMargin": 0,
    "realisedPnl": 0,
    "unrealisedPnl": 0,
    "indicativeTax": 0,
    "unrealisedProfit": 0,
    "syntheticMargin": 0,
    "walletBalance": 141755795,
    "marginBalance": 141755795,
    "marginBalancePcnt": 1,
    "marginLeverage": 0,
    "marginUsedPcnt": 0,
    "excessMargin": 141755795,
    "excessMarginPcnt": 1,
    "availableMargin": 141755795,
    "withdrawableMargin": 141755795,
    "timestamp": "2018-07-08T07:40:24.395Z",
    "grossLastValue": 0,
    "commission": null
  }
]}
             */

            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/margin?currency=all", BaseUrl, payload);
            var amounts = ParseMargin(token);
            return amounts;
        }

        public IWebSocket GetAmountsWebsocket(Action<Dictionary<string, decimal>> callback)
        {
            return SubscribeBitmexWebSocket("margin", (action, data) =>
            {
                var amounts = ParseMargin(data);
                callback(amounts);
            });
        }

        public IWebSocket SubscribeBitmexWebSocket(string topic, Action<JToken, JToken> callback)
        {
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["request"]?["op"]?.ToString() == "authKeyExpires")
                {
                    _socket.SendMessageAsync(new { op = "subscribe", args = topic }).GetAwaiter().GetResult();
                }

                if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;
                callback(action, data);
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                var authNonce = BitmexAuthentication.CreateAuthNonce();
                var authPayload = BitmexAuthentication.CreateAuthPayload(authNonce);
                var signature = BitmexAuthentication.CreateSignature(this.PrivateApiKey.ToUnsecureString(), authPayload);

                await _socket.SendMessageAsync(new
                {
                    op = "authKeyExpires",
                    args = new object[] { this.PublicApiKey.ToUnsecureString(), authNonce, signature }
                });
            });
        }

        

        public Dictionary<string, decimal> ParseMargin(IEnumerable<JToken> data)
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            foreach (var item in data)
            {
                var balance = item["marginBalance"].ConvertInvariant<decimal>() / satoshis;
                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }

            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/margin?currency=all", BaseUrl, payload);
            foreach (var item in token)
            {
                var balance = item["availableMargin"].ConvertInvariant<decimal>();
                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            //string query = "/order";
            string query = "/order?filter={\"open\": true}";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "&symbol=" + NormalizeMarketSymbol(marketSymbol);
            }
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string query = string.IsNullOrEmpty(orderId) ? $"/order?" :  $"/order?filter={{\"orderID\": \"{orderId}\"}}";
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders[0];
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(
            string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            var query = "/order?filter={\"ordStatus\": \"Filled\"}";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "&symbol=" + NormalizeMarketSymbol(marketSymbol);
            }

            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetMyTradesAsync(string marketSymbol = null,
            DateTime? afterDate = null, DateTime? beforeDate = null)
        {
            List<ExchangeOrderResult> trades = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            var query = "/execution/tradeHistory?filter={\"execType\": \"Trade\"}";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "symbol=" + NormalizeMarketSymbol(marketSymbol);
            }

            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken trade in token)
            {
                trades.Add(ParseTrade(trade));
            }

            return trades;
        }

        public IWebSocket GetMyTradesWebSocket(Action<IEnumerable<ExchangeOrderResult>> callback, 
            string symbol = null,
            DateTime? afterDate = null, DateTime? beforeDate = null)
        {
            return SubscribeBitmexWebSocket("execution", (action, data) =>
            {
                // TODO: to be implemented.
            });
        }

        private ExchangeAPIOrderResult ParseOrderStatus(string status)
        {
            switch (status)
            {
                case "PartiallyFilled":
                    return ExchangeAPIOrderResult.FilledPartially;
                case "Filled":
                    return ExchangeAPIOrderResult.Filled;
                case "New":
                    return ExchangeAPIOrderResult.Pending;
                case "PendingCancel":
                    return ExchangeAPIOrderResult.PendingCancel;
                default:
                    return ExchangeAPIOrderResult.Unknown;
            }
        }

        private ExchangeOrderResult ParseTrade(JToken token)
        {
            var status = token["ordStatus"].ToStringInvariant();
            var price = token["price"].ConvertInvariant<decimal>();
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Result = ParseOrderStatus(status),
                Amount = token["lastQty"].ConvertInvariant<decimal>() / price,
                AmountFilled = token["lastQty"].ConvertInvariant<decimal>() / price,
                Price = price,
                AveragePrice = price,
                IsBuy = token["side"].ToStringInvariant() == "Buy",
                OrderDate = token["transactTime"].ConvertInvariant<DateTime>(),
                OrderId = token["orderID"].ToStringInvariant(),
                TradeId = token["execID"].ToStringInvariant(),
                Fees = token["commission"].ConvertInvariant<decimal>(),
                FeesCurrency = token["currency"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant()
            };

            return result;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["orderID"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "DELETE");
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            AddOrderToPayload(order, payload);
            JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "POST");
            return ParseOrder(token);
        }

        protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] orders)
        {
            List<ExchangeOrderResult> results = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            List<Dictionary<string, object>> orderRequests = new List<Dictionary<string, object>>();
            foreach (ExchangeOrderRequest order in orders)
            {
                Dictionary<string, object> subPayload = new Dictionary<string, object>();
                AddOrderToPayload(order, subPayload);
                orderRequests.Add(subPayload);
            }
            payload[CryptoUtility.PayloadKeyArray] = orderRequests;
            JToken token = await MakeJsonRequestAsync<JToken>("/order/bulk", BaseUrl, payload, "POST");
            foreach (JToken orderResultToken in token)
            {
                results.Add(ParseOrder(orderResultToken));
            }
            return results.ToArray();
        }

        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["symbol"] = order.MarketSymbol;
            payload["ordType"] = order.OrderType.ToStringInvariant();
            payload["side"] = order.IsBuy ? "Buy" : "Sell";
            payload["orderQty"] = order.Amount;
            payload["price"] = order.Price;
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
{[
  {
    "orderID": "b7b8518a-c0d8-028d-bb6e-d843f8f723a3",
    "clOrdID": "",
    "clOrdLinkID": "",
    "account": 93592,
    "symbol": "XBTUSD",
    "side": "Buy",
    "simpleOrderQty": null,
    "orderQty": 1,
    "price": 5500,
    "displayQty": null,
    "stopPx": null,
    "pegOffsetValue": null,
    "pegPriceType": "",
    "currency": "USD",
    "settlCurrency": "XBt",
    "ordType": "Limit",
    "timeInForce": "GoodTillCancel",
    "execInst": "ParticipateDoNotInitiate",
    "contingencyType": "",
    "exDestination": "XBME",
    "ordStatus": "Canceled",
    "triggered": "",
    "workingIndicator": false,
    "ordRejReason": "",
    "simpleLeavesQty": 0,
    "leavesQty": 0,
    "simpleCumQty": 0,
    "cumQty": 0,
    "avgPx": null,
    "multiLegReportingType": "SingleSecurity",
    "text": "Canceled: Canceled via API.\nSubmission from testnet.bitmex.com",
    "transactTime": "2018-07-08T09:20:39.428Z",
    "timestamp": "2018-07-08T11:35:05.334Z"
  }
]}
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = token["orderQty"].ConvertInvariant<decimal>(),
                AmountFilled = token["cumQty"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                IsBuy = token["side"].ToStringInvariant().EqualsWithOption("Buy"),
                OrderDate = token["transactTime"].ConvertInvariant<DateTime>(),
                OrderId = token["orderID"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant()
            };

            // http://www.onixs.biz/fix-dictionary/5.0.SP2/tagNum_39.html
            switch (token["ordStatus"].ToStringInvariant())
            {
                case "New":
                    result.Result = ExchangeAPIOrderResult.Pending;
                    break;
                case "PartiallyFilled":
                    result.Result = ExchangeAPIOrderResult.FilledPartially;
                    break;
                case "Filled":
                    result.Result = ExchangeAPIOrderResult.Filled;
                    break;
                case "Canceled":
                    result.Result = ExchangeAPIOrderResult.Canceled;
                    break;

                default:
                    result.Result = ExchangeAPIOrderResult.Error;
                    break;
            }

            return result;
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
        {
            /*
            {[
              {
                "transactID": "00000000-0000-0000-0000-000000000000",
                "account": 158801,
                "currency": "XBt",
                "transactType": "UnrealisedPNL",
                "amount": 2910,
                "fee": 0,
                "transactStatus": "Pending",
                "address": "XBTUSD",
                "tx": "",
                "text": "",
                "transactTime": null,
                "walletBalance": 16858068,
                "marginBalance": 16860978,
                "timestamp": null
              },
              {
                "transactID": "ef6628db-5c95-74a7-05f1-346a95d8ca2d",
                "account": 158801,
                "currency": "XBt",
                "transactType": "Deposit",
                "amount": 15847649,
                "fee": null,
                "transactStatus": "Completed",
                "address": "",
                "tx": "",
                "text": "",
                "transactTime": "2018-12-28T10:08:43.942Z",
                "walletBalance": 16858225,
                "marginBalance": null,
                "timestamp": "2018-12-28T10:08:43.942Z"
              },
              {
                "transactID": "a86eb8f5-0fc8-ebc7-14bd-ffb932c55d2a",
                "account": 158801,
                "currency": "XBt",
                "transactType": "RealisedPNL",
                "amount": 13,
                "fee": 0,
                "transactStatus": "Completed",
                "address": "XBTUSD",
                "tx": "f3f04d8e-10f3-a8cc-2d97-2dc1d2cee7fe",
                "text": "",
                "transactTime": "2018-12-05T12:00:00Z",
                "walletBalance": 1000013,
                "marginBalance": null,
                "timestamp": "2018-12-05T12:00:00.261Z"
              },
              {
                "transactID": "ff6508ed-4e9b-75f5-63f8-b20297db79c5",
                "account": 158801,
                "currency": "XBt",
                "transactType": "Transfer",
                "amount": 1000000,
                "fee": null,
                "transactStatus": "Completed",
                "address": "0",
                "tx": "c309fb1a-6b6e-1e7e-1ec6-86a9b33b1a2f",
                "text": "Signup bonus",
                "transactTime": "2018-12-03T10:05:40.046Z",
                "walletBalance": 1000000,
                "marginBalance": null,
                "timestamp": "2018-12-03T10:05:40.046Z"
              }
            ]}
             */
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            var query = $@"/user/walletHistory?";
            if (!string.IsNullOrWhiteSpace(currency))
            {
                query += "currency={currency}" + NormalizeMarketSymbol(currency);
            }

            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");

            foreach (JToken transaction in token)
            {
                var txn = new ExchangeTransaction();
                txn.Currency = transaction["currency"].ToStringInvariant();
                txn.Timestamp = transaction["transactTime"].ToDateTimeInvariant();
                txn.PaymentId = transaction["transactID"].ToStringInvariant();
                txn.Amount = transaction["amount"].ConvertInvariant<decimal>() / satoshis;
                txn.TxFee = transaction["fee"].ConvertInvariant<decimal>();
                var status = transaction["transactStatus"].ToStringInvariant();
                txn.Status = status == "Completed"
                    ? TransactionStatus.Complete
                    : status == "Pending" ? TransactionStatus.Processing
                    : TransactionStatus.Unknown;

                var txnType = transaction["transactType"].ToStringInvariant();
                if (txnType == "Transfer" || txnType == "Deposit")
                {
                    depositHistory.Add(txn);
                }

                if (txnType == "Withdrawal")
                {
                    withdrawalHistory.Add(txn);
                }
                // TODO: save wallet and margin balance somewhere
            }

            return depositHistory;
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawalHistoryAsync(string currency)
        {
            return withdrawalHistory;
        }

        //private decimal GetInstrumentTickSize(ExchangeMarket market)
            //{
            //    if (market.MarketName == "XBTUSD")
            //    {
            //        return 0.01m;
            //    }
            //    return market.PriceStepSize.Value;
            //}

            //private ExchangeMarket GetMarket(string symbol)
            //{
            //    var m = GetSymbolsMetadata();
            //    return m.Where(x => x.MarketName == symbol).First();
            //}

            //private decimal GetPriceFromID(long id, ExchangeMarket market)
            //{
            //    return ((100000000L * market.Idx) - id) * GetInstrumentTickSize(market);
            //}

            //private long GetIDFromPrice(decimal price, ExchangeMarket market)
            //{
            //    return (long)((100000000L * market.Idx) - (price / GetInstrumentTickSize(market)));
            //}
        }

    public partial class ExchangeName { public const string BitMEX = "BitMEX"; }
}
