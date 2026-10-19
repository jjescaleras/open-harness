# Plan de Implementación Técnica: Plataforma Open Harness (Feature 001)

## 1. Resumen Ejecutivo y Metodología
Este plan técnico desglosa la implementación integral de la funcionalidad **001-open-harness-platform**, estructurada en cinco fases secuenciales y complementarias. Cada fase define sus entregables de ingeniería, decisiones técnicas clave, contratos de interfaz y criterios de verificación, alineándose de forma estricta con la arquitectura documentada en `PLAN_IMPLEMENTACION_HARNESS.md` y los principios de la `constitution.md`.

---

## 2. Desglose Técnico por Fases

### Fase 1: Infraestructura Base y Persistencia PostgreSQL 17 (Días 1–2)
- **Objetivo**: Establecer la estructura de la solución .NET 10, gestión centralizada de paquetes (CPM), contenedor de base de datos con extensiones vectoriales y la capa de acceso a datos con EF Core 10.
- **Componentes y Tareas Técnicas**:
  1. Configuración de `OpenHarness.slnx` con formato XML moderno, `Directory.Build.props` (net10.0, C# 14, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`) y `Directory.Packages.props`.
  2. Creación de `docker-compose.yml` para PostgreSQL 17 (`pgvector/pgvector:pg17`), exponiendo el puerto 5432 y volumen de datos persistente.
  3. Implementación del proyecto `OpenHarness.Domain`:
     - Entidad `User`: `Id` (`Guid v7`), `Email`, `PasswordHash`, `Role`, `IsActive`, auditoría temporal.
     - Entidad `RefreshToken`: `Id` (`Guid v7`), `UserId`, `TokenHash` (SHA-256), `FamilyId` (`Guid v7`), `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByTokenHash`.
     - Entidad `WorkspacePolicy`: `Id` (`Guid v7`), `OwnerId`, `RootPath`, `SandboxMode` (Enum), `ApprovalPolicy` (Enum), `RulesConfig` (Value Object / Jsonb).
     - Entidad `AgentSession`: `Id` (`Guid v7`), `UserId`, `WorkspacePolicyId`, `ParentSessionId` (nullable), `Status` (Enum), `ModelConfig`, `TotalInputTokens`, `TotalOutputTokens`, `RowVersion` (`xmin`).
     - Entidad `ConversationTurn`: `Id` (`Guid v7`), `SessionId`, `SequenceNumber` (índice único compuesto), `Role`, `Content`, `ThinkingContent`, `TokenUsage`, `Embedding` (`Vector`).
     - Entidad `ToolExecution`: `Id` (`Guid v7`), `SessionId`, `TurnId`, `CallId`, `ToolName`, `Arguments` (`JsonDocument` / Jsonb), `Result` (`JsonDocument` / Jsonb), `Status` (Enum), `RequiresApproval`, `ApprovedByUserId`, `DurationMs`.
  4. Implementación del proyecto `OpenHarness.Infrastructure` con `OpenHarnessDbContext`:
     - Mapeo nativo `jsonb` con índices GIN (`jsonb_path_ops`) en `ToolExecution.Arguments` y `ToolExecution.Result`.
     - Configuración de `ConversationTurn.Embedding` con índice `hnsw` sobre distancia coseno.
     - Configuración de concurrencia optimista con `.IsRowVersion()` mapeado a la columna PostgreSQL `xmin`.
     - Interceptor `AuditableEntityInterceptor` inyectando `TimeProvider.System`.
  5. Generación y aplicación de la migración inicial EF Core 10 (`InitialHarnessSchema`).
- **Trazabilidad**: Cumple con `FR-001`, `FR-002`, `FR-003`; valida `SC-001`.

---

### Fase 2: Autenticación JWT Zero-Allocation y Rotación por FamilyId (Días 3–4)
- **Objetivo**: Proveer un sistema de autenticación de alta velocidad sin asignaciones innecesarias, con rotación continua de tokens de refresco y revocación automática ante detección de ataques.
- **Componentes y Tareas Técnicas**:
  1. Implementación de `OpenHarness.Application/Features/Auth`:
     - Casos de uso / handlers para `RegisterCommand`, `LoginCommand`, `RefreshTokenCommand`, `RevokeTokenCommand`.
     - Servicio de hashing criptográfico seguro (Argon2id / PBKDF2) para contraseñas de usuarios.
  2. Implementación de `JwtTokenProvider` en `OpenHarness.Infrastructure`:
     - Generación de Access Tokens con `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`).
     - Firma con clave simétrica `HMAC-SHA256` o asimétrica `ECDSA P-256`, expiración a 15 minutos y claims mínimos (`sub`, `email`, `role`).
  3. Configuración del middleware de autenticación en `OpenHarness.Api`:
     - Configuración de `AddJwtBearer` con `MapInboundClaims = false` para preservar nombres estándar de claims sin overhead.
  4. Lógica de Rotación y Detección de Robo (`RefreshTokenCommand`):
     - Recepción del Refresh Token en texto plano; cálculo de su hash `SHA-256` para búsqueda en base de datos.
     - Si el token encontrado ya fue revocado o reemplazado: ejecución de `ExecuteUpdateAsync` para invalidar de inmediato todos los tokens que compartan el mismo `FamilyId`, rechazando la solicitud con error `SessionCompromised`.
     - Si el token es válido: emisión de un nuevo par (Access Token + nuevo Refresh Token con el mismo `FamilyId`), marcando el anterior como reemplazado.
  5. Batería de pruebas con `Testcontainers.PostgreSql` verificando el ciclo de vida y casos de concurrencia.
