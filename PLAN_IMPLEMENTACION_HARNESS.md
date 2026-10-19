# Plan Maestro de Implementación: Open Harness (.NET 10 + EF Core 10 + PostgreSQL 17 + Pi RPC + UI Armonia/Avalonia + JWT)

## 1. Visión Arquitectónica y Decisiones de Diseño

Este plan define la implementación de extremo a extremo de **Open Harness**, una plataforma enterprise de orquestación de agentes de IA y ejecución controlada de herramientas (*tool-calling* y *sandboxing*), construida sobre las versiones más modernas del ecosistema .NET e integrada nativamente con **Pi (`pi.dev`)**:

1. **Frontend UI (`Armonia` / `Avalonia UI` sobre .NET 10)**:
   - Se estructura en dos capas estrictamente separadas: un SDK cliente puro en .NET 10 (`OpenHarness.Client.Core`) que encapsula HTTP, autenticación JWT reactiva y streaming en tiempo real, y la capa visual multiplataforma (`OpenHarness.UI.Avalonia`) basada en **Avalonia UI** con patrón **MVVM (`CommunityToolkit.Mvvm 8.4`)**, `AvaloniaEdit` para diffs y un sistema de tokens de diseño (`ArmoniaDesignTokens.axaml`).
   - *Nota de compatibilidad*: Al separar `OpenHarness.Client.Core`, si el contenedor visual se despliega en Desktop (Linux Wayland/X11, Windows, macOS), WebAssembly o un frontend externo con el sistema de diseño Armonia, los contratos de API, autenticación JWT y streaming permanecen idénticos.
2. **Motor del Agente con Integración Nativa de Pi (`pi --mode rpc --no-session`) + Streaming SSE**:
   - En lugar de reimplementar las herramientas de bajo nivel desde cero (`read`, `write`, `edit` con *fuzzy matching*, `bash`), `OpenHarness.Infrastructure` integra **Pi en modo RPC (`pi --mode rpc --no-session`)** mediante un puente asíncrono (`PiRpcSessionBridge` sobre `stdin`/`stdout` JSONL), manteniendo la abstracción `IChatClient` (`Microsoft.Extensions.AI`) para flexibilidad multi-proveedor.
   - **PostgreSQL 17 (vía EF Core 10)** es la única fuente de verdad de las sesiones, turnos y auditoría de herramientas (por ello se invoca `pi` con `--no-session`).
   - Las solicitudes de confirmación (`extension_ui_request` de Pi o evaluación de `WorkspacePolicy`) pausan la ejecución de forma asíncrona (`IApprovalGate` con `TaskCompletionSource<ApprovalDecision>`), se envían por **Server-Sent Events (SSE) nativo de ASP.NET Core 10 (`TypedResults.ServerSentEvents`)** a la UI de Avalonia (`System.Net.ServerSentEvents.SseParser`), muestran el `SandboxApprovalDialog`, y al recibir `POST /api/v1/sessions/{id}/approvals` devuelven `extension_ui_response` al proceso `pi`.
3. **Seguridad y Autenticación JWT Zero-Allocation**:
   - Emisión y validación en .NET 10 mediante `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`), `AddJwtBearer` con `MapInboundClaims = false`, **Access Tokens** de corta vida (10 min) y **Refresh Tokens rotatorios** (7 días) en PostgreSQL con detección automática de robo de sesión por familia (`FamilyId`) mediante `ExecuteUpdateAsync`.
4. **Persistencia Moderna (EF Core 10 + PostgreSQL 17)**:
   - Contenedor `pgvector/pgvector:pg17` con extensiones `vector`, `uuid-ossp` y `pg_trgm`.
   - Uso de `Npgsql.EntityFrameworkCore.PostgreSQL 10.0` con `EnableDynamicJson()`, `AddDbContextPool`, `EnableRetryOnFailure` y `UseSnakeCaseNamingConvention()`.
   - Claves primarias secuenciales `Guid.CreateVersion7()`, mapeo `.ToJson()` para configuraciones/métricas y columnas nativas `jsonb` con índices **GIN (`jsonb_path_ops`)** para argumentos y resultados de herramientas, concurrencia optimista con `xmin` e indexación vectorial **HNSW (`pgvector`)** para memoria semántica.

---

## 2. Arquitectura de Integración (.NET 10 ↔ Pi RPC ↔ PostgreSQL 17 ↔ Avalonia UI)

