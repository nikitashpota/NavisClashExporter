using System;

namespace NavisClashExporter.Models
{
    public class ClashTestHistoryModel
    {
        public int Id { get; set; }
        public int NavisworksProjectId { get; set; }
        public string TestName { get; set; }
        public DateTime RecordDate { get; set; }
        public int SummaryTotal { get; set; }
        public int SummaryNew { get; set; }
        public int SummaryActive { get; set; }
        public int SummaryReviewed { get; set; }
        public int SummaryApproved { get; set; }
        public int SummaryResolved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}