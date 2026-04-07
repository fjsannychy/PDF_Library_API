namespace PDFLibrary.Models.DTOs
{
    public class RegisterUserRequest
    {
        public int Id { get; set; } 
        public string Username { get; set; } = string.Empty;
        public string Fullname { get; set; } = string.Empty; 
        public string Password { get; set; } = string.Empty;
    }
}
