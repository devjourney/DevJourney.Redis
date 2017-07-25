using System;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisConnector
    {
        private Lazy<ConnectionMultiplexer> _lazyPlex = null;
        public string ServerName { get; private set; }
        public int Port { get; private set; }
        public int DefaultDatabase { get; private set; }

        public RedisConnector(string serverName = "localhost",
            int port = 6379, string password = null, int defaultDatabase = 0, 
            bool allowAdmin = true)
        {
			ServerName = serverName;
			Port = port;
            DefaultDatabase = defaultDatabase;

			ConfigurationOptions options = ConfigurationOptions.Parse(
                $"{serverName}:{port}");
            options.AllowAdmin = allowAdmin;
            options.DefaultDatabase = defaultDatabase;
            options.Password = password;
            if (password != null)
            {
                options.Ssl = true;
            }

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
