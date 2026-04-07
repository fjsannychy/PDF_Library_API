namespace PDFLibrary.Models.DTOs
{
    public class UserFilter
    {
        public string Search { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
}
