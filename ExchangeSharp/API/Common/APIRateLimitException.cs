using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.API.Common
{
    public class APIRateLimitException : APIException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public APIRateLimitException(string message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException">Inner exception</param>
        public APIRateLimitException(string message, Exception innerException) : base(message, innerException) { }
    }
}
