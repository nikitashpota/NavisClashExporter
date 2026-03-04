using System;

namespace NavisClashExporter.Models
{
    public class DirectoryModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public DateTime CreatedAt { get; set; }
        public override string ToString() => Code;
    }
}