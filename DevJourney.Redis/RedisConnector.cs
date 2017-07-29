using System;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisConnector
    {
        private Lazy<ConnectionMultiplexer> _lazyPlex = null;
        private ConfigurationOptions _configurationOptions;

        public RedisConnector(ConfigurationOptions config,
                             bool allowAdmin = false)
        {
            _configurationOptions = config;
            _configurationOptions.AllowAdmin = allowAdmin;

            _lazyPlex =
               new Lazy<ConnectionMultiplexer>(() =>
               {
                  return ConnectionMultiplexer.Connect(_configurationOptions);
               });
        }

        public ConnectionMultiplexer Connection
        {
            get
            {
                return _lazyPlex.Value;
            }
        }
    }
}
