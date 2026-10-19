# Tareas de Implementación: Open Harness Core (Feature 001)

Este documento contiene la lista detallada y estructurada de tareas accionables por fase para la implementación de **Open Harness Core**. Cada tarea cuenta con identificador único, dependencias, archivos objetivo, referencias de trazabilidad a `specify.md` (`FR-XXX`, `SC-XXX`, `EC-XXX`) y criterios de aceptación verificables.

---

## Fase 1: Dominio, EF Core 10 y Persistencia PostgreSQL 17

- [x] **T1.1: Configuración de la Solución .NET 10 y Gestión Centralizada de Paquetes**
  - **Dependencias**: Ninguna
  - **Archivos Objetivo**: `OpenHarness.slnx`, `Directory.Build.props`, `Directory.Packages.props`
  - **Trazabilidad**: `FR-001`, `SC-001`
  - **Criterio de Aceptación**: `dotnet build` compila la solución en `.slnx` sobre `net10.0` y C# 14 con `<Nullable>enable</Nullable>` sin emitir errores de versiones dispersas.

- [x] **T1.2: Infraestructura de Base de Datos Contenerizada con pgvector**
  - **Dependencias**: `T1.1`
  - **Archivos Objetivo**: `docker-compose.yml`, `docker/postgres/init-extensions.sql`
  - **Trazabilidad**: `FR-001`, `SC-001`
  - **Criterio de Aceptación**: `docker compose up -d` inicializa un contenedor `pgvector/pgvector:pg17` con las extensiones `vector`, `uuid-ossp` y `pg_trgm` disponibles en menos de 5 segundos.

