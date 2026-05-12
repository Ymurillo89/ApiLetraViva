# Requirements Document

## Introduction

Celeste es el agente conversacional de LetraViva, un servicio que vende canciones personalizadas a través de Telegram (y en el futuro WhatsApp). El backend es ASP.NET Core (.NET 8) desplegado en Railway con PostgreSQL.

El objetivo del MVP es validar si los usuarios realmente compran canciones personalizadas a través de Celeste. El sistema debe guiar al usuario desde el primer mensaje hasta el pago, pasando por la selección de paquete, recolección de detalles y confirmación del pedido, con soporte de IA (GPT-4o-mini) para interpretar intenciones y generar respuestas con personalidad.

La arquitectura objetivo es: Telegram / WhatsApp → Webhook API → Conversation Manager → OpenAI → Tools Layer → PostgreSQL → Payments / Orders.

---

## Glossary

- **Celeste**: El agente conversacional de LetraViva que interactúa con los clientes.
- **ConversationManager**: Componente responsable de orquestar el flujo conversacional: leer el estado actual, decidir la siguiente acción y actualizar el estado.
- **ConversationState**: Enum que representa la etapa actual de una conversación. Valores: `Idle`, `DiscoveringOccasion`, `ChoosingPackage`, `CollectingDetails`, `AwaitingPayment`, `Paid`, `InProduction`, `Delivered`.
- **Customer**: Entidad que representa a un usuario registrado, identificado por su `TelegramChatId`.
- **Conversation**: Entidad que agrupa los mensajes de un cliente y mantiene el estado actual del flujo.
- **Message**: Entidad que representa un mensaje individual dentro de una conversación, con rol `user` o `assistant`.
- **Order**: Entidad que representa un pedido de canción personalizada, con ocasión, género, paquete, detalles y estado de pago.
- **Package**: Paquete de canción personalizada. Valores disponibles: `Básico` ($99.900), `Estándar` ($149.900), `Premium` ($199.900).
- **Intent**: Intención detectada por la IA en el mensaje del usuario. Valores: `create_order`, `ask_price`, `ask_delivery`, `payment_question`, `other`.
- **PromptBuilder**: Componente que construye el prompt del sistema para OpenAI, incluyendo personalidad de Celeste, reglas de negocio, contexto de la conversación y estado actual.
- **IntentExtractor**: Componente que analiza la respuesta estructurada de OpenAI para extraer la intención y los datos del usuario.
- **AIResponse**: Estructura de respuesta de OpenAI con campos `message` (string), `intent` (string) y `data` (objeto JSON).
- **TelegramService**: Servicio responsable de enviar mensajes y acciones de escritura a Telegram.
- **OrderService**: Servicio responsable de crear, actualizar y consultar pedidos.
- **PaymentService**: Servicio responsable de generar enlaces de pago con Wompi y procesar webhooks de confirmación.
- **Wompi**: Pasarela de pagos colombiana utilizada para procesar los pagos de los pedidos.
- **WebhookController**: Controlador que recibe actualizaciones de Telegram en `POST /webhook/telegram` y de Wompi en `POST /webhook/payments`.

---

## Requirements

### Requirement 1: Gestión de clientes y conversaciones

**User Story:** Como operador de LetraViva, quiero que Celeste identifique automáticamente a cada usuario de Telegram y mantenga una conversación persistente, para que el historial y el estado del flujo no se pierdan entre mensajes.

#### Acceptance Criteria

1. WHEN un mensaje de Telegram es recibido en `POST /webhook/telegram`, THE ConversationManager SHALL identificar al cliente por su `TelegramChatId` y crearlo en la base de datos si no existe.
2. WHEN un cliente es identificado, THE ConversationManager SHALL obtener o crear la conversación activa asociada a ese cliente.
3. THE Conversation SHALL mantener el `ConversationState` actual y un campo `Context` en formato JSON con los datos recolectados durante el flujo.
4. WHEN el nombre del usuario está disponible en el update de Telegram, THE ConversationManager SHALL persistir el nombre en el campo `Name` del Customer.
5. IF el `TelegramChatId` no puede ser extraído del update de Telegram, THEN THE WebhookController SHALL retornar HTTP 200 sin procesar el mensaje.

---

### Requirement 2: Flujo conversacional guiado por estados

**User Story:** Como cliente de LetraViva, quiero que Celeste me guíe paso a paso para pedir mi canción personalizada, para que el proceso sea claro y no me pierda.

#### Acceptance Criteria

