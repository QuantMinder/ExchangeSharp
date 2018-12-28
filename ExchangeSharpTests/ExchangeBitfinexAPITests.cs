using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    [TestClass]
    public class ExchangeBitfinexAPITests
    {
        [TestMethod]
        public void TestTickersWebsocket()
        {
            var api = new ExchangeBitfinexAPI();
            var symbols = api.GetMarketSymbolsAsync().GetAwaiter().GetResult();
            Assert.IsNotNull(symbols);
        }
    }
}
