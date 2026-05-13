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
        private readonly ILogger<TelegramController> _logger;

        public TelegramController(
            ConversationManager conversationManager,
            ILogger<TelegramController> logger)
        {
            _conversationManager = conversationManager;
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
    }
}
