namespace ApiLetraViva.Models
{
    public class Order
    {
        public Guid Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;  // Ej: LV-20260513-0042

        public Guid CustomerId { get; set; }

        public Customer Customer { get; set; } = null!;

        public string Package { get; set; } = string.Empty;

        public string? Occasion { get; set; }

        public string? Genre { get; set; }

        public string? Details { get; set; }

        public string Status { get; set; } = "Pending";

        public string PaymentStatus { get; set; } = "Pending";

        public decimal Total { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
