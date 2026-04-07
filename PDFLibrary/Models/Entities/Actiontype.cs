using PDFLibrary.Models.Enums;

namespace PDFLibrary.Models.Entities
{

    public class UserAction
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int UserId { get; set; }
        public ActionType ActionType { get; set; }
        public DateTime ActionTime { get; set; }

    }
}

