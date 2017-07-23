﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisInstance
    {
        private string _serverName = "localhost";
        public string ServerName
        {
            get { return _serverName; }
            set
            {
                if (value == null || value.Trim().Length == 0)
                    throw new ArgumentException("The ServerName may not be null or empty.");
                _probeComplete = false;
                _serverName = value.Trim();
            }
        }

        private int _port = 6379;
        public int Port
        {
            get { return _port; }
            set
            {
                if (value < 0 || value > 65535)
                    throw new ArgumentException("The Port must be from 0 to 65535.");
                _probeComplete = false;
                _port = value;
            }
        }

        private RedisFeatures _features;
        public async Task<RedisFeatures> GetFeaturesAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _features;
        }

        private string _version;
        public async Task<string> GetVersionAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _version;
        }

        private sbyte _maxDb = 0;
        public async Task<sbyte> GetMaxDbAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _maxDb;
        }

        private RedisConnector _connector = null;
        public async Task<RedisConnector> GetConnectorAsync()
        {
            if (!_probeComplete)
            {
                _probeComplete = await Probe();
            }
            return _connector;
        }

        public async Task<IDatabase> GetDatabaseAsync(int dbNumber = 0)
        {
			if (!_probeComplete)
			{
				_probeComplete = await Probe();
			}

            if (dbNumber < 0 || dbNumber > _maxDb)
				throw new IndexOutOfRangeException(
                    $"Index must be 0 to {_maxDb} for {_serverName}:{_port}. The value {dbNumber} is out of range.");

			RedisConnector rc = await GetConnectorAsync();
            return rc.Connection.GetDatabase(dbNumber);
        }

        public RedisInstance(string serverName = "localhost", int port = 6379)
        {
            ServerName = serverName;
            Port = port;
        }

        private bool _probeComplete = false;
        private IServer _server = null;
        private async Task<bool> Probe()
        {
            try
            {
                _connector = new RedisConnector(_serverName, _port, defaultDatabase: 0, allowAdmin: true);
            }
            catch (Exception ex)
            {
                throw new RedisHelperException("Cannot create DbConnection.", ex);
            }

            IDatabase db0 = null;
            try
            {
                db0 = _connector.Connection.GetDatabase(0);
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
                _server = _connector.Connection.GetServer(_serverName, _port);
                _features = _server.Features;
            }
            catch (Exception ex)
            {
                throw new RedisHelperException("Cannot get server features.", ex);
            }

            try
            {
                IGrouping<string, KeyValuePair<string, string>>[] info =
                    await _server.InfoAsync("Server");
                if (info[0].Key.Equals("Server", StringComparison.CurrentCultureIgnoreCase))
                {
                    foreach (KeyValuePair<string, string> kvp in info[0])
                    {
                        if (kvp.Key.Equals("redis_version", StringComparison.CurrentCultureIgnoreCase))
                        {
                            _version = kvp.Value;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new RedisHelperException("Cannot get redis_version.", ex);
            }

            sbyte maxDb = 0;
            do
            {
                try
                {
                    IDatabase nxtDb = _connector.Connection.GetDatabase(maxDb + 1);
                    RedisResult rr = await nxtDb.ExecuteAsync("PING");
                    if (rr == null || rr.IsNull || !rr.ToString().Equals("PONG", StringComparison.CurrentCultureIgnoreCase))
                        break;
                    ++maxDb;
                }
                catch //(Exception ex)
                {
                    //Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
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
            if (maxCount < 1) maxCount = 1;
            //if (maxCount > 250000) maxCount = 250000;

            IDatabase dbx = await GetDatabaseAsync(dbNumber);
			SortedDictionary<string, RedisKeyInfo> result =
                new SortedDictionary<string, RedisKeyInfo>();

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
