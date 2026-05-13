using ApiLetraViva.Context;
using ApiLetraViva.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Obtener connection string y convertir formato URI a formato Npgsql si es necesario
var rawConnectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontró la connection string. Define DATABASE_URL como variable de entorno en Railway.");

var connectionString = ConvertToNpgsqlFormat(rawConnectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<TelegramService>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ConversationManager>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Sin UseHttpsRedirection — Railway maneja HTTPS externamente
app.UseAuthorization();

app.MapControllers();

// Aplicar migraciones al iniciar — protegido para no crashear la app
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    Console.WriteLine("✅ Migraciones aplicadas correctamente.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error aplicando migraciones: {ex.Message}");
    // No relanzamos — la app sigue corriendo aunque las migraciones fallen
}

app.Run();

// Convierte postgresql://user:pass@host:port/db → Host=host;Port=port;Database=db;Username=user;Password=pass
static string ConvertToNpgsqlFormat(string connectionString)
{
    if (!connectionString.StartsWith("postgresql://") && !connectionString.StartsWith("postgres://"))
        return connectionString; // Ya está en formato Npgsql, no convertir

    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo[0];
    var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');

    return $"Host={host};Port={port};Database={database};Username={username};Password={password};";
}
