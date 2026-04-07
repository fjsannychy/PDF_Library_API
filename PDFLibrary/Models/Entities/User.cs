using PDFLibrary.Models.Enums;

namespace PDFLibrary.Models.Entities
{
    public class User
    {
        public int Id { get; set; } 
        public string Username { get; set; } = string.Empty;
        public string Fullname { get; set; } = string.Empty; 
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public UserStatus Status { get; set; } 
    }
}
