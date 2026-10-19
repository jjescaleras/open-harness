# Plan Técnico de Implementación: Open Harness Core (Feature 001)

## 1. Resumen y Arquitectura General

El propósito de este plan técnico es definir la arquitectura detallada, el modelo de datos, los protocolos de comunicación y la secuencia de entrega para **Open Harness Core**, cubriendo las cinco fases del proyecto según lo estipulado en `PLAN_IMPLEMENTACION_HARNESS.md` y `constitution.md`.

### 1.1. Arquitectura de Componentes
```
+-----------------------------------------------------------------------------------+
|                           OpenHarness.UI.Avalonia                                 |
|  - ArmoniaDesignTokens.axaml  - ChatStreamView (60 FPS PeriodicTimer)             |
|  - AvaloniaEdit (Diffs/Code)  - SandboxApprovalDialog (Shortcuts)                 |
+-----------------------------------------------------------------------------------+
                                       |
                                       v
+-----------------------------------------------------------------------------------+
|                            OpenHarness.Client.Core                                |
|  - JwtAuthenticationHandler (SemaphoreSlim)  - PlatformSecureTokenStorage (Keyring)|
|  - HarnessSseClient (System.Net.ServerSentEvents.SseParser)                       |
+-----------------------------------------------------------------------------------+
                                       | HTTP / SSE (Bearer JWT)
                                       v
+-----------------------------------------------------------------------------------+
|                              OpenHarness.Api                                      |
|  - Minimal APIs (.NET 10)     - TypedResults.ServerSentEvents                     |
|  - JsonWebTokenHandler Auth   - MapInboundClaims = false                          |
+-----------------------------------------------------------------------------------+
        |                                                   |
        v                                                   v
+-------------------------------+   +-----------------------------------------------+
|    OpenHarness.Application    |   |           OpenHarness.Infrastructure          |
| - Vertical Slices (Auth,      |   | - OpenHarnessDbContext (EF Core 10 / Npgsql)  |
|   Sessions, Approvals)        |   | - PiRpcSessionBridge (pi --mode rpc)          |
| - IApprovalGate (TCS)         |   | - JwtTokenProvider (HMAC-SHA256)              |
+-------------------------------+   +-----------------------------------------------+
        |                                                   |
        +-----------------------+---------------------------+
                                |
                                v
+-----------------------------------------------------------------------------------+
|                             OpenHarness.Domain                                    |
| - Entidades con Guid.CreateVersion7() (User, RefreshToken, WorkspacePolicy,       |
|   AgentSession, ConversationTurn, ToolExecution)                                  |
+-----------------------------------------------------------------------------------+
                                |
                                v
+-----------------------------------------------------------------------------------+
|                PostgreSQL 17 (pgvector/pgvector:pg17)                             |
| - Esquema: harness  - jsonb (GIN)  - vector(1536) (HNSW)  - Concurrencia (xmin)   |
+-----------------------------------------------------------------------------------+
```

---

## 2. Modelo de Datos en el Esquema `harness` (PostgreSQL 17 + EF Core 10)

Todas las entidades heredan de `AuditableEntity` (`CreatedAtUtc`, `UpdatedAtUtc`) y generan su clave primaria mediante **`Guid.CreateVersion7()`** en C#.

```
+-----------------------+       +-------------------------+
|     harness.users     |       | harness.refresh_tokens  |
+-----------------------+       +-------------------------+
| id: uuid (v7, PK)     |<-----\| id: uuid (v7, PK)       |
| email: varchar (UQ)   |      || user_id: uuid (FK)      |
| display_name: varchar |      || token_hash: char(64) UQ |
| password_hash: text   |      || family_id: uuid (v7, IX)|
| role: varchar         |      || expires_at_utc: timestamptz
| is_active: bool       |      || revoked_at_utc: timestamptz
| created_at_utc        |      || replaced_by_hash: text  |
| updated_at_utc        |      || revocation_reason: text |
+-----------------------+      |+-------------------------+
           |
           v
+-----------------------------+       +-----------------------------+
| harness.workspace_policies  |       |    harness.agent_sessions   |
+-----------------------------+       +-----------------------------+
| id: uuid (v7, PK)           |<-----\| id: uuid (v7, PK)           |
| owner_id: uuid (FK)         |      || user_id: uuid (FK)          |
| name: varchar               |      || workspace_policy_id: uuid   |
| root_path: text             |      || parent_session_id: uuid? FK |
| sandbox_mode: varchar       |      || title: varchar              |
| approval_policy: varchar    |      || status: varchar             |
| rules_config: jsonb (.ToJson)      || model_config: jsonb (.ToJson)
+-----------------------------+      || total_input_tokens: bigint  |
                                     || total_output_tokens: bigint |
                                     || xmin: uint (RowVersion)     |
                                     |+-----------------------------+
                                                    |
                      +-----------------------------+-----------------------------+
                      |                                                           |
                      v                                                           v
+-------------------------------------------+               +-------------------------------------------+
|        harness.conversation_turns         |               |          harness.tool_executions         |
+-------------------------------------------+               +-------------------------------------------+
| id: uuid (v7, PK)                         |               | id: uuid (v7, PK)                         |
| session_id: uuid (FK)                     |               | session_id: uuid (FK)                     |
| sequence_number: int (UQ con session_id)  |               | turn_id: uuid (FK)                        |
| role: varchar                             |               | call_id: varchar                          |
| content: text                             |               | tool_name: varchar                        |
| thinking_content: text                    |               | arguments: jsonb (GIN jsonb_path_ops)     |
| token_usage: jsonb (.ToJson)              |               | result: jsonb (GIN jsonb_path_ops)        |
| embedding: vector(1536) (HNSW Cosine)     |               | status: varchar                           |
+-------------------------------------------+               | requires_approval: bool                   |
                                                            | approved_by_user_id: uuid? (FK)           |
                                                            | duration_ms: bigint                       |
                                                            +-------------------------------------------+
```

