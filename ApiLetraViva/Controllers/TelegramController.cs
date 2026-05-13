using ApiLetraViva.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace ApiLetraViva.Controllers
{
    [ApiController]
    [Route("webhook/telegram")]
    public class TelegramController : ControllerBase
    {
        private readonly ConversationManager _conversationManager;
        private readonly EmailService _emailService;
        private readonly ILogger<TelegramController> _logger;

        public TelegramController(
            ConversationManager conversationManager,
            EmailService emailService,
            ILogger<TelegramController> logger)
        {
            _conversationManager = conversationManager;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] Update update)
        {
            long chatId = 0;
            try
            {
                if (update.Message is null)
                {
                    _logger.LogWarning("Update sin mensaje, ignorando.");
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(update.Message.Text))
                {
                    _logger.LogWarning("Mensaje sin texto, ignorando.");
                    return Ok();
                }

                chatId = update.Message.Chat.Id;

                await _conversationManager.HandleMessageAsync(update);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error procesando webhook | ChatId: {ChatId}",
                    chatId);
                return Ok();
            }
        }

        [HttpPost("test-email")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
        {
            try
            {
                _logger.LogInformation("Endpoint de prueba de email llamado | Email: {Email}", request.Email);

                // Crear objetos de prueba
                var testCustomer = new ApiLetraViva.Models.Customer
                {
                    Id = Guid.NewGuid(),
                    Name = request.CustomerName ?? "Cliente de Prueba",
                    Email = request.Email,
                    TelegramChatId = 123456789
                };

                var testOrder = new ApiLetraViva.Models.Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = $"LV-TEST-{DateTime.UtcNow:HHmmss}",
                    CustomerId = testCustomer.Id,
                    Package = request.Package ?? "Estándar",
                    Occasion = request.Occasion ?? "Cumpleaños",
                    Genre = request.Genre ?? "Pop",
                    Details = request.Details ?? "Canción de prueba para verificar el sistema de correos",
                    Total = 109900,
                    Status = "Pending",
                    PaymentStatus = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                // Enviar correos
                await _emailService.SendOrderConfirmationToCustomerAsync(testOrder, testCustomer);
                await _emailService.SendOrderNotificationToProviderAsync(testOrder, testCustomer);

                return Ok(new
                {
                    success = true,
                    message = "Correos de prueba enviados. Revisa los logs para ver el resultado.",
                    orderNumber = testOrder.OrderNumber,
                    customerEmail = testCustomer.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en endpoint de prueba de email");
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }

    public class TestEmailRequest
    {
        public string Email { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? Package { get; set; }
        public string? Occasion { get; set; }
        public string? Genre { get; set; }
        public string? Details { get; set; }
    }
}
