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
            long chatId = 0;
            try
            {
                if (update.Message is null)
                {
                    Console.WriteLine("⚠️ Update sin mensaje, ignorando.");
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(update.Message.Text))
                {
                    Console.WriteLine("⚠️ Mensaje sin texto, ignorando.");
                    return Ok();
                }

                var userMessage = update.Message.Text;
                chatId = update.Message.Chat.Id;
                var customerName = update.Message.Chat.FirstName;

                Console.WriteLine($"📩 Mensaje recibido | ChatId: {chatId} | Texto: {userMessage}");

                // CLIENTE
                Console.WriteLine("🔍 Buscando o creando cliente...");
                var customer = await _conversationService.GetOrCreateCustomer(chatId, customerName);
                Console.WriteLine($"✅ Cliente OK | Id: {customer.Id}");

                // CONVERSACIÓN
                Console.WriteLine("🔍 Buscando o creando conversación...");
                var conversation = await _conversationService.GetOrCreateConversation(customer.Id);
                Console.WriteLine($"✅ Conversación OK | Id: {conversation.Id}");

                // GUARDAR MENSAJE USUARIO
                Console.WriteLine("💾 Guardando mensaje del usuario...");
                await _conversationService.SaveMessage(conversation.Id, "user", userMessage);
                Console.WriteLine("✅ Mensaje del usuario guardado.");

                // RESPUESTA IA
                var response = await _aiService.GetResponse(userMessage);

                // GUARDAR RESPUESTA IA
                Console.WriteLine("💾 Guardando respuesta del asistente...");
                await _conversationService.SaveMessage(conversation.Id, "assistant", response);
                Console.WriteLine("✅ Respuesta del asistente guardada.");

                // RESPONDER TELEGRAM
                await _telegramService.SendMessage(chatId, response);
                Console.WriteLine("📤 Respuesta enviada a Telegram.");

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR | ChatId: {chatId} | Tipo: {ex.GetType().Name} | Mensaje: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Console.WriteLine($"❌ InnerException: {ex.InnerException.Message}");

                return Ok();
            }
        }
    }
}

