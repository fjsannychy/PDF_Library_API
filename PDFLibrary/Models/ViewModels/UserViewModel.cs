using PDFLibrary.Models.Entities;

namespace PDFLibrary.Models.ViewModels
{
    public class UserViewModel
    {
        // Users for the current page
        public IEnumerable<User> Users { get; set; } = new List<User>();

        // Total number of users matching the search
        public int TotalCount { get; set; }
    }
}