### 2.1. Detalles de Mapeo en EF Core 10
1. **Convención**: `optionsBuilder.UseSnakeCaseNamingConvention()`.
2. **Esquema**: `modelBuilder.HasDefaultSchema("harness")`.
3. **Columnas Nativas `jsonb`**:
   - `ToolExecution.Arguments`: `.HasColumnType("jsonb")` con índice `.HasMethod("gin").HasOperators("jsonb_path_ops")`.
   - `ToolExecution.Result`: `.HasColumnType("jsonb")` con índice `.HasMethod("gin").HasOperators("jsonb_path_ops")`.
4. **Mapeo Tipado `.ToJson()`**:
   - `WorkspacePolicy.RulesConfig` (`WorkspaceRulesConfig`).
   - `AgentSession.ModelConfig` (`AgentModelConfig`).
   - `ConversationTurn.TokenUsage` (`TurnTokenUsage`).
5. **Vectores HNSW**:
   - `builder.Entity<ConversationTurn>().HasIndex(t => t.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops")`.
6. **Concurrencia Optimista**:
   - `builder.Entity<AgentSession>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`.

---

## 3. Protocolo JSONL de `PiRpcSessionBridge`

`PiRpcSessionBridge` interactúa con el subproceso `pi --mode rpc --no-session` utilizando flujos bidireccionales de objetos JSON delimitados por salto de línea (`\n`).

### 3.1. Mensajes Enviados al Subproceso (`stdin`)
1. **Inicio de Turno**:
   ```json
   {"type": "prompt", "message": "¿Puedes listar los archivos del directorio src?"}
   ```
2. **Respuesta a Solicitud de Aprobación UI (`extension_ui_response`)**:
   ```json
   {"type": "extension_ui_response", "id": "req-123", "confirmed": true}
   ```
3. **Cancelación o Interrupción**:
   ```json
   {"type": "abort"}
   ```

### 3.2. Eventos Emitidos por el Subproceso (`stdout`)
1. **Delta de Texto Generado**:
   ```json
   {"type": "message_update", "delta": "Analizando la estructura del proyecto..."}
   ```
2. **Delta de Razonamiento Interno**:
   ```json
   {"type": "thinking_update", "delta": "Debo invocar la herramienta bash para ejecutar ls -la"}
   ```
3. **Inicio de Ejecución de Herramienta**:
   ```json
   {"type": "tool_execution_start", "call_id": "call-001", "tool": "bash", "arguments": {"command": "ls -la src"}}
   ```
4. **Solicitud de Confirmación Interactiva (Extension UI Request)**:
   ```json
   {"type": "extension_ui_request", "id": "req-123", "method": "confirm", "title": "Ejecutar comando bash", "message": "ls -la src"}
   ```
5. **Fin de Ejecución de Herramienta**:
   ```json
   {"type": "tool_execution_end", "call_id": "call-001", "result": {"output": "...", "exit_code": 0}}
   ```
6. **Fin de Turno del Agente**:
   ```json
   {"type": "agent_end", "usage": {"input_tokens": 420, "output_tokens": 150}}
   ```

---

## 4. Plan de Implementación por Fases

### Fase 1: Dominio, EF Core 10 y Persistencia PostgreSQL 17
- **Entregables**:
  1. `OpenHarness.slnx`, `Directory.Build.props`, `Directory.Packages.props`.
  2. `docker-compose.yml` para `pgvector/pgvector:pg17` con script de inicialización (`CREATE EXTENSION IF NOT EXISTS vector;`, `uuid-ossp;`, `pg_trgm;`).
  3. `OpenHarness.Domain`: Entidades, Value Objects, Enums y generador `Guid.CreateVersion7()`.
  4. `OpenHarness.Infrastructure`: `OpenHarnessDbContext`, interceptores de auditoría (`TimeProvider`), configuraciones de entidad (`EntityTypeConfiguration`), migración inicial `InitialHarnessSchema`.
- **Criterio de Verificación**: `dotnet build` exitoso sin advertencias; migración ejecutada limpiamente en contenedor de Postgres.

