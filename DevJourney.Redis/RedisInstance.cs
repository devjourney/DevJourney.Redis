using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisInstance
    {
        ConfigurationOptions _configurationOptions;

        RedisFeatures _features;
        public async Task<RedisFeatures> GetFeaturesAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _features;
        }

        string _version;
        public async Task<string> GetVersionAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _version;
        }

        sbyte _maxDb;
        public async Task<sbyte> GetMaxDbAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _maxDb;
        }

        public async Task<IDatabase> GetDatabaseAsync(int dbNumber = 0,
                                                     bool allowAdmin = false)
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }

            if (dbNumber < 0 || dbNumber > _maxDb)
                throw new IndexOutOfRangeException(
                    $"DB number must be 0 to {_maxDb}.");

            RedisConnector rc = new RedisConnector(_configurationOptions,
                                                   allowAdmin);
            return rc.Connection.GetDatabase(dbNumber);
        }

        public RedisInstance(string configuration)
        {
            _configurationOptions = ConfigurationOptions.Parse(configuration);
            if (_configurationOptions == null
                || _configurationOptions.EndPoints == null
                || _configurationOptions.EndPoints.Count == 0)
            {
                throw new RedisHelperException(
                    "No endpoints were supplied in the configuration string.");
            }
        }

        bool _probeComplete;
        IServer _server;
        async Task<bool> Probe()
        {
            ConfigurationOptions configDB0 = _configurationOptions.Clone();
            configDB0.DefaultDatabase = 0;
            RedisConnector connectorDB0 = null;
            try
            {
                connectorDB0 = new RedisConnector(configDB0, true);
            }
            catch (Exception ex)
            {
                throw new RedisHelperException(
                    "Cannot create DbConnection.", ex);
            }

            IDatabase db0 = null;
            try
            {
                db0 = connectorDB0.Connection.GetDatabase(0);
            }
            catch (Exception ex)
            {
                throw new RedisHelperException("Cannot GetDatabase(0).", ex);
            }

            TimeSpan pingDuration = default(TimeSpan);
            try
            {
                pingDuration = await db0.PingAsync();
            }
            catch (Exception ex)
            {
                throw new RedisHelperException("Cannot ping db0.", ex);
            }

            try
            {
                _server = connectorDB0.Connection.GetServer(
                    configDB0.EndPoints[0]);
                _features = _server.Features;
            }
            catch (Exception ex)
            {
                throw new RedisHelperException(
                    "Cannot get server features.", ex);
            }

            try
            {
                IGrouping<string, KeyValuePair<string, string>>[] info =
                    await _server.InfoAsync("Server");
                if (info[0].Key.Equals("Server",
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    foreach (KeyValuePair<string, string> kvp in info[0])
                    {
                        if (kvp.Key.Equals("redis_version",
                            StringComparison.CurrentCultureIgnoreCase))
                        {
                            _version = kvp.Value;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new RedisHelperException(
                    "Cannot get redis_version.", ex);
            }

            sbyte maxDb = 0;
            do
            {
                try
                {
                    IDatabase nxtDb = connectorDB0
                        .Connection.GetDatabase(maxDb);
                    RedisResult rr = await nxtDb.ExecuteAsync("PING");
                    if (rr == null || rr.IsNull
                        || !rr.ToString().Equals(
                            "PONG", StringComparison.CurrentCultureIgnoreCase))
                    {
                        break;
                    }
                    ++maxDb;
                }
                catch
                {
                    // expected... swallow
                    break;
                }
            } while (maxDb < sbyte.MaxValue);
            _maxDb = maxDb;

            return true;
        }

        public async Task<SortedDictionary<string, RedisKeyInfo>>
            ScanDatabaseAsync(int dbNumber, string pattern = "*",
                              int maxCount = Int32.MaxValue,
                              bool includeLastAccessed = false,
                              bool includeExpiry = false)
        {
            SortedDictionary<string, RedisKeyInfo> result =
                new SortedDictionary<string, RedisKeyInfo>();
            if (maxCount < 1)
                return result;

            IDatabase dbx = await GetDatabaseAsync(dbNumber);

            int ndx = 0;
            int exceptionCount = 0;
            foreach (RedisKey key in _server.Keys(dbNumber, pattern, 10000))
            {
                try
                {
                    RedisType type = await dbx.KeyTypeAsync(key);
                    DateTime? expiry = null;
                    if (includeExpiry)
                    {
                        TimeSpan? ttl = await dbx.KeyTimeToLiveAsync(key);
                        if (ttl.HasValue)
                            expiry = DateTime.UtcNow.AddSeconds(
                                ttl.Value.TotalSeconds);
                    }
                    DateTime? lastAccessed = null;
                    if (includeLastAccessed)
                    {
                        RedisResult idleString = await dbx.ExecuteAsync(
                            "object",
                            new[] { "idletime", key.ToString() });
                        int idleSeconds = (int)idleString;
                        if (idleSeconds > 0)
                            lastAccessed = DateTime.UtcNow.AddSeconds(
                                -idleSeconds);
                    }
                    result.Add(key.ToString(), new RedisKeyInfo(key, type,
                        expiry, lastAccessed));
                    if (++ndx == maxCount)
                        break;
                }
                catch (Exception ex)
                {
                    if (++exceptionCount > 100)
                        throw ex;
                    Console.WriteLine($"{ex.GetType().Name} while " +
                        $"loading details for {key}: '{ex.Message}'.");
                }
            }

            return result;
        }
    }
}
