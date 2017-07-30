using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace DevJourney.Redis
{
    public class RedisKeyInfo
    {
        public string Name { get; private set; }
        public RedisType Type { get; private set; }
        public DateTime? Expiry { get; private set; }
        public DateTime? LastAccessed { get; private set; }

        public RedisKeyInfo(RedisKey key, RedisType type,
            DateTime? expiry = null, DateTime? lastAccessed = null)
        {
            if ((object)key == null)
                throw new ArgumentNullException(nameof(key));
            Name = key.ToString();
            Type = type;
            Expiry = expiry;
            LastAccessed = lastAccessed;
        }

        public static implicit operator string(RedisKeyInfo keyInfo)
        {
            return keyInfo.Name;
        }

        public static implicit operator RedisKey(RedisKeyInfo keyInfo)
        {
            return keyInfo.Name;
        }

        public override string ToString()
        {
            string result = $"{Name} ({Type})";
            if (LastAccessed.HasValue)
                result += $" accessed '{LastAccessed.Value}'";
            if (Expiry.HasValue)
                result += $" expiring '{Expiry.Value}'";
            return result;
        }

        public SortedDictionary<double, string> ScanSortedSet(
            IDatabase dbx, string pattern = "*",
            int maxCount = Int32.MaxValue)
        {
            if (dbx == null)
                throw new ArgumentNullException(nameof(dbx));
            if (Type != RedisType.SortedSet)
                throw new InvalidCastException(
                    $"{nameof(ScanSortedSet)} invalid on {Type} type keys.");

            var output = new SortedDictionary<double, string>();
            if (maxCount < 1)
                return output;
            foreach (SortedSetEntry entry in dbx.SortedSetScan(this,
                pattern, 10000))
            {
                if (--maxCount < 0)
                    break;
                if (!entry.Element.HasValue)
                    continue;
                output.Add(entry.Score, entry.Element);
            }
            return output;
        }

        public SortedDictionary<string, string> ScanHash(
            IDatabase dbx, string pattern = "*",
            int maxCount = Int32.MaxValue)
        {
            if (dbx == null)
                throw new ArgumentNullException(nameof(dbx));
            if (Type != RedisType.Hash)
                throw new InvalidCastException(
                    $"{nameof(ScanHash)} invalid on {Type} type keys.");

            var output = new SortedDictionary<string, string>();
            if (maxCount < 1)
                return output;
            foreach (HashEntry entry in dbx.HashScan(this, pattern, 10000))
            {
                if (--maxCount < 0)
                    break;
                output.Add(entry.Name, entry.Value);
            }
            return output;
        }

        public async Task<List<string>> RangeListAsync(
            IDatabase dbx, long start = 0, long stop = -1)
        {
            if (dbx == null)
                throw new ArgumentNullException(nameof(dbx));
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start),
                    start, "start must be a non-negative number.");
            if (Type != RedisType.List)
                throw new InvalidCastException(
                    $"{nameof(RangeListAsync)} invalid on {Type} type keys.");
            if (stop < 0)
            {
                stop = -1;
            }
            else
            {
                if (start > stop)
                    throw new ArgumentException(
                        $"{nameof(start)} '{start}' must be less " +
                        $"than {nameof(stop)} '{stop}'.",
                        nameof(start));
            }

            var output = new List<string>();
            foreach (RedisValue rv in
               await dbx.ListRangeAsync(this, start, stop))
            {
                output.Add(rv);
            }
            return output;
        }

        public List<string> ScanSet(
            IDatabase dbx, string pattern = "*",
            int maxCount = Int32.MaxValue)
        {
            if (dbx == null)
                throw new ArgumentNullException(nameof(dbx));
            if (Type != RedisType.Set)
                throw new InvalidCastException(
                    $"Key {Name} is a {Type}. ScanSet invalid.");

            var output = new List<string>();
            if (maxCount < 1)
                return output;
			foreach (RedisValue entry in dbx.SetScan(this, pattern, 10000))
            {
                if (--maxCount < 0)
                    break;
                output.Add(entry);
            }
            return output;
        }

        public async Task<object> ScanAsync(IDatabase dbx)
        {
            switch (this.Type)
            {
                default:
                    throw new RedisHelperException(
                        $"Unsupported type '{this.Type}'.");
                case RedisType.None:
                case RedisType.Unknown:
                    break;
                case RedisType.SortedSet:
                    return ScanSortedSet(dbx);
                case RedisType.Hash:
                    return ScanHash(dbx);
                case RedisType.List:
                    return await RangeListAsync(dbx);
                case RedisType.String:
                    return dbx.StringGetAsync(this);
                case RedisType.Set:
                    return ScanSet(dbx);
            }
            return null;
        }
    }
}