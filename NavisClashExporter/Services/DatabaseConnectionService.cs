using System;
using System.IO;
using NavisClashExporter.Models;

namespace NavisClashExporter.Services
{
    public class DatabaseConnectionService
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NavisClashExporter", "db_connection.json");

        private DbConnectionConfig _config;
        public DbConnectionConfig Config => _config;

        public bool Load()
        {
            if (!File.Exists(FilePath)) return false;
            try
            {
                _config = DbConnectionConfig.Deserialize(File.ReadAllText(FilePath));
                return _config != null;
            }
            catch { return false; }
        }

        public void Save(DbConnectionConfig config)
        {
            _config = config;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, config.Serialize());
        }
    }
}