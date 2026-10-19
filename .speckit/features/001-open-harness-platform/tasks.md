# Desglose de Tareas: Plataforma Open Harness (Feature 001)

Este documento detalla el plan de trabajo de ingeniería por fases, con trazabilidad exhaustiva hacia los Requisitos Funcionales (`FR-xxx`), Criterios de Éxito (`SC-xxx`) y Casos Límite (`EC-xxx`).

---

## Phase 1: Infraestructura Base y Persistencia PostgreSQL 17
- [x] Configurar solución en formato XML moderno `OpenHarness.slnx`, `Directory.Build.props` (TargetFramework `net10.0`, LangVersion `14.0`, Nullable `enable`) y `Directory.Packages.props` con Central Package Management (`CPM`) [FR-001] [SC-001]
- [x] Crear manifiesto `docker-compose.yml` para despliegue de PostgreSQL 17 con extensión vectorial (`pgvector/pgvector:pg17`) y configuración de volumen persistente [FR-001] [SC-001]
- [x] Definir modelos de dominio en `OpenHarness.Domain` con claves primarias secuenciales `Guid.CreateVersion7()` para entidades `User`, `RefreshToken`, `WorkspacePolicy`, `AgentSession`, `ConversationTurn` y `ToolExecution` [FR-002] [SC-001]
- [x] Implementar `OpenHarnessDbContext` en EF Core 10 con mapeo nativo `jsonb` e índices GIN `jsonb_path_ops`, mapeos `.ToJson()`, índice vectorial `hnsw` e intercepción de concurrencia optimista con `xmin` [FR-003] [SC-001] [EC-006]
- [x] Generar y aplicar migración inicial de base de datos EF Core 10 `InitialHarnessSchema` [FR-001] [FR-003] [SC-001]
- [x] Inicializar artefactos de Spec-Driven Development (`.speckit`) y Constitución Técnica con estándares de arquitectura y seguridad [FR-001] [SC-001]

---

## Phase 2: Autenticación JWT Zero-Allocation y Rotación por FamilyId
- [ ] Implementar Vertical Slices de autenticación en `OpenHarness.Application/Features/Auth` (`RegisterCommand`, `LoginCommand`, `RefreshTokenCommand`, `RevokeTokenCommand`) [FR-004] [SC-002]
- [ ] Implementar `JwtTokenProvider` utilizando `JsonWebTokenHandler` de alto rendimiento para emisión de Access Tokens (15 min) y configuración `MapInboundClaims = false` [FR-005] [SC-002]
- [ ] Implementar mecanismo de rotación de Refresh Tokens criptográficos de 64 bytes con almacenamiento de hash `SHA-256` y revocación en cascada de toda la familia `FamilyId` ante intentos de reuso [FR-006] [SC-003] [EC-002]
- [ ] Configurar middleware de autenticación `AddJwtBearer` y políticas de autorización (`SessionOwner`) en Minimal APIs de `OpenHarness.Api` [FR-004] [FR-005] [SC-002]
- [ ] Desarrollar suite de pruebas de integración con `Testcontainers.PostgreSql` validando rotación de tokens, concurrencia y detección de anomalías [FR-006] [SC-003] [SC-008] [EC-002]

---

## Phase 3: Motor del Agente Pi RPC y Abstracción IChatClient
- [ ] Implementar `PiRpcSessionBridge` en `OpenHarness.Infrastructure.Agents.Pi` administrando el subproceso `pi --mode rpc --no-session` sobre `stdin`/`stdout` JSONL [FR-007] [SC-004] [EC-001] [EC-005]
- [ ] Implementar abstracción unificada `IChatClient` (`Microsoft.Extensions.AI`) para soporte multi-proveedor LLM y agentes auxiliares [FR-008] [SC-004]
- [ ] Implementar `IApprovalGate` con `TaskCompletionSource<bool>` para pausa y resolución asíncrona no bloqueante de permisos de sandbox [FR-009] [SC-005] [EC-003]
- [ ] Configurar endpoint SSE nativo en ASP.NET Core 10 (`TypedResults.ServerSentEvents`) transmitiendo deltas de texto, pensamiento y ciclo de herramientas [FR-010] [SC-006] [EC-004]
- [ ] Implementar endpoint de resolución `POST /api/v1/sessions/{id}/approvals` que canaliza la decisión del usuario como `extension_ui_response` hacia `pi` [FR-011] [SC-005] [EC-003]
- [ ] Persistir auditoría completa de llamadas de herramientas `ToolExecution` en PostgreSQL 17 con argumentos y resultados en `jsonb` bajo control `xmin` [FR-003] [FR-007] [SC-001] [EC-006]

---

## Phase 4: SDK Cliente Core y Autenticación Transparente
- [ ] Crear proyecto `OpenHarness.Client.Core` con interfaces tipadas de API (`IAuthApiClient`, `ISessionsApiClient`, `IApprovalsApiClient`) [FR-012] [SC-007]
- [ ] Implementar `JwtAuthenticationHandler` (`DelegatingHandler`) con `SemaphoreSlim(1, 1)` para refresco concurrente seguro y reintento automático de peticiones con 401 [FR-012] [SC-007]
- [ ] Implementar `PlatformSecureTokenStorage` con proveedores nativos de almacenamiento seguro por sistema operativo (`libsecret` en Linux, `DPAPI` en Windows, `Keychain` en macOS) [FR-013] [SC-007]
- [ ] Implementar `HarnessSseClient` basado en `System.Net.ServerSentEvents.SseParser` con reconexión automática y retroceso exponencial ante caídas de red [FR-010] [FR-012] [SC-006] [EC-004]

---

## Phase 5: Frontend UI Avalonia con Armonia Design Tokens
- [ ] Configurar arquitectura MVVM en `OpenHarness.UI.Avalonia` con `CommunityToolkit.Mvvm 8.4` y paleta de diseño `ArmoniaDesignTokens` [FR-014] [SC-006]
- [ ] Diseñar `HarnessShellView` con disposición ergonómica de 3 paneles (árbol de sesiones históricas, área de chat/streaming, panel de herramientas y telemetría) [FR-014] [SC-006]
- [ ] Desarrollar `ChatStreamViewModel` y vista con acumulador de deltas en búfer volcado a 60 FPS mediante `PeriodicTimer` (16 ms) [FR-015] [SC-006]
- [ ] Integrar editor `Avalonia.AvaloniaEdit` con resaltado de sintaxis `TextMateSharp` para visualización clara y coloreada de diffs de código [FR-015] [SC-006]
- [ ] Desarrollar diálogo modal accesible `SandboxApprovalDialog` con soporte de atajos de teclado (`Enter`, `Ctrl+Enter`, `Esc`) y temporizador de expiración [FR-015] [SC-005] [EC-003]
- [ ] Ejecutar suite completa de pruebas de extremo a extremo cubriendo todos los requisitos y criterios de éxito [FR-001] [FR-002] [FR-003] [FR-004] [FR-005] [FR-006] [FR-007] [FR-008] [FR-009] [FR-010] [FR-011] [FR-012] [FR-013] [FR-014] [FR-015] [SC-001] [SC-002] [SC-003] [SC-004] [SC-005] [SC-006] [SC-007] [SC-008] [EC-001] [EC-002] [EC-003] [EC-004] [EC-005] [EC-006]
