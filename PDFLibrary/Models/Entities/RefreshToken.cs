namespace PDFLibrary.Models.Entities
{
    public class RefreshToken
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
