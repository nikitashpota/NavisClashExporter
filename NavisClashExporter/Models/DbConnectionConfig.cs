using Newtonsoft.Json;

namespace NavisClashExporter.Models
{
    public class DbConnectionConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5432;
        public string Database { get; set; } = "progress";
        public string Username { get; set; } = "progress";
        public string Password { get; set; } = "12345678";

        public string ToConnectionString() =>
    $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};" +
    $"Timeout=5;Command Timeout=30;" +
    $"Pooling=true;Minimum Pool Size=1;Maximum Pool Size=5;";

        public string Serialize() => JsonConvert.SerializeObject(this);

        public static DbConnectionConfig Deserialize(string json)
        {
            try { return JsonConvert.DeserializeObject<DbConnectionConfig>(json); }
            catch { return null; }
        }
    }
}