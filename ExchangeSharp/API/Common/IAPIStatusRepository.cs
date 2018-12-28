using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public interface IAPIStatusRepository
    {
        Task<IList<APIStatus>> FindAll();
        Task<APIStatus> FindOneByKey(string exchangeName, string key);
        Task Add(string exchangeName, APIStatus status);
        Task UpdateLastThrottledByKey(string exchangeName, string key, DateTime lastThrottled);
        Task DeleteCounterByKeyLt(string exchangeName, string key, DateTime olderThan);
        Task AddCounterByKey(string exchangeName, string key, DateTime timestamp);
        Task AddCounterByKey(string exchangeName, string key, IList<DateTime> timestamps);
        Task AddCounterByKey(string exchangeName, string key, DateTime timestamp, int interval);
    }
}
