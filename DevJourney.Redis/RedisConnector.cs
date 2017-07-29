using System;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisConnector
    {
        readonly Lazy<ConnectionMultiplexer> _lazyPlex;
        readonly ConfigurationOptions _configurationOptions;

        public RedisConnector(ConfigurationOptions config,
                             bool allowAdmin = false)
        {
			if (config == null
				|| config.EndPoints == null
				|| config.EndPoints.Count == 0)
			{
				throw new RedisHelperException(
					"No endpoints were supplied in the configuration.");
			}
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
