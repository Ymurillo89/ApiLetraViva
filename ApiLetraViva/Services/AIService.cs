namespace ApiLetraViva.Services
{
    public class AIService
    {
        public async Task<string> GetResponse(string userMessage)
        {
            await Task.Delay(100);

            return $"IA dice: {userMessage}";
        }
    }
}
