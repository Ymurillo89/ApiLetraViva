using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ApiLetraViva.Controllers
{
    [ApiController]
    [Route("webhook/telegram")]
    public class TelegramController : Controller
    {
        private readonly IConfiguration _configuration;

        public TelegramController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] Update update)
        {
            try
            {
                if (update.Message is null)
                    return Ok();

                var message = update.Message.Text;

                var chatId = update.Message.Chat.Id;

                Console.WriteLine($"Mensaje recibido: {message}");

                var token = _configuration["TELEGRAM_BOT_TOKEN"];

                var botClient = new TelegramBotClient(token!);

                object value = await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Recibí tu mensaje: {message} 🎶"
                );

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
