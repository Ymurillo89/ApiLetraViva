using ApiLetraViva.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
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

        public TelegramController(
            TelegramService telegramService,
            AIService aiService,
            ConversationService conversationService)
        {
            _telegramService = telegramService;

            _aiService = aiService;

            _conversationService = conversationService;
        }

        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] Update update)
        {
            try
            {
                if (update.Message is null)
                    return Ok();

                if (string.IsNullOrWhiteSpace(update.Message.Text))
                    return Ok();

                var userMessage = update.Message.Text;

                var chatId = update.Message.Chat.Id;

                var customerName = update.Message.Chat.FirstName;

                Console.WriteLine($"Mensaje recibido: {userMessage}");

                // =========================
                // CLIENTE
                // =========================

                var customer =
                    await _conversationService
                    .GetOrCreateCustomer(
                        chatId,
                        customerName
                    );

                // =========================
                // CONVERSACIÓN
                // =========================

                var conversation =
                    await _conversationService
                    .GetOrCreateConversation(
                        customer.Id
                    );

                // =========================
                // GUARDAR MENSAJE USUARIO
                // =========================

                await _conversationService
                    .SaveMessage(
                        conversation.Id,
                        "user",
                        userMessage
                    );

                // =========================
                // RESPUESTA IA
                // =========================

                var response =
                    await _aiService
                    .GetResponse(userMessage);

                // =========================
                // GUARDAR RESPUESTA IA
                // =========================

                await _conversationService
                    .SaveMessage(
                        conversation.Id,
                        "assistant",
                        response
                    );

                // =========================
                // RESPONDER TELEGRAM
                // =========================

                await _telegramService
                    .SendMessage(chatId, response);

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                return Ok();
            }
        }
    }
}