1. `POST /api/v1/sessions/{id}/prompt` persiste el `ConversationTurn` del usuario en PostgreSQL y envía `{"type": "prompt", "message": "..."}` por `stdin` al proceso `pi --mode rpc --no-session`.
2. `PiRpcSessionBridge` lee el flujo JSONL de `stdout`:
   - `message_update` -> emite evento SSE `text.delta` / `thinking.delta`.
   - `tool_execution_start` -> registra `ToolExecution` (`jsonb`) en PostgreSQL y emite `tool.started`.
   - `extension_ui_request` (o regla de `WorkspacePolicy`) -> pausa en `IApprovalGate`, emite `sandbox.approval_required` a Avalonia UI y, tras `POST /api/v1/sessions/{id}/approvals`, envía `extension_ui_response` a `pi`.
   - `tool_execution_end` -> actualiza `ToolExecution` (`Result jsonb`, `DurationMs`) y emite `tool.completed`.
   - `agent_end` -> persiste el `ConversationTurn` del asistente con `TokenUsage` (`.ToJson()`) y emite `turn.completed`.

---

## 3. Catálogo de Versiones y Paquetes NuGet

| Capa | Paquete / Tecnología | Versión | Propósito |
|---|---|---|---|
| **SDK / Runtime** | `.NET SDK` / `C#` | `10.0.x` / `C# 14` | Runtime unificado con soporte de solución `.slnx` y mejoras AOT/Span. |
| **Motor Agéntico RPC** | `pi` CLI (`pi.dev`) | `latest` (`--mode rpc --no-session`) | Ejecución de herramientas de código, edición con fuzzy matching y bash sobre JSONL. |
| **Backend Auth** | `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.0.*` | Middleware de autenticación JWT Bearer en ASP.NET Core 10. |
| **Backend Auth** | `Microsoft.IdentityModel.JsonWebTokens` | `8.x` / `10.0.*` | `JsonWebTokenHandler` de alto rendimiento y cero asignaciones innecesarias. |
| **Datos / ORM** | `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.*` | Proveedor oficial EF Core 10 para PostgreSQL 17. |
| **Datos / Naming** | `EFCore.NamingConventions` | `10.0.*` | Convención automática `snake_case` para tablas, columnas e índices en PostgreSQL. |
| **Datos / Vectores** | `Pgvector.EntityFrameworkCore` | `0.3.*` | Soporte de columnas `vector(1536)` e índices `hnsw` en EF Core 10. |
| **Motor IA** | `Microsoft.Extensions.AI` | `9.x` / `10.0.*` | Abstracción unificada `IChatClient` para flexibilidad multi-proveedor. |
| **Frontend UI** | `Avalonia` + `Avalonia.Themes.Fluent` | `11.3.*` / `12.1.*` | Renderizado UI multiplataforma acelerado por GPU sobre .NET 10. |
| **Frontend MVVM** | `CommunityToolkit.Mvvm` | `8.4.*` | Source Generators (`[ObservableProperty]`, `[RelayCommand]`, `IMessenger`). |
| **Editor / Diffs** | `Avalonia.AvaloniaEdit` + `TextMateSharp` | `11.2.*` | Bloques de código, resaltado de sintaxis, diffs y terminal virtualizada. |
| **Streaming Cliente** | `System.Net.ServerSentEvents` / `SignalR.Client` | `10.0.*` | Consumo nativo de streams SSE (`SseParser`) y WebSockets autenticados. |

---

## 4. Estructura de la Solución (`OpenHarness.slnx`)

- `OpenHarness.slnx`: Formato XML moderno de solución .NET 10.
- `Directory.Build.props`: `TargetFramework=net10.0`, `LangVersion=14.0`, `Nullable=enable`.
- `Directory.Packages.props`: Central Package Management (CPM).
- `docker-compose.yml`: PostgreSQL 17 (`pgvector/pgvector:pg17`).
- `.speckit/`: Artefactos de Spec-Driven Development (`constitution.md` y `features/001-open-harness-core/`).
- `src/Backend/OpenHarness.Domain`: Entidades con `Guid.CreateVersion7()`, ValueObjects y Enums.
- `src/Backend/OpenHarness.Application`: Abstracciones y Vertical Slices (`Auth`, `Sessions`, `Approvals`, `Workspaces`).
- `src/Backend/OpenHarness.Infrastructure`: EF Core 10 `OpenHarnessDbContext`, `Npgsql`, `JwtTokenProvider`, `PiRpcSessionBridge`.
- `src/Backend/OpenHarness.Api`: Minimal APIs .NET 10, `TypedResults.ServerSentEvents`.
- `src/Client/OpenHarness.Client.Core`: SDK HTTP, `JwtAuthenticationHandler` (`SemaphoreSlim`), `HarnessSseClient`.
- `src/Client/OpenHarness.UI.Avalonia`: Vistas AXAML, `ArmoniaDesignTokens.axaml`, ViewModels MVVM, `PlatformSecureTokenStorage`.
- `tests/OpenHarness.UnitTests` y `tests/OpenHarness.IntegrationTests`.

