using System.Text.Json;
using ApiLetraViva.Dtos;
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

            ## Tu misión principal
            Guiar ACTIVAMENTE a cada cliente paso a paso hasta completar su pedido.

            ## Flujo obligatorio del pedido
            1. **Bienvenida** – Saluda con emoción y pregunta para qué ocasión es la canción.
            2. **Ocasión** – Cuando el cliente diga la ocasión, muestra los paquetes y pregunta cuál prefiere.
            3. **Paquete** – Cuando elija el paquete, pide: nombre del destinatario, género musical y el mensaje especial.
            4. **Detalles** – Con todos los detalles listos, confirma el pedido con un resumen y explica el proceso de pago.
            5. **Cierre** – Indica que en breve le llegará el fragmento para escuchar antes de pagar.

            Siempre termina tu mensaje con una pregunta o acción clara.

            ## Paquetes disponibles
            - 🎵 **Mini** – $59.900 COP · 2:00-2:30 min · MP3
            - ⭐ **Estándar** – $109.900 COP · 3:30 min · MP3 + Tarjeta Digital + Foto portada
            - 🌟 **Premium** – $199.900 COP · 3:30 min · MP3 + Tarjeta Digital + Video Lyric

            ## Políticas clave
            - Entrega: 24 a 48 horas. Express en 4 horas.
            - Revisiones: hasta 2 sin costo.
            - Contra entrega digital: primero escuchas 40-50 seg, luego pagas.
            - Pagos: PSE, Nequi, Daviplata, tarjetas, efectivo, Mercado Pago.

            ## FORMATO DE RESPUESTA (OBLIGATORIO)
            Responde SIEMPRE con un JSON válido con esta estructura exacta:
            {
              "message": "tu respuesta al cliente aquí",
              "intent": "uno de: greeting, discovering_occasion, choosing_package, collecting_details, order_ready, ask_price, ask_delivery, payment_question, other",
              "data": {
                "occasion": "ocasión si ya fue mencionada, sino null",
                "package": "Mini, Estándar o Premium si ya fue elegido, sino null",
                "recipient": "nombre del destinatario si fue dado, sino null",
                "genre": "género musical si fue dado, sino null",
                "details": "mensaje especial si fue dado, sino null"
              }
            }

            El intent "order_ready" se usa SOLO cuando tienes: occasion, package, recipient, genre Y details completos.
            No incluyas texto fuera del JSON.
            """;

        public AIService(IConfiguration configuration, ILogger<AIService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AIResponse> GetStructuredResponse(
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
                    return Fallback("Lo siento, en este momento no puedo responder. Por favor intenta más tarde 🙏");
                }

                var googleAI = new GoogleAI(apiKey);
                var model = googleAI.GenerativeModel(
                    model: Model.Gemini20Flash,
                    systemInstruction: new Content(SystemPrompt)
                );

                var fullPrompt = BuildPromptWithHistory(userMessage, history, currentState);
                var response = await model.GenerateContent(fullPrompt);
                var rawText = response.Text ?? string.Empty;

                return ParseResponse(rawText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al llamar a la API de Gemini");
                return Fallback("Ups, tuve un problema técnico 😅 ¿Puedes intentarlo de nuevo en un momento?");
            }
        }

        private AIResponse ParseResponse(string rawText)
        {
            try
            {
                // Limpiar posibles bloques de código markdown
                var json = rawText.Trim();
                if (json.StartsWith("```"))
                {
                    var start = json.IndexOf('{');
                    var end = json.LastIndexOf('}');
                    if (start >= 0 && end > start)
                        json = json[start..(end + 1)];
                }

                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var message = root.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString() ?? rawText
                    : rawText;

                var intent = root.TryGetProperty("intent", out var intentProp)
                    ? intentProp.GetString() ?? "other"
                    : "other";

                var data = root.TryGetProperty("data", out var dataProp)
                    ? (JsonElement?)dataProp
                    : null;

                return new AIResponse(message, intent, data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo parsear respuesta JSON de Gemini: {Raw}", rawText);
                return new AIResponse(rawText, "other", null);
            }
        }

        private static AIResponse Fallback(string message) =>
            new(message, "other", null);

        private static string BuildPromptWithHistory(
            string userMessage,
            IReadOnlyList<ConversationMessage> history,
            ConversationState currentState)
        {
            var sb = new System.Text.StringBuilder();

            if (currentState != ConversationState.Idle)
                sb.AppendLine($"[Estado actual: {GetStateDescription(currentState)}]\n");

            if (history.Count > 0)
            {
                sb.AppendLine("Historial:");
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
            ConversationState.DiscoveringOccasion => "preguntando por la ocasión",
            ConversationState.ChoosingPackage => "eligiendo paquete",
            ConversationState.CollectingDetails => "recolectando detalles",
            ConversationState.AwaitingPayment => "esperando pago",
            ConversationState.Paid => "pago confirmado",
            ConversationState.InProduction => "en producción",
            ConversationState.Delivered => "entregado",
            _ => state.ToString()
        };
    }
}
