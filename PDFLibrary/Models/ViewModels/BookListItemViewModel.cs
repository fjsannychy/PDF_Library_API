namespace PDFLibrary.Models.ViewModels
{
    public class BookListItemViewModel
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

        public string? CoverPhotoUrl { get; set; }

        public DateTime? PublishDate { get; set; }
        public DateTime RegisterDate { get; set; } = DateTime.Now;

        public int RegisteredBy { get; set; }
        public int Status { get; set; }
        public string Category { get; set; }
        public string Publisher { get; set; }
        public string Author { get; set; }
        public string RegisteredUser { get; set; }
        public string Features { get; set; }
        

    }
}
