using System;
using System.IO;

namespace NavisClashExporter.Services
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "navis-clash-exporter", "log.txt");

        static Logger()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        }

        public static void Log(string message)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n"); }
            catch { }
        }

        public static void LogError(Exception ex)
            => Log($"ERROR: {ex.Message}\n{ex.StackTrace}");
    }
}