using SysLogAlerter.Objects.JSON;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SysLogAlerter
{
    internal class Program
    {
        private const string PrivalPattern = @"\<(?<PRIVAL>\d{1,3})\>";
        private const string TimestampPattern = @"(?<TIMESTAMP>(?:(?:\w)+ (?:(?:0?[1-9]|[12][0-9]|3[01)])) (?:0?[0-9]|1[0-9]|2[0-3]):(?:0?[0-9]|[1-5][0-9]):(?:0?[0-9]|[1-5][0-9])))";
        private const string HostnamePattern = @"(?<HOSTNAME>(?:(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))|(?:\.|\S|\w)+)";
        private const string MessagePattern = @"(?<MESSAGE>.+)?";

        private const string DEFAULT_FILENAME_CONFIG = "slaconfig.json";

        private static Config? ParseConfig(string fileName = DEFAULT_FILENAME_CONFIG)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"Failed to find file ({fileName}) - creating default ({DEFAULT_FILENAME_CONFIG})");

                var config = new Config();

                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, fileName), JsonSerializer.Serialize(config));

                return config;
            }

            return JsonSerializer.Deserialize<Config>(File.ReadAllText(fileName));
        }

        private static string ParseLine(string line, Dictionary<string, string> mapping)
        {
            var regex = new Regex($@"^{PrivalPattern}{TimestampPattern} {HostnamePattern} {MessagePattern}", RegexOptions.None, new TimeSpan(0, 0, 5));

            var matches = regex.Matches(line);

            if (matches.Count == 0)
            {
                return String.Empty;
            }

            var jsonString = "{";

            foreach (Match match in matches)
            {
                Console.WriteLine($"{match.Name}:{match.Value}");
            
                if (!mapping.ContainsKey(match.Name.ToUpper()))
                {
                    continue;
                }

                Console.WriteLine($"Match on {match.Name} mapping to ({match.Value})");

                jsonString += $"\"{mapping[match.Name]}\": \"{match.Value}\",";
            }

            jsonString += "}";

            return jsonString;
        }

        private static async void RunWatcher(string logFilePath, Dictionary<string, string> mapping, string urlToPost)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                Console.WriteLine("LogPath property is null or empty - cannot create monitor");

                return;
            }

            if (!File.Exists(logFilePath))
            {
                Console.WriteLine($"Path ({logFilePath}) does not exist - cannot create monitor");

                return;
            }

            using var reader = new StreamReader(new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

            long lastMaxOffset = reader.BaseStream.Length;

            while (true)
            {
                System.Threading.Thread.Sleep(100);

                if (reader.BaseStream.Length == lastMaxOffset)
                {
                    continue;
                }

                reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var jsonObject = ParseLine(line, mapping);

                    if (string.IsNullOrEmpty(jsonObject))
                    {
                        continue;
                    }

                    Console.WriteLine($"JSON Object to POST: {jsonObject}");

                    var httpClient = new HttpClient();

                    var content = new StringContent(jsonObject, Encoding.UTF8, "application/json");

                    var result = await httpClient.PostAsync(urlToPost, content);

                    Console.WriteLine($"POST to {urlToPost}: {result.StatusCode}");
                }

                lastMaxOffset = reader.BaseStream.Position;
            }
        }

        static void Main(string[] args)
        {
            var configFileName = DEFAULT_FILENAME_CONFIG;

            if (args.Length > 0)
            {
                configFileName = args[0];   
            }

            var config = ParseConfig(configFileName);

            if (config == null)
            {
                Console.WriteLine("Failing to load config - exiting");

                return;
            }

            RunWatcher(config.LogFilePath, config.Mapping, config.URL);

            return;
        }
    }
}