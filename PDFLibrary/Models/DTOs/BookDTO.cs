using PDFLibrary.Models.Entities;
using PDFLibrary.Models.Enums;

namespace PDFLibrary.Models.DTOs
{

    public class BookDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? PriceBeforeDiscount { get; set; }

        public int CategoryId { get; set; }
        public int PublisherId { get; set; }
        public int AuthorId { get; set; }

        public string? Edition { get; set; }
        public int? Volume { get; set; }

        public string? ShortDescription { get; set; }
        public string? Details { get; set; }

        public Status Status { get; set; }

        public List<string> Features { get; set; } = new();

        public IFormFile? CoverPhoto { get; set; }

        public IFormFile? PdfFile { get; set; }

        public List<BookAttachmentDTO> bookAttachments { get; set; } = new();
    }

}
