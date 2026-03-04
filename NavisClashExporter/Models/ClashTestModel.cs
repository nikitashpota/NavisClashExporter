using System;

namespace NavisClashExporter.Models
{
    public class ClashTestModel
    {
        public int Id { get; set; }
        public int NavisworksProjectId { get; set; }
        public string Name { get; set; }
        public string TestType { get; set; }
        public string Status { get; set; }
        public double? Tolerance { get; set; }
        public string LeftLocator { get; set; }
        public string RightLocator { get; set; }
        public int SummaryTotal { get; set; }
        public int SummaryNew { get; set; }
        public int SummaryActive { get; set; }
        public int SummaryReviewed { get; set; }
        public int SummaryApproved { get; set; }
        public int SummaryResolved { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}