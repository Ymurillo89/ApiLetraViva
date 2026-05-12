using ApiLetraViva.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace ApiLetraViva.Controllers
{
    [ApiController]
    [Route("webhook/telegram")]
    public class TelegramController : ControllerBase
    {
        private readonly TelegramService _telegramService;
        private readonly ConversationService _conversationService;
        private readonly AIService _aiService;
        private readonly ILogger<TelegramController> _logger;

        public TelegramController(
            TelegramService telegramService,
            AIService aiService,
            ConversationService conversationService,
            ILogger<TelegramController> logger)
        {
            _telegramService = telegramService;
            _aiService = aiService;
            _conversationService = conversationService;
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

                var userMessage = update.Message.Text;
                chatId = update.Message.Chat.Id;
                var customerName = update.Message.Chat.FirstName;

                // CLIENTE
                var customer = await _conversationService.GetOrCreateCustomer(chatId, customerName);

                // CONVERSACIÓN
                var conversation = await _conversationService.GetOrCreateConversation(customer.Id);

                _logger.LogInformation(
                    "Webhook recibido | ChatId: {ChatId} | State: {State} | Message: {Message}",
                    chatId, conversation.State, userMessage);

                // GUARDAR MENSAJE USUARIO
                await _conversationService.SaveMessage(conversation.Id, "user", userMessage);

                // OBTENER HISTORIAL RECIENTE
                var history = await _conversationService.GetRecentMessages(conversation.Id, count: 10);

                // RESPUESTA IA (con historial y estado actual)
                var response = await _aiService.GetResponse(userMessage, history, conversation.State);

                // GUARDAR RESPUESTA IA
                await _conversationService.SaveMessage(conversation.Id, "assistant", response);

                // RESPONDER TELEGRAM
                await _telegramService.SendMessage(chatId, response);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error procesando webhook | ChatId: {ChatId} | Tipo: {ExType}",
                    chatId, ex.GetType().Name);

                return Ok();
            }
        }
    }
}
