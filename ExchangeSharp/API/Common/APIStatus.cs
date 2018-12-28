using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp
{
    public class APIStatus
    {
        public string Key { get; set; }
        public DateTime LastThrottled { get; set; }
        public DateTime LastDispatched { get; set; }
        public List<DateTime> Counters { get; set; }

        public APIStatus()
        {
            this.Counters = new List<DateTime>();
        }
    }
}
