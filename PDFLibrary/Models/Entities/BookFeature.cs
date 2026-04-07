using PDFLibrary.Models.Enums;

namespace PDFLibrary.Models.Entities
{
    public class BookFeature
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int BookId { get; set; }
        public Status Status { get; set; }

    }
}
