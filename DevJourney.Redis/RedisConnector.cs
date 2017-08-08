using System;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisConnector
    {
        private Lazy<ConnectionMultiplexer> _lazyPlex;
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

		public RedisConnector(string serverName = "localhost",
            int port = 6379, int defaultDatabase = 0, bool allowAdmin = true)
		{
			ConfigurationOptions options = ConfigurationOptions.Parse(
				$"{serverName}:{port}");
			options.AllowAdmin = allowAdmin;
			options.DefaultDatabase = defaultDatabase;

			_lazyPlex =
			   new Lazy<ConnectionMultiplexer>(() =>
			   {
				   return ConnectionMultiplexer.Connect(options);
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
