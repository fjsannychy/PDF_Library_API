namespace PDFLibrary.Models.DTOs
{
    public class AuthorFilter
    {
        public string Search { get; set; }
        public int PageNumber { get; set; } = 1; 
        public int PageSize { get; set; } = 100;   
    }
}
