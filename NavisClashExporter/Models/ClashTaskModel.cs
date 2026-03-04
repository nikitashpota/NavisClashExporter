using System;

namespace NavisClashExporter.Models
{
    public enum ClashTaskStatus { Pending, Running, Done, Failed }

    public class ClashTaskModel
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string NwcFolder { get; set; }
        public ClashTaskStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public string RevitVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}