1. WHEN el `ConversationState` es `Idle` y el usuario envía cualquier mensaje, THE ConversationManager SHALL transicionar el estado a `DiscoveringOccasion` y responder preguntando la ocasión de la canción.
2. WHEN el `ConversationState` es `DiscoveringOccasion` y el usuario proporciona una ocasión, THE ConversationManager SHALL guardar la ocasión en el `Context` de la conversación, transicionar el estado a `ChoosingPackage` y presentar los paquetes disponibles con sus precios.
3. WHEN el `ConversationState` es `ChoosingPackage` y el usuario selecciona un paquete válido, THE ConversationManager SHALL guardar el paquete en el `Context`, transicionar el estado a `CollectingDetails` y solicitar los detalles de la canción (nombre del destinatario, género musical, mensaje especial).
4. WHEN el `ConversationState` es `CollectingDetails` y el usuario proporciona los detalles requeridos, THE ConversationManager SHALL guardar los detalles en el `Context`, crear un Order en la base de datos y transicionar el estado a `AwaitingPayment`.
5. WHEN el `ConversationState` es `AwaitingPayment`, THE ConversationManager SHALL presentar al usuario un resumen del pedido con paquete, precio, tiempo de entrega y enlace de pago de Wompi.
6. WHEN el `ConversationState` es `Paid`, THE ConversationManager SHALL confirmar al usuario que el pago fue recibido y que la canción está en producción, y transicionar el estado a `InProduction`.
7. WHEN el `ConversationState` es `InProduction`, THE ConversationManager SHALL responder al usuario que su canción está siendo creada y que será notificado cuando esté lista.
8. WHEN el `ConversationState` es `Delivered`, THE ConversationManager SHALL confirmar al usuario que la canción fue entregada y ofrecer la posibilidad de hacer un nuevo pedido.
9. IF el usuario envía un mensaje que no corresponde al paso esperado del flujo, THEN THE ConversationManager SHALL responder con un mensaje de orientación que indique qué información se espera en ese momento.

---

### Requirement 3: Integración con Telegram

**User Story:** Como cliente de LetraViva, quiero recibir respuestas de Celeste en Telegram de forma fluida, para que la experiencia se sienta natural y responsiva.

#### Acceptance Criteria

1. WHEN Celeste va a enviar una respuesta, THE TelegramService SHALL enviar primero una acción de escritura (`SendChatAction` con `typing`) al chat del usuario.
2. WHEN una respuesta está lista, THE TelegramService SHALL enviar el mensaje de texto al `TelegramChatId` del usuario.
3. THE TelegramService SHALL leer el token del bot desde la variable de entorno `TELEGRAM_BOT_TOKEN`.
4. IF el envío de un mensaje a Telegram falla, THEN THE TelegramService SHALL registrar el error en los logs del sistema y no lanzar una excepción que interrumpa el flujo principal.
5. THE WebhookController SHALL retornar HTTP 200 a Telegram en todos los casos, incluso cuando ocurra un error interno, para evitar reintentos del webhook.

---

### Requirement 4: Registro y gestión de pedidos

**User Story:** Como operador de LetraViva, quiero que cada pedido quede registrado en la base de datos con todos sus detalles, para poder gestionarlo y hacer seguimiento.

#### Acceptance Criteria

1. WHEN el usuario completa la recolección de detalles, THE OrderService SHALL crear un Order con los campos: `CustomerId`, `Package`, `Occasion`, `Genre`, `Details`, `Total` y `Status = "Pending"`.
2. WHEN un Order es creado, THE OrderService SHALL calcular el `Total` según el paquete seleccionado: Básico = $99.900, Estándar = $149.900, Premium = $199.900.
3. WHEN el pago de un Order es confirmado por Wompi, THE OrderService SHALL actualizar el `PaymentStatus` a `"Paid"` y el `Status` a `"InProduction"`.
4. WHEN el operador marca un Order como entregado, THE OrderService SHALL actualizar el `Status` a `"Delivered"` y el `ConversationState` asociado a `Delivered`.
5. THE OrderService SHALL exponer un método `GetOrder(Guid orderId)` que retorne el Order completo con su Customer asociado.
6. IF se intenta crear un Order con un `Package` que no existe en el catálogo, THEN THE OrderService SHALL lanzar una excepción de validación con un mensaje descriptivo.

---

### Requirement 5: Integración con IA (OpenAI GPT-4o-mini)

**User Story:** Como cliente de LetraViva, quiero que Celeste entienda mis mensajes aunque no sigan el formato exacto esperado, para que la conversación se sienta natural.

#### Acceptance Criteria