---

## 5. Diseño de Base de Datos (PostgreSQL 17 + EF Core 10)

### 5.1. Entidades Principales (`Schema: harness`)
1. **`User` (`users`)**: `Id` (`Guid.CreateVersion7()`), `Email` (único), `DisplayName`, `PasswordHash`, `Role`, `IsActive`, timestamps de auditoría.
2. **`RefreshToken` (`refresh_tokens`)**: `Id` (`Guid.CreateVersion7()`), `UserId`, `TokenHash` (`SHA-256` hex de 64 caracteres), `FamilyId` (`Guid.CreateVersion7()`), `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByTokenHash`.
3. **`WorkspacePolicy` (`workspace_policies`)**: `Id` (`Guid.CreateVersion7()`), `OwnerId`, `Name`, `RootPath`, `SandboxMode`, `ApprovalPolicy`, y `RulesConfig` mapeado con `.ToJson("rules_config")`.
4. **`AgentSession` (`agent_sessions`)**: `Id` (`Guid.CreateVersion7()`), `UserId`, `WorkspacePolicyId`, `ParentSessionId`, `Title`, `Status`, `ModelConfig` (`.ToJson("model_config")`), `TotalInputTokens`, `TotalOutputTokens`, y `RowVersion` (`xmin` para concurrencia optimista).
5. **`ConversationTurn` (`conversation_turns`)**: `Id` (`Guid.CreateVersion7()`), `SessionId`, `SequenceNumber` (índice único compuesto con `SessionId`), `Role`, `Content`, `ThinkingContent`, `TokenUsage` (`.ToJson("token_usage")`), `Embedding` (`vector(1536)` con índice `hnsw`).
6. **`ToolExecution` (`tool_executions`)**: `Id` (`Guid.CreateVersion7()`), `SessionId`, `TurnId`, `CallId`, `ToolName`, `Arguments` (`jsonb` con índice GIN `jsonb_path_ops`), `Result` (`jsonb` con índice GIN `jsonb_path_ops`), `Status`, `RequiresApproval`, `ApprovedByUserId`, `DurationMs`, `ExitCode`.

### 5.2. Características de EF Core 10 Aplicadas
- `NpgsqlDataSourceBuilder.EnableDynamicJson()` + `UseVector()` + `AddDbContextPool<OpenHarnessDbContext>` con `EnableRetryOnFailure(5)` y `UseSnakeCaseNamingConvention()`.
- `AuditableEntityInterceptor` (`SaveChangesInterceptor`) inyectando `TimeProvider.System` de .NET 10.
- `EF.CompileAsyncQuery` en las consultas críticas de validación de propiedad de sesión y lectura de historial.

---

## 6. Hoja de Ruta por Fases

- **Fase 1**: Actualizar `PLAN_IMPLEMENTACION_HARNESS.md` con la arquitectura de `Pi (--mode rpc)`, generar artefactos SDD en `.speckit/`, crear `OpenHarness.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `docker-compose.yml`, `OpenHarness.Domain` y `OpenHarness.Infrastructure` (con `OpenHarnessDbContext` y la migración inicial `InitialHarnessSchema` de EF Core 10).
- **Fase 2**: Seguridad JWT en .NET 10 (`JwtTokenProvider`, endpoints `/api/v1/auth/*`, rotación por `FamilyId`).
- **Fase 3**: Motor del Harness con `PiRpcSessionBridge` (`pi --mode rpc --no-session`), `IApprovalGate` y streaming SSE nativo en `.NET 10`.
- **Fase 4**: SDK Cliente (`OpenHarness.Client.Core`: `JwtAuthenticationHandler` con `SemaphoreSlim` y `HarnessSseClient`).
- **Fase 5**: Interfaz gráfica `OpenHarness.UI.Avalonia` (`avalonia.mvvm`, layout de 3 paneles, `AvaloniaEdit` para diffs y `SandboxApprovalDialog`).
