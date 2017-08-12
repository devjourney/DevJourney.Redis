//#define GENDATA

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace DevJourney.Redis.TestHarness
{
    class MainClass
	{
		public static void Main(string[] args)
		{
			int port = 6379;
            string server = "localhost";
            string password = null;
            if (args != null)
            {
                if (args.Length > 0)
                {
                    server = args[0];
                    if (args.Length > 1)
                    {
                        Int32.TryParse(args[1], out port);
                        if (args.Length > 2)
                        {
                            password = args[2];
                        }
                    }
                }
            }

            string configString = $"{server}:{port}";

			RedisInstance dbi = new RedisInstance(configString);
			Task.Run(async () =>
			{
				IDatabase db = await dbi.GetDatabaseAsync(0);

#if GENDATA
                for (int ndx = 0; ndx < 100000; ndx++)
                    db.StringSet($"user:{ndx}", $"{ndx}");
#endif // GENDATA

				SortedDictionary<string, RedisKeyInfo> items =
					await dbi.ScanDatabaseAsync(0, "*",
						maxCount: 200000,
						includeLastAccessed: true,
						includeExpiry: true);
				foreach (string key in items.Keys)
				{
					Console.WriteLine(items[key].ToString());
					switch (items[key].Type)
					{
						default:
							Console.WriteLine(
								$"Unsupported type '{items[key].Type}'.");
							break;
						case RedisType.SortedSet:
							foreach (SortedSetEntry entry
									 in db.SortedSetScan(items[key]))
							{
								Console.WriteLine($"{entry.Score} : {entry.Element}");
							}
							break;
						case RedisType.Hash:
							foreach (HashEntry entry
									 in db.HashScan(items[key]))
							{
								Console.WriteLine($"{entry.Name} : {entry.Value}");
							}
							break;
						case RedisType.List:
							Console.WriteLine(String.Join(", ",
								await db.ListRangeAsync(items[key])));
							break;
						case RedisType.String:
							Console.WriteLine(
								await db.StringGetAsync(items[key]));
							break;
						case RedisType.Set:
							Console.WriteLine(String.Join(", ",
							   db.SetScan(items[key])));
							break;
					}
				}
				Console.WriteLine($"Item count = {items.Count}");
			}).Wait();

			Console.ReadLine();
		}
	}
}