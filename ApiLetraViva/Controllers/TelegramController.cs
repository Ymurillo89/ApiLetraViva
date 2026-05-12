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

        private readonly AIService _aiService;

        public TelegramController(
            TelegramService telegramService,
            AIService aiService)
        {
            _telegramService = telegramService;

            _aiService = aiService;
        }

        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] Update update)
        {
            try
            {
                if (update.Message is null)
                    return Ok();

                var userMessage = update.Message.Text;

                var chatId = update.Message.Chat.Id;

                Console.WriteLine($"Mensaje recibido: {userMessage}");

                var response = await _aiService.GetResponse(userMessage!);

                await _telegramService.SendMessage(chatId, response);

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return Ok();
            }
        }
    }
}
