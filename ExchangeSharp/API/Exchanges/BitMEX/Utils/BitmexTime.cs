using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp.API.Exchanges.BitMEX.Utils
{
    public static class BitmexTime
    {
        public static readonly DateTime UnixBase = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long NowMs()
        {
            var substracted = DateTime.UtcNow.Subtract(UnixBase);
            return (long)substracted.TotalMilliseconds;
        }

        public static long NowTicks()
        {
            return DateTime.UtcNow.Ticks - UnixBase.Ticks;
        }

        public static DateTime ConvertToTime(long timeInMs)
        {
            return UnixBase.AddMilliseconds(timeInMs);
        }
    }
}
