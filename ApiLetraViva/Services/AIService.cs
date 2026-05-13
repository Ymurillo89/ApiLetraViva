using ApiLetraViva.Enums;
using Mscc.GenerativeAI;
using ConversationMessage = ApiLetraViva.Models.Message;

namespace ApiLetraViva.Services
{
    public class AIService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIService> _logger;

        private const string SystemPrompt = """
            Eres Celeste 🎶, asistente de Letra Viva. Tu personalidad es cálida, cercana y llena de emoción — como la música misma. Hablas en español colombiano, con naturalidad y afecto. No te presentas como IA.

            ## Estilo de comunicación
            - Mensajes cortos (máximo 3-4 líneas), pero con alma y calidez.
            - Usa emojis con moderación para transmitir emoción (🎵 🎁 💛 ✨).
            - Llama al cliente de forma cercana: "amigo/a", "cuéntame", "con gusto".
            - Transmite que detrás de cada canción hay una historia especial.

            ## Tu misión
            Ayudar a cada cliente a encontrar el paquete ideal y resolver sus dudas con calidez, para que hagan su pedido con confianza y emoción.

            ## Paquetes disponibles
            - **Mini** – $59.900 COP · 2:00-2:30 min · MP3
            - **Estándar** ⭐ – $109.900 COP · 3:30 min · MP3 + Tarjeta Digital + Foto portada
            - **Premium** – $199.900 COP · 3:30 min · MP3 + Tarjeta Digital + Video Lyric
            - Empresas: cotización personalizada

            ## Políticas clave
            - Entrega: 24 a 48 horas. Express disponible en 4 horas.
            - Revisiones: hasta 2 sin costo.
            - Contra entrega digital: primero escuchas 40-50 seg, luego pagas.
            - Pagos: PSE, Nequi, Daviplata, tarjetas, efectivo, Mercado Pago.
            - Géneros: pop, rock, balada, reggaeton, cumbia, vallenato y más.
            - Disponible en toda Latinoamérica.

            ## Cómo responder
            - Responde solo lo que el cliente pregunta, sin información extra.
            - Si no sabe qué paquete elegir, pregunta primero para qué ocasión es.
            - NO incluyas el WhatsApp en cada mensaje.
            - Solo comparte el WhatsApp (https://wa.me/573243798334) si el cliente pide hablar con una persona, tiene un problema complejo, o lo solicita expresamente.

            ## Lo que NO debes hacer
            - No prometer tiempos menores a los establecidos.
            - No dar descuentos adicionales.
            - No hablar de temas ajenos a Letra Viva.
            - No escribir mensajes largos ni repetir información ya dada.
            """;

        public AIService(IConfiguration configuration, ILogger<AIService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // Sobrecarga sin historial — compatibilidad con código existente
        public async Task<string> GetResponse(string userMessage)
        {
            return await GetResponse(userMessage, [], ConversationState.Idle);
        }

        public async Task<string> GetResponse(
            string userMessage,
            IReadOnlyList<ConversationMessage> history,
            ConversationState currentState)
        {
            try
            {
                var apiKey = _configuration["GEMINI_API_KEY"]
                    ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError("GEMINI_API_KEY no está configurada");
                    return "Lo siento, en este momento no puedo responder. Por favor intenta más tarde 🙏";
                }

                var googleAI = new GoogleAI(apiKey);
                var model = googleAI.GenerativeModel(
                    model: Model.Gemini20Flash,
                    systemInstruction: new Content(SystemPrompt)
                );

                // Construir el prompt incluyendo el historial como contexto
                var fullPrompt = BuildPromptWithHistory(userMessage, history, currentState);

                var response = await model.GenerateContent(fullPrompt);

                return response.Text ?? "No pude generar una respuesta. ¿Puedes repetir tu mensaje? 😊";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al llamar a la API de Gemini");
                return "Ups, tuve un problema técnico 😅 ¿Puedes intentarlo de nuevo en un momento?";
            }
        }

        private static string BuildPromptWithHistory(
            string userMessage,
            IReadOnlyList<ConversationMessage> history,
            ConversationState currentState)
        {
            var sb = new System.Text.StringBuilder();

            if (currentState != ConversationState.Idle)
                sb.AppendLine($"[Estado actual de la conversación: {GetStateDescription(currentState)}]\n");

            if (history.Count > 0)
            {
                sb.AppendLine("Historial de la conversación:");
                foreach (var msg in history)
                {
                    var label = msg.Role == "user" ? "Cliente" : "Celeste";
                    sb.AppendLine($"{label}: {msg.Content}");
                }
                sb.AppendLine();
            }

            sb.Append($"Cliente: {userMessage}");
            return sb.ToString();
        }

        private static string GetStateDescription(ConversationState state) => state switch
        {
            ConversationState.Idle => "inicio",
            ConversationState.DiscoveringOccasion => "preguntando por la ocasión de la canción",
            ConversationState.ChoosingPackage => "el cliente está eligiendo el paquete",
            ConversationState.CollectingDetails => "recolectando detalles (destinatario, género, mensaje)",
            ConversationState.AwaitingPayment => "esperando que el cliente realice el pago",
            ConversationState.Paid => "pago confirmado, canción en producción",
            ConversationState.InProduction => "canción en producción",
            ConversationState.Delivered => "canción entregada",
            _ => state.ToString()
        };
    }
}
