namespace ApiLetraViva.Models
{
    public class Customer
    {
        public Guid Id { get; set; }
        public long TelegramChatId { get; set; }
        public string Phone { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? PreferredPlatform { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Conversation> Conversations { get; set; }= new List<Conversation>();
    }
}
