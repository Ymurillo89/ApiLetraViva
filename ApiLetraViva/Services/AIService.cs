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
            Guiar ACTIVAMENTE a cada cliente paso a paso hasta completar su pedido. No esperes a que el cliente sepa qué hacer — tú llevas la conversación hacia la venta con calidez.

            ## Flujo obligatorio del pedido (sigue este orden siempre)
            1. **Bienvenida** – Saluda con emoción y pregunta para qué ocasión es la canción.
            2. **Ocasión** – Una vez que el cliente diga la ocasión, muestra los paquetes con sus precios y pregunta cuál prefiere.
            3. **Paquete** – Cuando elija el paquete, pide los detalles: nombre del destinatario, género musical y el mensaje o historia especial que quiere en la canción.
            4. **Detalles** – Con los detalles listos, confirma el pedido con un resumen y explica cómo es el proceso de pago (contra entrega digital: primero escucha 40-50 seg, luego paga).
            5. **Cierre** – Indica que en breve le llegará el fragmento para escuchar antes de pagar.

            IMPORTANTE: Siempre termina tu mensaje con una pregunta o acción clara para que el cliente sepa exactamente qué hacer a continuación.

            ## Paquetes disponibles
            - 🎵 **Mini** – $59.900 COP · 2:00-2:30 min · MP3
            - ⭐ **Estándar** – $109.900 COP · 3:30 min · MP3 + Tarjeta Digital + Foto portada
            - 🌟 **Premium** – $199.900 COP · 3:30 min · MP3 + Tarjeta Digital + Video Lyric
            - Empresas: cotización personalizada

            ## Políticas clave
            - Entrega: 24 a 48 horas. Express disponible en 4 horas.
            - Revisiones: hasta 2 sin costo.
            - Contra entrega digital: primero escuchas 40-50 seg, luego pagas.
            - Pagos: PSE, Nequi, Daviplata, tarjetas, efectivo, Mercado Pago.
            - Géneros: pop, rock, balada, reggaeton, cumbia, vallenato y más.
            - Disponible en toda Latinoamérica.

            ## Reglas importantes
            - Siempre avanza hacia el siguiente paso del flujo — no te quedes respondiendo sin guiar.
            - Si el cliente pregunta algo puntual (precio, tiempo), respóndelo brevemente y retoma el flujo.
            - NO incluyas el WhatsApp en cada mensaje.
            - Solo comparte el WhatsApp (https://wa.me/573243798334) si el cliente pide hablar con una persona o tiene un problema complejo.
            - No prometer tiempos menores a los establecidos.
            - No dar descuentos adicionales.
            - No hablar de temas ajenos a Letra Viva.
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
