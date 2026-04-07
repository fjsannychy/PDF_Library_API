namespace PDFLibrary.Models.Entities
{
    public class BookAttachment
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string FileUrl { get; set; } = string.Empty;


    }
}