### Fase 2: Seguridad y Autenticación JWT Zero-Allocation
- **Entregables**:
  1. `OpenHarness.Application`: Slices de autenticación (`LoginCommand`, `RegisterCommand`, `RefreshTokenCommand`, `RevokeTokenCommand`).
  2. `OpenHarness.Infrastructure`: `JwtTokenProvider` implementado con `JsonWebTokenHandler`, generador criptográfico de Refresh Tokens y hashing SHA-256.
  3. `OpenHarness.Api`: Configuración de `AddJwtBearer` con `MapInboundClaims = false`, endpoints de autenticación en Minimal APIs.
  4. Lógica de invalidación atómica de `FamilyId` mediante `ExecuteUpdateAsync` ante reutilización indebida de tokens revocados.
- **Criterio de Verificación**: Pruebas de integración con `Testcontainers.PostgreSql` verificando emisión de tokens en < 1 ms y revocación atómica en < 30 ms.

### Fase 3: Motor Pi RPC, Streaming SSE y Aprobaciones
- **Entregables**:
  1. `PiRpcSessionBridge`: Gestión del proceso `pi --mode rpc --no-session` con pipes asíncronos y deserialización JSONL.
  2. `IApprovalGate` e implementación `InMemoryApprovalGate`: `ConcurrentDictionary` con `TaskCompletionSource<ApprovalDecision>` y temporizadores de expiración.
  3. `OpenHarness.Api`: Endpoints `/api/v1/sessions/{id}/prompt`, `/api/v1/sessions/{id}/stream` (`TypedResults.ServerSentEvents`) y `/api/v1/sessions/{id}/approvals`.
  4. Mantenimiento de la abstracción `IChatClient` (`Microsoft.Extensions.AI`).
- **Criterio de Verificación**: Flujo de prompt y streaming SSE completo transmitiendo eventos en vivo y pausando ante solicitudes de aprobación.

### Fase 4: SDK Cliente Core
- **Entregables**:
  1. `OpenHarness.Client.Core`: Clientes HTTP fuertemente tipados.
  2. `JwtAuthenticationHandler`: `DelegatingHandler` con `SemaphoreSlim(1, 1)` para refresco transparente ante 401.
  3. `PlatformSecureTokenStorage`: Implementación multiplataforma con soporte para `libsecret` en Linux.
  4. `HarnessSseClient`: Consumo continuo con `System.Net.ServerSentEvents.SseParser` y manejo de reconexión.
- **Criterio de Verificación**: Pruebas de cliente que simulen peticiones concurrentes bajo token vencido, verificando un único llamado de refresco.

### Fase 5: Interfaz de Usuario Avalonia con ArmoniaDesignTokens
- **Entregables**:
  1. `OpenHarness.UI.Avalonia`: Proyecto configurado con MVVM (`CommunityToolkit.Mvvm`).
  2. `ArmoniaDesignTokens.axaml`: Definición de recursos de color, espaciado y tipografía.
  3. Shell de 3 paneles: `SessionListView`, `ChatStreamView` y `ToolInspectorView`.
  4. Búfer de streaming desacoplado gobernado por `PeriodicTimer` a 16 ms (60 FPS).
  5. Integración de `Avalonia.AvaloniaEdit` para diffs de código con sintaxis coloreada.
  6. `SandboxApprovalDialog` con control por atajos de teclado (`Enter`, `Ctrl+Enter`, `Esc`).
- **Criterio de Verificación**: Ejecución visual fluida de sesión agéntica sin bloqueos de interfaz ni pérdidas de cuadros por segundo.

---

## 5. Hitos, Dependencias y Mitigación de Riesgos

| Hito | Duración Estimada | Dependencias Críticas | Riesgo Principal | Mitigación |
|---|---|---|---|---|
| **H1: Persistencia & Dominio** | Días 1–2 | Docker, .NET 10 SDK, PostgreSQL 17 | Compatibilidad de tipos vectoriales en EF Core 10 | Uso de `Pgvector.EntityFrameworkCore 0.3.0` y pruebas con `Testcontainers`. |
| **H2: Seguridad JWT** | Días 3–4 | H1 finalizado | Condiciones de carrera en refresco concurrente | Uso de `FamilyId` indexado y `ExecuteUpdateAsync` atómico en Postgres. |
| **H3: Motor Pi RPC & SSE** | Días 5–7 | H1, H2 | Caída o cuelgue del subproceso `pi` | Supervisión del ciclo de vida del proceso en `PiRpcSessionBridge` con reinicio y cancelación limpia en `IApprovalGate`. |
| **H4: Client SDK** | Días 8–9 | H2, H3 | Incompatibilidad de llaveros seguros en Linux/Wayland | Abstracción `PlatformSecureTokenStorage` con fallback en memoria si el Secret Service no está disponible. |
| **H5: Avalonia UI** | Días 10–12 | H4 | Saturación del `Dispatcher.UIThread` por ráfagas SSE | Búfer desacoplado en memoria con volcado periódico a 16 ms (60 FPS). |
