using ApiLetraViva.Models;
using Resend;

namespace ApiLetraViva.Services
{
    public class EmailService
    {
        private readonly IResend _resend;
        private readonly string _providerEmail;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            var apiKey = configuration["RESEND_API_KEY"] 
                ?? throw new InvalidOperationException("RESEND_API_KEY no configurada");
            
            _providerEmail = configuration["PROVIDER_EMAIL"] 
                ?? throw new InvalidOperationException("PROVIDER_EMAIL no configurado");

            _resend = ResendClient.Create(apiKey);
            _logger = logger;
        }

        public async Task SendOrderConfirmationToCustomerAsync(Order order, Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.Email))
            {
                _logger.LogWarning(
                    "No se puede enviar correo al cliente | CustomerId: {CustomerId} | Email no registrado",
                    customer.Id);
                return;
            }

            try
            {
                var total = order.Total.ToString("N0");
                var htmlBody = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                        <h2 style="color: #FFD700;">✅ ¡Pedido registrado con éxito!</h2>
                        
                        <h3>📋 Resumen de tu pedido:</h3>
                        <ul style="line-height: 1.8;">
                            <li>🔢 <strong>N° de orden:</strong> {order.OrderNumber}</li>
                            <li>🎵 <strong>Paquete:</strong> {order.Package}</li>
                            <li>💰 <strong>Total:</strong> ${total} COP</li>
                            <li>🎉 <strong>Ocasión:</strong> {order.Occasion ?? "—"}</li>
                            <li>🎸 <strong>Género:</strong> {order.Genre ?? "—"}</li>
                            <li>💌 <strong>Mensaje:</strong> {order.Details ?? "—"}</li>
                        </ul>

                        <p style="background-color: #FFF9E6; padding: 15px; border-left: 4px solid #FFD700;">
                            <strong>Guarda tu número de orden <span style="color: #FF6B6B;">{order.OrderNumber}</span> 
                            para consultar el estado de tu canción 🎶</strong>
                        </p>

                        <p>En breve recibirás un fragmento de 40-50 seg para escuchar antes de pagar.</p>
                        
                        <p style="color: #666; margin-top: 30px;">
                            ¡Gracias por confiar en <strong>Letra Viva</strong>! 💛
                        </p>
                    </div>
                    """;

                var message = new EmailMessage
                {
                    From = "onboarding@resend.dev",
                    To = customer.Email,
                    Subject = $"Confirmación de pedido {order.OrderNumber} - Letra Viva",
                    HtmlBody = htmlBody
                };

                var response = await _resend.EmailSendAsync(message);

                _logger.LogInformation(
                    "Correo enviado al cliente | OrderNumber: {OrderNumber} | Email: {Email}",
                    order.OrderNumber, customer.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error enviando correo al cliente | OrderNumber: {OrderNumber} | Email: {Email}",
                    order.OrderNumber, customer.Email);
            }
        }

        public async Task SendOrderNotificationToProviderAsync(Order order, Customer customer)
        {
            try
            {
                var total = order.Total.ToString("N0");
                var htmlBody = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                        <h2 style="color: #4CAF50;">🔔 Nueva orden recibida</h2>
                        
                        <h3>📋 Detalles del pedido:</h3>
                        <ul style="line-height: 1.8;">
                            <li>🔢 <strong>N° de orden:</strong> {order.OrderNumber}</li>
                            <li>👤 <strong>Cliente:</strong> {customer.Name ?? "Sin nombre"}</li>
                            <li>📧 <strong>Email:</strong> {customer.Email ?? "No registrado"}</li>
                            <li>📱 <strong>Telegram Chat ID:</strong> {customer.TelegramChatId}</li>
                            <li>🎵 <strong>Paquete:</strong> {order.Package}</li>
                            <li>💰 <strong>Total:</strong> ${total} COP</li>
                            <li>🎉 <strong>Ocasión:</strong> {order.Occasion ?? "—"}</li>
                            <li>🎸 <strong>Género:</strong> {order.Genre ?? "—"}</li>
                            <li>💌 <strong>Detalles:</strong> {order.Details ?? "—"}</li>
                            <li>📅 <strong>Fecha:</strong> {order.CreatedAt:dd/MM/yyyy HH:mm} UTC</li>
                        </ul>

                        <p style="background-color: #E8F5E9; padding: 15px; border-left: 4px solid #4CAF50;">
                            <strong>Estado:</strong> {order.Status} | <strong>Pago:</strong> {order.PaymentStatus}
                        </p>
                    </div>
                    """;

                var message = new EmailMessage
                {
                    From = "onboarding@resend.dev",
                    To = _providerEmail,
                    Subject = $"Nueva orden {order.OrderNumber} - Letra Viva",
                    HtmlBody = htmlBody
                };

                var response = await _resend.EmailSendAsync(message);

                _logger.LogInformation(
                    "Correo enviado al proveedor | OrderNumber: {OrderNumber}",
                    order.OrderNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error enviando correo al proveedor | OrderNumber: {OrderNumber}",
                    order.OrderNumber);
            }
        }
    }
}
