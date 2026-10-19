# Lista de Verificación de Calidad (Checklist): Plataforma Open Harness

Esta lista de verificación permite auditar y certificar que la arquitectura, la seguridad, la persistencia, la integración del motor Pi RPC y la interfaz de usuario cumplen con los más altos estándares definidos en la Constitución Técnica y la especificación de Open Harness.

---

## 1. Arquitectura y Estándares .NET 10 / C# 14
- [ ] La solución utiliza el formato XML moderno `OpenHarness.slnx`.
- [ ] Todas las dependencias de paquetes NuGet se gestionan centralmente en `Directory.Packages.props` (CPM).
- [ ] Los proyectos configuran `net10.0`, C# `14.0`, `<Nullable>enable</Nullable>` y `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- [ ] El núcleo de dominio (`OpenHarness.Domain`) carece de dependencias externas hacia frameworks web o librerías de infraestructura.
- [ ] Las funcionalidades de aplicación (`OpenHarness.Application`) están modeladas bajo el patrón Vertical Slices (Auth, Sessions, Approvals, Workspaces).
- [ ] No existen archivos de código `.cs` ni `.csproj` modificados de forma imprevista durante la fase de documentación y especificación.

---

## 2. Seguridad y Autenticación JWT Zero-Allocation
- [ ] La generación y validación de Access Tokens emplea `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`) optimizado para evitar asignaciones en el hot path.
- [ ] El middleware de autenticación configura `MapInboundClaims = false` para mantener claims estándar sin transformaciones arbitrarias.
- [ ] El tiempo de vida de los Access Tokens está restringido entre 10 y 15 minutos, almacenándose únicamente en memoria volátil del cliente.
- [ ] Los Refresh Tokens se generan con 64 bytes de entropía criptográfica (`RandomNumberGenerator.Fill`).
- [ ] En la base de datos se almacena únicamente el hash criptográfico `SHA-256` del Refresh Token, nunca su valor en claro.
- [ ] Cada Refresh Token está vinculado a un `FamilyId` (`Guid v7`); ante el intento de reutilización de un token rotado, se revocan inmediatamente todos los tokens asociados a dicha familia.
- [ ] En el cliente, el almacenamiento seguro de credenciales (`PlatformSecureTokenStorage`) implementa cifrado nativo por plataforma (`libsecret` en Linux, `DPAPI` en Windows, `Keychain` en macOS).
- [ ] El handler HTTP `JwtAuthenticationHandler` implementa un `SemaphoreSlim(1, 1)` para resolver refrescos concurrentes evitando llamadas duplicadas al endpoint de autenticación.

---

## 3. Persistencia y EF Core 10 con PostgreSQL 17
- [ ] El contenedor oficial PostgreSQL 17 incluye la extensión vectorial (`pgvector/pgvector:pg17`).
- [ ] Todas las entidades de base de datos utilizan claves primarias secuenciales `Guid` generadas con `Guid.CreateVersion7()`.
- [ ] Las columnas `Arguments` y `Result` de `ToolExecution` se persisten como tipo nativo `jsonb` con índices GIN (`jsonb_path_ops`).
- [ ] Los metadatos de configuración y token usage se mapean mediante la directiva nativa `.ToJson()` de EF Core 10.
- [ ] La entidad `AgentSession` cuenta con control de concurrencia optimista mediante mapeo a la columna `xmin` de PostgreSQL.
- [ ] La columna `Embedding` en `ConversationTurn` dispone de un índice vectorial `hnsw` sobre distancia coseno.
- [ ] El contexto `OpenHarnessDbContext` está configurado con pool de conexiones (`AddDbContextPool`), convención snake_case y reintentos automáticos ante fallas transitorias.

---

## 4. Integración del Motor Pi RPC y Sandbox de Seguridad
- [ ] El subproceso `pi` se inicializa con los parámetros `--mode rpc --no-session` para delegar el 100% de la persistencia a PostgreSQL 17.
- [ ] `PiRpcSessionBridge` implementa comunicación bidireccional asíncrona no bloqueante sobre `StandardInput` y `StandardOutput` en formato JSONL.
- [ ] El cierre o fallo inesperado del proceso `pi` es detectado de inmediato, cancelando los turnos en vuelo y liberando recursos (`IAsyncDisposable`).
- [ ] `IApprovalGate` gestiona la suspensión interactiva de herramientas de riesgo mediante `TaskCompletionSource<bool>`, evitando el bloqueo de hilos del servidor.
- [ ] Las solicitudes interactivas `extension_ui_request` de Pi son transformadas en eventos SSE `sandbox.approval_required`.
- [ ] La resolución enviada a `POST /api/v1/sessions/{id}/approvals` se despacha correctamente hacia `pi` como `extension_ui_response`.
- [ ] El streaming de eventos hacia el cliente utiliza `TypedResults.ServerSentEvents` nativo de ASP.NET Core 10.
- [ ] Se mantiene desacoplada la abstracción `IChatClient` (`Microsoft.Extensions.AI`) para soporte multi-proveedor sin dependencia exclusiva de Pi.

---

## 5. Experiencia de Usuario y Frontend Avalonia UI
- [ ] La aplicación de escritorio `OpenHarness.UI.Avalonia` está desacoplada de la lógica HTTP mediante el SDK `OpenHarness.Client.Core`.
- [ ] El patrón MVVM está implementado con generadores de código de `CommunityToolkit.Mvvm 8.4.*` sin lógica de negocio en el code-behind de vistas.
- [ ] Los componentes visuales consumen la paleta y espaciados centralizados en `ArmoniaDesignTokens.axaml`.
- [ ] El shell principal (`HarnessShellView`) implementa un diseño ergonómico de 3 paneles (sesiones, streaming de chat, inspector de herramientas).
- [ ] El renderizado de texto en streaming acumula deltas en un búfer de memoria volcado a la UI a 60 FPS mediante `PeriodicTimer` (16 ms).
- [ ] La visualización de diffs de código utiliza `Avalonia.AvaloniaEdit` con resaltado de sintaxis `TextMateSharp`.
- [ ] El diálogo `SandboxApprovalDialog` ofrece atajos de teclado accesibles (`Enter` para aprobación única, `Ctrl+Enter` para sesión, `Esc` para denegar) y temporizador de expiración visual.

---

## 6. Pruebas y Trazabilidad de Calidad
- [ ] Las pruebas de integración en `OpenHarness.IntegrationTests` se ejecutan contra contenedores reales con `Testcontainers.PostgreSql`.
- [ ] La cobertura de pruebas automatizadas sobre la capa de autenticación, control de concurrencia y flujo de sandbox supera el 90%.
- [ ] Todos los requisitos funcionales (`FR-001` a `FR-015`), criterios de éxito (`SC-001` a `SC-008`) y casos límite (`EC-001` a `EC-006`) cuentan con tareas explícitamente asignadas en `tasks.md`.
