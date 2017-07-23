using System;
namespace DevJourney.Redis
{
    public class RedisHelperException : Exception
    {
        public RedisHelperException(string message,
            Exception innerException = null) : base(message, innerException)
        {
        }
    }
}
