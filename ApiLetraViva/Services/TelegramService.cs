using Telegram.Bot;

namespace ApiLetraViva.Services
{
    public class TelegramService
    {
        private readonly IConfiguration _configuration;

        public TelegramService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendMessage(long chatId, string message)
        {
            var token = _configuration["TELEGRAM_BOT_TOKEN"];

            var botClient = new TelegramBotClient(token!);

            await botClient.SendMessage(
                chatId: chatId,
                text: message
            );
        }
    }
}
