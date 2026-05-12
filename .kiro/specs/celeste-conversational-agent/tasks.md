# Plan de Implementación: Celeste — Telegram MVP

## Overview

Objetivo de este plan: tener Telegram funcionando de extremo a extremo — recibir mensajes, guardarlos en PostgreSQL y responder. Sin pagos, sin IA real, sin panel admin. Solo el flujo base validado.

## Tasks

- [ ] 1. Corregir la migración de base de datos
  - [ ] 1.1 Verificar que `Customer.TelegramChatId` es `long` (no nullable) en el modelo
    - El modelo ya tiene `public long TelegramChatId { get; set; }` — confirmar que la migración existente lo incluye
    - Si la migración `InitialCreate` no tiene la columna `TelegramChatId`, crear una nueva migración `AddTelegramChatId`
    - _Requirements: 7.1_
  - [ ] 1.2 Ejecutar la migración pendiente en Railway
    - Agregar en `Program.cs` la llamada `app.Services.CreateScope()` para aplicar migraciones automáticamente al iniciar
    - Verificar en los logs de Railway que la migración se aplica sin errores
    - _Requirements: 7.3_

- [ ] 2. Completar `ConversationService` con los métodos faltantes
  - [ ] 2.1 Agregar `UpdateConversationStateAsync` al `ConversationService`
    - Firma: `Task UpdateConversationStateAsync(Guid conversationId, ConversationState state)`
    - Actualizar `Conversation.State` y `Conversation.UpdatedAt = DateTime.UtcNow` y guardar
    - _Requirements: 1.3_
  - [ ] 2.2 Agregar `GetRecentMessagesAsync` al `ConversationService`
    - Firma: `Task<List<Message>> GetRecentMessagesAsync(Guid conversationId, int count = 10)`
    - Retornar los últimos `count` mensajes ordenados por `CreatedAt` ascendente
    - _Requirements: 1.3_

- [ ] 3. Mejorar `TelegramService` con `SendTypingAsync`
  - [ ] 3.1 Agregar `SendTypingAsync(long chatId)` al `TelegramService`
    - Usar `botClient.SendChatAction(chatId, ChatAction.Typing)`
    - En caso de fallo, registrar el error con `ILogger` y no lanzar excepción
    - _Requirements: 3.1, 3.4_
  - [ ] 3.2 Agregar `ILogger<TelegramService>` al constructor de `TelegramService`
    - Reemplazar `IConfiguration` directo por inyección de `ILogger` también
    - Registrar errores de envío con `_logger.LogError(ex, "Error enviando mensaje a chatId {ChatId}", chatId)`
    - _Requirements: 3.4, 8.1_

- [ ] 4. Refactorizar `TelegramController` para separar responsabilidades
  - [ ] 4.1 Extraer toda la lógica de procesamiento del controller al `ConversationService`
    - El controller solo debe: extraer `chatId` y `text` del `Update`, llamar a un método de procesamiento, retornar `Ok()`
    - Si `update.Message` es null o `update.Message.Text` está vacío, retornar `Ok()` inmediatamente
    - Si `update.Message.Chat.Id` no puede extraerse, retornar `Ok()` sin procesar
    - _Requirements: 1.5, 3.5_
  - [ ] 4.2 Envolver todo el body del endpoint en `try/catch`
    - Capturar cualquier excepción, registrarla con `ILogger` incluyendo el stack trace completo
    - Siempre retornar `Ok()` — nunca retornar 4xx o 5xx al webhook de Telegram
    - _Requirements: 3.5, 8.1_
  - [ ] 4.3 Agregar `SendTypingAsync` antes de procesar la respuesta
    - Llamar a `_telegramService.SendTypingAsync(chatId)` justo antes de generar la respuesta
    - _Requirements: 3.1_
  - [ ] 4.4 Registrar log de entrada al inicio de cada mensaje recibido
    - `_logger.LogInformation("Webhook recibido | ChatId: {ChatId} | State: {State} | Message: {Message}", chatId, conversation.State, userMessage)`
    - _Requirements: 8.4_

- [ ] 5. Verificar flujo completo end-to-end
  - [ ] 5.1 Compilar el proyecto sin errores
    - Ejecutar `dotnet build` y corregir cualquier error de compilación
  - [ ] 5.2 Verificar que el webhook de Telegram está configurado en Railway
    - Confirmar que la variable de entorno `TELEGRAM_BOT_TOKEN` está seteada
    - Confirmar que el webhook apunta a `https://<railway-url>/webhook/telegram`
  - [ ] 5.3 Probar el flujo completo manualmente
    - Enviar un mensaje al bot en Telegram
    - Verificar en los logs de Railway que el mensaje fue recibido y guardado
    - Verificar en PostgreSQL que existe el registro en `Customers`, `Conversations` y `Messages`
    - Verificar que el bot responde en Telegram

## Notes

- Sin pagos (Wompi), sin IA real (OpenAI), sin panel admin por ahora
- El `AIService` actual (stub que hace echo) es suficiente para este MVP
- Una vez validado este flujo, el siguiente paso natural es integrar OpenAI para respuestas reales
- La migración automática en `Program.cs` es conveniente para Railway; en producción madura se prefiere migrar manualmente
