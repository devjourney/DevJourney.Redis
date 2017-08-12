using System;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisConnector
    {
        readonly Lazy<ConnectionMultiplexer> _lazyPlex;

        public RedisConnector(ConfigurationOptions options,
                             bool allowAdmin = false)
        {
			if (options == null
				|| options.EndPoints == null
				|| options.EndPoints.Count == 0)
			{
				throw new RedisHelperException(
					"No endpoints were supplied in the configuration.");
			}
            options.AllowAdmin = allowAdmin;

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
