using System;

namespace NavisClashExporter.Models
{
    public class ClashResultModel
    {
        public int Id { get; set; }
        public int ClashTestId { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public double? Distance { get; set; }
        public string Description { get; set; }
        public string GridLocation { get; set; }
        public double? PointX { get; set; }
        public double? PointY { get; set; }
        public double? PointZ { get; set; }
        public DateTime? CreatedDate { get; set; }
        public byte[] Image { get; set; }
        public string Item1Id { get; set; }
        public string Item1Name { get; set; }
        public string Item1Type { get; set; }
        public string Item1Layer { get; set; }
        public string Item1SourceFile { get; set; }
        public string Item2Id { get; set; }
        public string Item2Name { get; set; }
        public string Item2Type { get; set; }
        public string Item2Layer { get; set; }
        public string Item2SourceFile { get; set; }
    }
}