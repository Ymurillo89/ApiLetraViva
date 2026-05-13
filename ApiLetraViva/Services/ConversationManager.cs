using System.Text.Json;
using ApiLetraViva.Enums;
using ApiLetraViva.Models;
using Telegram.Bot.Types;

namespace ApiLetraViva.Services
{
    public class ConversationManager
    {
        private readonly ConversationService _conversationService;
        private readonly AIService _aiService;
        private readonly OrderService _orderService;
        private readonly TelegramService _telegramService;
        private readonly ILogger<ConversationManager> _logger;

        public ConversationManager(
            ConversationService conversationService,
            AIService aiService,
            OrderService orderService,
            TelegramService telegramService,
            ILogger<ConversationManager> logger)
        {
            _conversationService = conversationService;
            _aiService = aiService;
            _orderService = orderService;
            _telegramService = telegramService;
            _logger = logger;
        }

        public async Task HandleMessageAsync(Update update)
        {
            var userMessage = update.Message!.Text!;
            var chatId = update.Message.Chat.Id;
            var customerName = update.Message.Chat.FirstName;

            // Obtener o crear cliente y conversación
            var customer = await _conversationService.GetOrCreateCustomer(chatId, customerName);
            var conversation = await _conversationService.GetOrCreateConversation(customer.Id);

            _logger.LogInformation(
                "Procesando mensaje | ChatId: {ChatId} | State: {State} | Message: {Message}",
                chatId, conversation.State, userMessage);

            // Guardar mensaje del usuario
            await _conversationService.SaveMessage(conversation.Id, "user", userMessage);

            // Obtener historial reciente
            var history = await _conversationService.GetRecentMessages(conversation.Id, count: 10);

            // Obtener respuesta estructurada de la IA
            var aiResponse = await _aiService.GetStructuredResponse(userMessage, history, conversation.State);

            // Aplicar transición de estado según intent
            var newState = ApplyStateTransition(conversation.State, aiResponse.Intent);

            // Si el pedido está listo, crearlo en la BD
            if (aiResponse.Intent == "order_ready" && conversation.State != ConversationState.AwaitingPayment)
            {
                await TryCreateOrderAsync(customer, conversation, aiResponse);
                newState = ConversationState.AwaitingPayment;
            }

            // Actualizar estado si cambió
            if (newState != conversation.State)
            {
                await _conversationService.UpdateConversationState(conversation.Id, newState);
                _logger.LogInformation(
                    "Estado actualizado | ChatId: {ChatId} | {OldState} → {NewState}",
                    chatId, conversation.State, newState);
            }

            // Guardar respuesta del asistente
            await _conversationService.SaveMessage(conversation.Id, "assistant", aiResponse.Message);

            // Enviar respuesta a Telegram
            await _telegramService.SendMessage(chatId, aiResponse.Message);
        }

        private static ConversationState ApplyStateTransition(ConversationState current, string intent) =>
            (current, intent) switch
            {
                (ConversationState.Idle, _) => ConversationState.DiscoveringOccasion,
                (ConversationState.DiscoveringOccasion, "choosing_package") => ConversationState.ChoosingPackage,
                (ConversationState.DiscoveringOccasion, "discovering_occasion") => ConversationState.DiscoveringOccasion,
                (ConversationState.ChoosingPackage, "collecting_details") => ConversationState.CollectingDetails,
                (ConversationState.CollectingDetails, "order_ready") => ConversationState.AwaitingPayment,
                _ => current
            };

        private async Task TryCreateOrderAsync(
            Customer customer,
            Conversation conversation,
            ApiLetraViva.Dtos.AIResponse aiResponse)
        {
            try
            {
                string? occasion = null, package = null, genre = null, details = null, recipient = null;

                if (aiResponse.Data.HasValue)
                {
                    var data = aiResponse.Data.Value;
                    occasion  = GetString(data, "occasion");
                    package   = GetString(data, "package");
                    genre     = GetString(data, "genre");
                    details   = GetString(data, "details");
                    recipient = GetString(data, "recipient");
                }

                if (string.IsNullOrWhiteSpace(package))
                {
                    _logger.LogWarning("order_ready sin paquete definido | ConversationId: {Id}", conversation.Id);
                    return;
                }

                // Combinar destinatario y detalles en el campo Details
                var fullDetails = string.IsNullOrWhiteSpace(recipient)
                    ? details
                    : $"Destinatario: {recipient}. {details}";

                var order = await _orderService.CreateOrderAsync(
                    customer.Id, package, occasion, genre, fullDetails);

                _logger.LogInformation(
                    "Pedido creado exitosamente | OrderId: {OrderId} | Package: {Package} | Total: {Total}",
                    order.Id, order.Package, order.Total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando pedido | CustomerId: {CustomerId}", customer.Id);
            }
        }

        private static string? GetString(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                var val = prop.GetString();
                return string.IsNullOrWhiteSpace(val) || val == "null" ? null : val;
            }
            return null;
        }
    }
}
