using PDFLibrary.Models.Enums;

namespace PDFLibrary.Models.DTOs
{
    public class BookFilter
    {
        public string Search { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public FilterType FilterType { get; set; }
    }
}
