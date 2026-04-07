using PDFLibrary.Models.Enums;
using System.Net.NetworkInformation;

namespace PDFLibrary.Models.Entities
{
    public class Author
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public Status Status { get; set; }
    }
}
