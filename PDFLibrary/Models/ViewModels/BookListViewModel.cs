namespace PDFLibrary.Models.ViewModels
{
    public class BookListViewModel
    {
        public int TotalCount { get; set; }
        public List<BookListItemViewModel> Items { get; set; } = new List<BookListItemViewModel>();
    }
}