- [x] **T1.3: Modelo de Dominio con Identificadores Guid v7**
  - **Dependencias**: `T1.1`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Domain/Entities/*.cs`, `src/Backend/OpenHarness.Domain/Common/IAuditableEntity.cs`
  - **Trazabilidad**: `FR-002`
  - **Criterio de Aceptación**: Entidades `User`, `RefreshToken`, `WorkspacePolicy`, `AgentSession`, `ConversationTurn` y `ToolExecution` definidas con `Guid.CreateVersion7()` como clave primaria en sus constructores.

- [x] **T1.4: Contexto EF Core 10, Npgsql y Mapeos Avanzados**
  - **Dependencias**: `T1.2`, `T1.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Infrastructure/Persistence/OpenHarnessDbContext.cs`, `src/Backend/OpenHarness.Infrastructure/Persistence/Configurations/*.cs`
  - **Trazabilidad**: `FR-003`, `EC-003`, `SC-001`
  - **Criterio de Aceptación**: `OpenHarnessDbContext` configurado bajo el esquema `harness` con `UseSnakeCaseNamingConvention()`, mapeos `.ToJson()` en configuraciones, índices GIN `jsonb_path_ops` en `ToolExecution`, índice HNSW en `ConversationTurn.Embedding` y concurrencia optimista con `xmin` en `AgentSession`.

- [x] **T1.5: Migración Inicial y Pruebas de Integración de Base de Datos**
  - **Dependencias**: `T1.4`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Infrastructure/Persistence/Migrations/*`
  - **Trazabilidad**: `FR-001`, `FR-003`, `SC-001`, `EC-003`
  - **Criterio de Aceptación**: Migración `InitialHarnessSchema` generada en EF Core 10 validando `Guid v7`, persistencia de `jsonb` (`rules_config`, `model_config`, `token_usage`, `arguments`, `result`), índices `hnsw`/`gin` y concurrencia optimista `xmin`.

---

## Fase 2: Seguridad y Autenticación JWT Zero-Allocation

- [x] **T2.1: Proveedor Criptográfico y Generador de Tokens JWT**
  - **Dependencias**: `T1.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Infrastructure/Security/JwtTokenProvider.cs`, `src/Backend/OpenHarness.Application/Common/Interfaces/IJwtTokenProvider.cs`
  - **Trazabilidad**: `FR-004`, `SC-002`
  - **Criterio de Aceptación**: Emisión de Access Token JWT (10 min) utilizando `JsonWebTokenHandler` sin asignaciones superfluas en menos de 0.5 ms, y generación de Refresh Tokens criptográficos de 64 bytes (`RandomNumberGenerator.Fill`).

- [x] **T2.2: Casos de Uso de Autenticación y Rotación por FamilyId**
  - **Dependencias**: `T1.4`, `T2.1`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Application/Features/Auth/*.cs`
  - **Trazabilidad**: `FR-004`, `FR-005`, `SC-003`, `EC-001`
  - **Criterio de Aceptación**: Implementación de comandos `Register`, `Login`, `RefreshToken` y `RevokeToken`. La rotación actualiza el token en base de datos almacenando su hash SHA-256 y manteniendo el `FamilyId`.

- [x] **T2.3: Detección Automática de Robo y Revocación Atómica**
  - **Dependencias**: `T2.2`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Application/Features/Auth/RefreshTokenCommand.cs`
  - **Trazabilidad**: `FR-005`, `SC-003`, `EC-001`
  - **Criterio de Aceptación**: Si un Refresh Token revocado es presentado, `ExecuteUpdateAsync` invalida en menos de 30 ms todos los tokens con el mismo `FamilyId`, denegando la autenticación con error `SessionCompromised`.

- [x] **T2.4: Endpoints de Autenticación Minimal API y Middleware JWT**
  - **Dependencias**: `T2.2`, `T2.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Api/Endpoints/AuthEndpoints.cs`, `src/Backend/OpenHarness.Api/Program.cs`
  - **Trazabilidad**: `FR-004`, `SC-002`
  - **Criterio de Aceptación**: Endpoints `/api/v1/auth/*` registrados y protegidos con `AddJwtBearer` configurado con `MapInboundClaims = false`.

---

## Fase 3: Motor del Agente Pi RPC, Streaming SSE y Aprobaciones

- [x] **T3.1: Puente de Comunicación Pi RPC (`PiRpcSessionBridge`)**
  - **Dependencias**: `T1.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Infrastructure/Agents/Pi/PiRpcSessionBridge.cs`
  - **Trazabilidad**: `FR-006`, `SC-004`, `EC-002`, `EC-006`
  - **Criterio de Aceptación**: Proceso `pi --mode rpc --no-session` ejecutado con redirección asíncrona de `StandardInput`, `StandardOutput` y `StandardError`, manejando flujo JSONL, líneas malformadas y reinicio limpio ante caída abrupta.

- [x] **T3.2: Abstracción Multi-Proveedor IChatClient**
  - **Dependencias**: `T3.1`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Infrastructure/Agents/Adapters/PiChatClientAdapter.cs`
  - **Trazabilidad**: `FR-007`
  - **Criterio de Aceptación**: `IChatClient` de `Microsoft.Extensions.AI` implementado sobre el puente de Pi, garantizando desacoplamiento del resto de la aplicación.

- [x] **T3.3: Compuerta de Aprobación Asíncrona (`IApprovalGate`)**
  - **Dependencias**: `T1.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Application/Common/Interfaces/IApprovalGate.cs`, `src/Backend/OpenHarness.Infrastructure/Security/InMemoryApprovalGate.cs`
  - **Trazabilidad**: `FR-008`, `SC-005`, `EC-005`
  - **Criterio de Aceptación**: `IApprovalGate` gestiona `TaskCompletionSource<ApprovalDecision>` con timeout automático (5 minutos), resolviendo decisiones humanas y retornando respuesta `extension_ui_response` a Pi sin bloquear hilos.

- [x] **T3.4: Endpoints de Sesión y Streaming SSE Nativo**
  - **Dependencias**: `T3.1`, `T3.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Api/Endpoints/SessionEndpoints.cs`
  - **Trazabilidad**: `FR-008`, `SC-005`
  - **Criterio de Aceptación**: Endpoints `POST /api/v1/sessions/{id}/prompt`, `GET /api/v1/sessions/{id}/stream` (usando `TypedResults.ServerSentEvents`) y `POST /api/v1/sessions/{id}/approvals` transmitiendo eventos en vivo hacia el cliente.

---

## Fase 4: SDK Cliente Core

- [x] **T4.1: Clientes HTTP Fuertemente Tipados y Modelos de Contrato**
  - **Dependencias**: `T2.4`, `T3.4`
  - **Archivos Objetivo**: `src/Client/OpenHarness.Client.Core/Http/*.cs`, `src/Client/OpenHarness.Client.Core/Models/*.cs`
  - **Trazabilidad**: `FR-009`
  - **Criterio de Aceptación**: Interfaces y clases `IAuthApiClient`, `ISessionsApiClient` y `IApprovalsApiClient` implementadas para consumo de la API de Open Harness.

- [x] **T4.2: DelegatingHandler de Autenticación con SemaphoreSlim**
  - **Dependencias**: `T4.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.Client.Core/Http/JwtAuthenticationHandler.cs`
  - **Trazabilidad**: `FR-009`, `EC-004`
  - **Criterio de Aceptación**: Ante respuesta 401, el handler bloquea con `SemaphoreSlim(1, 1)`, refresca el token una única vez y reintenta todas las peticiones concurrentes encoladas de manera transparente.

- [x] **T4.3: Almacenamiento Seguro Multiplataforma**
  - **Dependencias**: `T4.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.Client.Core/Storage/PlatformSecureTokenStorage.cs`
  - **Trazabilidad**: `FR-009`
  - **Criterio de Aceptación**: Almacenamiento y recuperación cifrada del Refresh Token utilizando `libsecret` en Linux, `DPAPI` en Windows y `Keychain` en macOS.

- [x] **T4.4: Cliente de Streaming SSE Reactivo**
  - **Dependencias**: `T4.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.Client.Core/Streaming/HarnessSseClient.cs`
  - **Trazabilidad**: `FR-009`
  - **Criterio de Aceptación**: Deserialización reactiva de Server-Sent Events con `System.Net.ServerSentEvents.SseParser`, exponiendo eventos fuertemente tipados con reconexión automática ante cortes de red.

---

## Fase 5: Interfaz de Usuario Avalonia con ArmoniaDesignTokens

- [x] **T5.1: Estilos Globales y ArmoniaDesignTokens**
  - **Dependencias**: `T4.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Assets/Styles/ArmoniaDesignTokens.axaml`
  - **Trazabilidad**: `FR-010`
  - **Criterio de Aceptación**: Paleta de colores oscuros, tipografía corporativa, espaciados y recursos visuales definidos de forma centralizada en Avalonia XAML.

- [x] **T5.2: Shell Principal y Navegación de Sesiones**
  - **Dependencias**: `T5.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Views/HarnessShellView.axaml`, `src/Client/OpenHarness.UI.Avalonia/ViewModels/HarnessShellViewModel.cs`
  - **Trazabilidad**: `FR-010`
  - **Criterio de Aceptación**: Ventana principal con layout de 3 paneles (lista de sesiones, área conversacional y panel de telemetría/herramientas) enlazado mediante CommunityToolkit.Mvvm.

- [x] **T5.3: Vista de Conversación y Búfer de Streaming a 60 FPS**
  - **Dependencias**: `T4.4`, `T5.2`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Views/ChatStreamView.axaml`, `src/Client/OpenHarness.UI.Avalonia/ViewModels/ChatStreamViewModel.cs`
  - **Trazabilidad**: `FR-010`, `SC-006`
  - **Criterio de Aceptación**: Componente de chat que acumula deltas SSE y actualiza la vista a 60 FPS mediante `PeriodicTimer` (16 ms) sin congelar el hilo `Dispatcher.UIThread`.

- [x] **T5.4: Integración de AvaloniaEdit para Visualización de Diffs**
  - **Dependencias**: `T5.3`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Controls/DiffEditorControl.axaml`
  - **Trazabilidad**: `FR-010`, `US-004`
  - **Criterio de Aceptación**: Bloques de código y propuestas de diffs renderizados con `Avalonia.AvaloniaEdit` y resaltado de sintaxis coloreado.

- [x] **T5.5: Diálogo Modal de Aprobación de Sandbox**
  - **Dependencias**: `T4.1`, `T5.2`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Views/SandboxApprovalDialog.axaml`, `src/Client/OpenHarness.UI.Avalonia/ViewModels/SandboxApprovalViewModel.cs`
  - **Trazabilidad**: `FR-010`, `SC-005`, `EC-005`
  - **Criterio de Aceptación**: Diálogo modal interactivo con atajos de teclado (`Enter`, `Ctrl+Enter`, `Esc`) y temporizador regresivo de expiración conectado a `/api/v1/sessions/{id}/approvals`.
