namespace ApiLetraViva.Models
{
    public class Customer
    {
        public Guid Id { get; set; }

        public string Phone { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? PreferredPlatform { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
