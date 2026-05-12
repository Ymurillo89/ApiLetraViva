using ApiLetraViva.Enums;

namespace ApiLetraViva.Models
{
    public class Conversation
    {
        public Guid Id { get; set; }

        public Guid CustomerId { get; set; }

        public Customer Customer { get; set; } = null!;

        public ConversationState State { get; set; } = ConversationState.Idle;

        public string? Context { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
