using PDFLibrary.Models.Entities;

namespace PDFLibrary.Models.ViewModels
{
    public class AuthorViewModel
    {
        // This holds the 5 Authors for the current page
        public IEnumerable<Author> Authors { get; set; } = new List<Author>();

        // This holds the total number of Authors matching the search (e.g., 50)
        public int TotalCount { get; set; }
    }
}
