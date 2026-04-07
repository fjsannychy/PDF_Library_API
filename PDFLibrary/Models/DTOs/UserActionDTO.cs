using PDFLibrary.Models.Enums;

namespace PDFLibrary.Models.DTOs
{
    public class UserActionDTO
    {
        public int BookId { get; set; }
        public ActionType ActionType { get; set; }
    }
}