- **Trazabilidad**: Cumple con `FR-004`, `FR-005`, `FR-006`; valida `SC-002`, `SC-003`; previene `EC-002`.

---

### Fase 3: Motor del Agente Pi RPC y Abstracción IChatClient (Días 5–7)
- **Objetivo**: Integrar el binario `pi` en modo RPC JSONL como motor principal de ejecución de herramientas de código, implementar la compuerta de aprobación asíncrona de sandbox y streaming SSE nativo.
- **Componentes y Tareas Técnicas**:
  1. Implementación de `PiRpcSessionBridge` en `OpenHarness.Infrastructure.Agents.Pi`:
     - Administración de subproceso `ProcessStartInfo` con flags `--mode rpc --no-session` y redirección completa de streams (`StandardInput`, `StandardOutput`, `StandardError`).
     - Deserialización continua de eventos JSONL mediante `JsonDocument.Parse` sobre `StandardOutput.ReadLineAsync`.
     - Manejo de cierre abrupto o caída del subproceso con eventos de limpieza y actualización de estado de sesión.
  2. Implementación de `IApprovalGate` (`OpenHarness.Application.Common.Interfaces`):
     - Estructura concurrente basada en `ConcurrentDictionary<string, TaskCompletionSource<bool>>`.
     - Método `WaitForDecisionAsync(string approvalId, TimeSpan timeout, CancellationToken ct)` que suspende asíncronamente sin bloquear el thread pool.
     - Método `SetDecision(string approvalId, bool approved)` que completa la promesa pendiente.
  3. Integración de `WorkspacePolicy` y eventos de confirmación:
     - Detección de evento `extension_ui_request` proveniente de `pi`.
     - Registro en `IApprovalGate` y actualización de `ToolExecution` en base de datos a `PendingApproval`.
     - Publicación inmediata del evento SSE `sandbox.approval_required`.
  4. Endpoints Minimal API de sesiones en `OpenHarness.Api`:
     - `POST /api/v1/sessions/{id}/prompt`: Inicia el turno, almacena el mensaje del usuario y alimenta el `stdin` de `pi`.
     - `GET /api/v1/sessions/{id}/stream`: Expone el canal Server-Sent Events nativo (`TypedResults.ServerSentEvents`) transmitiendo deltas de texto, pensamiento y ciclo de vida de herramientas.
     - `POST /api/v1/sessions/{id}/approvals`: Recibe la resolución de aprobación (`{ approvalId, approved }`), libera el `TaskCompletionSource` y escribe `extension_ui_response` en el `stdin` de `pi`.
  5. Mantenimiento de la abstracción `IChatClient` (`Microsoft.Extensions.AI`) para soporte multi-proveedor y agentes secundarios.
- **Trazabilidad**: Cumple con `FR-007`, `FR-008`, `FR-009`, `FR-010`, `FR-011`; valida `SC-004`, `SC-005`; mitiga `EC-001`, `EC-003`, `EC-005`.

---

### Fase 4: SDK Cliente Core y Autenticación Transparente (Días 8–9)
- **Objetivo**: Desarrollar la librería cliente .NET 10 reutilizable para consumo de la API, manejo transparente de autenticación reactiva y consumo eficiente de Server-Sent Events.
- **Componentes y Tareas Técnicas**:
  1. Implementación del proyecto `OpenHarness.Client.Core`:
     - Contratos de clientes tipados: `IAuthApiClient`, `ISessionsApiClient`, `IApprovalsApiClient`.
  2. Implementación de `JwtAuthenticationHandler` (`DelegatingHandler`):
     - Adjunta el Access Token en memoria a cada petición HTTP (`Authorization: Bearer <token>`).
     - Al recibir respuesta HTTP 401, adquiere un `SemaphoreSlim(1, 1)`:
       - El primer hilo refresca los tokens vía `IAuthApiClient.RefreshTokenAsync`.
       - Almacena el nuevo par de tokens y reintenta la petición original.
       - Hilos concurrentes posteriores detectan que el token en memoria cambió y reintentan directamente.
  3. Implementación de `PlatformSecureTokenStorage`:
     - Abstracción para persistir el Refresh Token cifrado según el SO:
       - Linux: API de Secret Service / `libsecret`.
       - Windows: `ProtectedData.Protect` (DPAPI).
       - macOS: `Security.framework` (Keychain Services).
  4. Implementación de `HarnessSseClient`:
     - Consumo reactivo del stream SSE mediante `System.Net.ServerSentEvents.SseParser`.
     - Emisión tipada de eventos (`TextDeltaReceived`, `ToolExecutionStarted`, `SandboxApprovalRequested`, `TurnCompleted`).
     - Lógica de reconexión automática con retroceso exponencial.
