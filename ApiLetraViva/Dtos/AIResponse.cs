using System.Text.Json;

namespace ApiLetraViva.Dtos
{
    public record AIResponse(
        string Message,
        string Intent,
        JsonElement? Data
    );
}
