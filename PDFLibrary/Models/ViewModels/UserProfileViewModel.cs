namespace PDFLibrary.Models.ViewModels
{
    public class UserProfileViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Fullname { get; set; }
        public string Role { get; set; }
        public List<UserActivityViewModel> Favorites { get; set; }
        public List<UserActivityViewModel> RecentActivity { get; set; }
        public List<BookListItemViewModel> PurchasedBooks { get; set; } = new();
    }
}