1. WHEN un mensaje de usuario es recibido, THE AIService SHALL enviar el mensaje junto con el historial reciente de la conversación y el estado actual a OpenAI GPT-4o-mini.
2. THE PromptBuilder SHALL construir el prompt del sistema incluyendo: la personalidad de Celeste (cálida, creativa, colombiana), las reglas de negocio (paquetes, precios, tiempos de entrega), el `ConversationState` actual y los últimos 10 mensajes del historial.
3. THE AIService SHALL solicitar a OpenAI una respuesta en formato JSON estructurado con los campos `message` (string), `intent` (string) y `data` (objeto).
4. THE IntentExtractor SHALL parsear la respuesta JSON de OpenAI y extraer el `intent` y el `data` para que el ConversationManager pueda tomar decisiones de flujo.
5. WHEN el `intent` extraído es `create_order`, THE ConversationManager SHALL iniciar o continuar el flujo de creación de pedido.
6. WHEN el `intent` extraído es `ask_price`, THE ConversationManager SHALL responder con la lista de paquetes y precios sin cambiar el estado actual.
7. WHEN el `intent` extraído es `ask_delivery`, THE ConversationManager SHALL responder con los tiempos de entrega por paquete sin cambiar el estado actual.
8. WHEN el `intent` extraído es `payment_question`, THE ConversationManager SHALL responder con información sobre los métodos de pago disponibles sin cambiar el estado actual.
9. IF la respuesta de OpenAI no puede ser parseada como JSON válido, THEN THE IntentExtractor SHALL retornar un AIResponse con `intent = "other"` y el texto crudo de la respuesta en el campo `message`.
10. FOR ALL mensajes de usuario válidos, parsear la respuesta de OpenAI y volver a parsear el resultado SHALL producir un AIResponse equivalente (propiedad round-trip del parser JSON).

---

### Requirement 6: Integración de pagos con Wompi

**User Story:** Como cliente de LetraViva, quiero poder pagar mi pedido directamente desde el chat de Telegram, para que el proceso de compra sea rápido y sin fricciones.

#### Acceptance Criteria

1. WHEN un Order transiciona al estado `AwaitingPayment`, THE PaymentService SHALL generar un enlace de pago de Wompi para ese Order usando el `orderId` como referencia única.
2. THE PaymentService SHALL leer las credenciales de Wompi desde las variables de entorno `WOMPI_PUBLIC_KEY` y `WOMPI_PRIVATE_KEY`.
3. WHEN Wompi envía una notificación al webhook `POST /webhook/payments`, THE WebhookController SHALL validar la firma del evento usando el `WOMPI_EVENTS_SECRET`.
4. WHEN la firma del evento de Wompi es válida y el estado de la transacción es `APPROVED`, THE PaymentService SHALL actualizar el Order correspondiente y notificar al ConversationManager para avanzar el estado de la conversación.
5. IF la firma del evento de Wompi no es válida, THEN THE WebhookController SHALL retornar HTTP 401 y registrar el intento en los logs.
6. IF el `orderId` de referencia en el evento de Wompi no corresponde a ningún Order existente, THEN THE PaymentService SHALL registrar el error en los logs y retornar sin lanzar excepción.

---

### Requirement 7: Corrección de la migración de base de datos

**User Story:** Como desarrollador de LetraViva, quiero que el esquema de base de datos refleje todos los campos necesarios para el funcionamiento del sistema, para que no haya errores en tiempo de ejecución.

#### Acceptance Criteria

1. THE Database SHALL incluir la columna `TelegramChatId` (tipo `bigint`, no nulo) en la tabla `Customers`.
2. THE Database SHALL incluir una tabla `Payments` con los campos: `Id` (UUID), `OrderId` (UUID, FK a Orders), `WompiTransactionId` (string), `Amount` (decimal), `Status` (string), `CreatedAt` (timestamp).
3. WHEN se ejecuta la migración de base de datos, THE Database SHALL aplicar los cambios sin errores en el entorno de Railway con PostgreSQL.
4. THE AppDbContext SHALL exponer un `DbSet<Payment>` para la entidad Payment.

---

### Requirement 8: Observabilidad y manejo de errores

**User Story:** Como operador de LetraViva, quiero que los errores queden registrados con suficiente contexto, para poder diagnosticar problemas en producción sin interrumpir el servicio.

#### Acceptance Criteria

1. WHEN una excepción no controlada ocurre en el WebhookController, THE WebhookController SHALL capturarla, registrar el stack trace completo en los logs del sistema y retornar HTTP 200 a Telegram.
2. WHEN una llamada a OpenAI falla por timeout o error de red, THE AIService SHALL registrar el error y retornar un AIResponse con `intent = "other"` y un mensaje de fallback predefinido en español.
3. WHEN una llamada a Wompi falla, THE PaymentService SHALL registrar el error con el `orderId` afectado y retornar un resultado de error sin lanzar excepción al caller.
4. THE System SHALL registrar en los logs el `TelegramChatId`, el `ConversationState` actual y el texto del mensaje de usuario al inicio de cada procesamiento de webhook.
