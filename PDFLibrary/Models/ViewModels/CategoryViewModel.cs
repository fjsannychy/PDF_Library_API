using PDFLibrary.Models.Entities;

namespace PDFLibrary.Models.ViewModels
{
    public class CategoryViewModel
    {
        // This holds the 5 Authors for the current page
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();

        // This holds the total number of Authors matching the search (e.g., 50)
        public int TotalCount { get; set; }
    }
}
