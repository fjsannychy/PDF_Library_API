using PDFLibrary.Models.Entities;

namespace PDFLibrary.Models.ViewModels
{
    public class PublisherViewModel
    {
        // This holds the 5 Authors for the current page
        public IEnumerable<Publisher> Publishers { get; set; } = new List<Publisher>();

        // This holds the total number of Authors matching the search (e.g., 50)
        public int TotalCount { get; set; }
    }
}
