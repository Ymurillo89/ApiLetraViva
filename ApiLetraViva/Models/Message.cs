namespace ApiLetraViva.Models
{
    public class Message
    {
        public Guid Id { get; set; }

        public Guid ConversationId { get; set; }

        public Conversation Conversation { get; set; } = null!;

        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
