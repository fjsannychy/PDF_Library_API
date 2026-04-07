namespace PDFLibrary.Models.Entities
{
    public class UserMark
    {
        public int Id { get; set; }

        public int BookId { get; set; }

        public int? UserId { get; set; }

        public int PageNumber { get; set; }

        public double PositionTopX { get; set; }

        public double PositionTopY { get; set; }

        public double PositionBottomX { get; set; }

        public double PositionBottomY { get; set; }

        public DateTime? MarkingTime { get; set; }
    }
}