- **Trazabilidad**: Cumple con `FR-012`, `FR-013`; valida `SC-007`; mitiga `EC-004`.

---

### Fase 5: Frontend UI Avalonia con Armonia Design Tokens (Días 10–12)
- **Objetivo**: Construir una interfaz de usuario de escritorio fluida, profesional y multiplataforma en Avalonia UI 11.x, garantizando streaming a 60 FPS, visor de diffs y diálogo modal de seguridad.
- **Componentes y Tareas Técnicas**:
  1. Configuración de `OpenHarness.UI.Avalonia`:
     - Arquitectura MVVM completa con `CommunityToolkit.Mvvm`.
     - Sistema de estilos centralizado en `Assets/Styles/ArmoniaDesignTokens.axaml` (paleta oscura empresarial, espaciados, tipografía).
  2. Vistas de Autenticación (`LoginView`, `RegisterView`):
     - Validación reactiva de campos y feedback de error en tiempo real.
  3. Shell Principal (`HarnessShellView`):
     - Layout ergonómico de 3 paneles: barra lateral con árbol de sesiones históricas, panel central de conversación interactiva y panel derecho con inspector de herramientas y telemetría.
  4. Vista de Conversación y Streaming (`ChatStreamView` / `ChatStreamViewModel`):
     - Acumulador de deltas de texto en búfer de memoria volcado a la UI a 60 FPS mediante `PeriodicTimer` (cadencia fija de 16 ms) para evitar saturación del `Dispatcher`.
     - Integración de `Avalonia.AvaloniaEdit` con resaltado `TextMateSharp` para bloques de código y visualización de diffs generados por el agente.
  5. Diálogo Modal de Aprobación de Sandbox (`SandboxApprovalDialog` / `SandboxApprovalViewModel`):
     - Presentación clara de la herramienta invocada, comando exacto y justificación.
     - Atajos de teclado accesibles: `Enter` (Aprobar comando único), `Ctrl+Enter` (Aprobar permanentemente para la sesión), `Esc` (Denegar ejecución).
     - Temporizador visual de expiración alineado con `EC-003`.
  6. Pruebas de integración de extremo a extremo y validación final de performance.
- **Trazabilidad**: Cumple con `FR-014`, `FR-015`; valida `SC-006`, `SC-008`; cubre `EC-003`, `EC-006`.

---

## 3. Matriz de Cobertura y Trazabilidad

| ID Requerimiento | Fase Principal | Componentes Clave | Criterio de Éxito / Caso Límite |
|---|---|---|---|
| **FR-001** | Fase 1 | `OpenHarness.slnx`, `Directory.Packages.props`, `docker-compose.yml` | `SC-001` |
| **FR-002** | Fase 1 | `OpenHarness.Domain` (Entidades con `Guid v7`) | `SC-001` |
| **FR-003** | Fase 1 | `OpenHarnessDbContext`, índices GIN, HNSW y `xmin` | `SC-001`, `EC-006` |
| **FR-004** | Fase 2 | `Auth` Feature Slices, Password Hasher | `SC-002` |
| **FR-005** | Fase 2 | `JwtTokenProvider`, `JsonWebTokenHandler` | `SC-002` |
| **FR-006** | Fase 2 | Rotación por `FamilyId`, revocación en cascada | `SC-003`, `EC-002` |
| **FR-007** | Fase 3 | `PiRpcSessionBridge` (`pi --mode rpc --no-session`) | `SC-004`, `EC-001` |
| **FR-008** | Fase 3 | `IChatClient` (`Microsoft.Extensions.AI`) | `SC-004` |
| **FR-009** | Fase 3 | `IApprovalGate`, `TaskCompletionSource` | `SC-005`, `EC-003` |
| **FR-010** | Fase 3 | `TypedResults.ServerSentEvents` (SSE nativo) | `SC-006`, `EC-004` |
| **FR-011** | Fase 3 | Endpoint `POST /api/v1/sessions/{id}/approvals` | `SC-005`, `EC-003` |
| **FR-012** | Fase 4 | `JwtAuthenticationHandler`, `SemaphoreSlim(1,1)` | `SC-007` |
| **FR-013** | Fase 4 | `PlatformSecureTokenStorage` (libsecret, DPAPI, Keychain) | `SC-007` |
| **FR-014** | Fase 5 | `HarnessShellView`, MVVM, `ArmoniaDesignTokens` | `SC-006` |
| **FR-015** | Fase 5 | Búfer a 16ms, `AvaloniaEdit` Diffs, `SandboxApprovalDialog` | `SC-006`, `SC-008`, `EC-003` |
