using System;
using NavisClashExporter.Models;

namespace NavisClashExporter.Models
{
    public class NavisworksProjectModel
    {
        public int Id { get; set; }
        /// <summary>Название = project_name из RevitSync (ключ связи)</summary>
        public string Name { get; set; }
        /// <summary>Полный путь к NWF файлу</summary>
        public string NwfPath { get; set; }
        public int? DirectoryId { get; set; }
        public DirectoryModel Directory { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public override string ToString() => Name;
    }
}