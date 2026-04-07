namespace PDFLibrary.Models.Entities
{
    public class BookAttachmentDTO
    {
        public int Id { get; set; }
        public int BookId { get; set; }

        public IFormFile? File { get; set; } = default!; 
        public string? FileUrl { get; set; }             
    }
}
