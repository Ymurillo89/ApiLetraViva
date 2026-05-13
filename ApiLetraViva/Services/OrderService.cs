using ApiLetraViva.Context;
using ApiLetraViva.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiLetraViva.Services
{
    public class OrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;

        private static readonly Dictionary<string, decimal> PackagePrices = new()
        {
            { "Mini",     59900m },
            { "Estándar", 109900m },
            { "Premium",  199900m }
        };

        public OrderService(AppDbContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(
            Guid customerId,
            string package,
            string? occasion,
            string? genre,
            string? details)
        {
            // Normalizar nombre del paquete
            var normalizedPackage = PackagePrices.Keys
                .FirstOrDefault(k => k.Equals(package, StringComparison.OrdinalIgnoreCase));

            if (normalizedPackage is null)
                throw new ArgumentException(
                    $"Paquete '{package}' no válido. Opciones: {string.Join(", ", PackagePrices.Keys)}");

            // Generar número de orden legible: LV-YYYYMMDD-XXXX
            var today = DateTime.UtcNow;
            var countToday = await _context.Orders
                .CountAsync(o => o.CreatedAt.Date == today.Date);
            var orderNumber = $"LV-{today:yyyyMMdd}-{(countToday + 1):D4}";

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = orderNumber,
                CustomerId = customerId,
                Package = normalizedPackage,
                Occasion = occasion,
                Genre = genre,
                Details = details,
                Total = PackagePrices[normalizedPackage],
                Status = "Pending",
                PaymentStatus = "Pending",
                CreatedAt = today
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Pedido creado | OrderNumber: {OrderNumber} | CustomerId: {CustomerId} | Package: {Package} | Total: {Total}",
                orderNumber, customerId, normalizedPackage, order.Total);

            return order;
        }

        public async Task<Order?> GetOrderAsync(Guid orderId)
        {
            return await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task UpdateOrderStatusAsync(Guid orderId, string status, string paymentStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order is null)
            {
                _logger.LogWarning("UpdateOrderStatus: Order {OrderId} no encontrado", orderId);
                return;
            }

            order.Status = status;
            order.PaymentStatus = paymentStatus;
            await _context.SaveChangesAsync();
        }
    }
}
