# Lista de Verificación de Calidad: Open Harness Core (Feature 001)

Esta lista de verificación constituye la puerta de calidad para la aceptación y validación técnica de la implementación de **Open Harness Core**. Ninguna fase se considerará completa si presenta elementos sin verificar.

---

## 1. Arquitectura y Stack Base (.NET 10 & C# 14)
- [ ] La solución utiliza el formato `.slnx` (`OpenHarness.slnx`) y compila limpiamente con `dotnet build`.
- [ ] `Directory.Build.props` aplica de manera homogénea:
  - `<TargetFramework>net10.0</TargetFramework>`
  - `<LangVersion>14.0</LangVersion>`
  - `<Nullable>enable</Nullable>`
  - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- [ ] La gestión de paquetes NuGet está centralizada en `Directory.Packages.props` (CPM); ningún proyecto declara versiones en sus `PackageReference`.
- [ ] La Clean Architecture respeta el flujo de dependencias: `Domain` no tiene dependencias externas; `Application` depende sólo de `Domain`; `Infrastructure` y `Api` implementan interfaces de aplicación.
- [ ] No se tocan ni modifican archivos fuera del alcance estricto de cada entrega técnica.

---

## 2. Persistencia y Base de Datos (PostgreSQL 17 + EF Core 10)
- [ ] El contenedor PostgreSQL 17 (`pgvector/pgvector:pg17`) inicia con extensiones `vector`, `uuid-ossp` y `pg_trgm` activas.
- [ ] Todas las entidades del dominio heredan de `AuditableEntity` y generan su clave primaria mediante **`Guid.CreateVersion7()`** en C#.
- [ ] El esquema de base de datos es exclusivamente `harness` y se aplica `UseSnakeCaseNamingConvention()`.
- [ ] Los argumentos y resultados de herramientas en `ToolExecution` se almacenan en columnas nativas `jsonb` con índices GIN `jsonb_path_ops`.
- [ ] Los objetos de configuración (`WorkspaceRulesConfig`, `AgentModelConfig`, `TurnTokenUsage`) se mapean transparentemente con `.ToJson()`.
- [ ] `ConversationTurn.Embedding` está configurado como `vector(1536)` con índice `hnsw` utilizando distancia coseno (`vector_cosine_ops`).
- [ ] `AgentSession` implementa concurrencia optimista forzosa mapeando la columna del sistema PostgreSQL `xmin` como token de concurrencia.
- [ ] Las migraciones de EF Core 10 se generan y aplican sin advertencias de pérdida de datos.

---

## 3. Seguridad y Autenticación JWT Zero-Allocation
- [ ] La emisión y validación de tokens utiliza `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`) eliminando asignaciones superfluas en el hot path.
- [ ] La configuración de `AddJwtBearer` define `MapInboundClaims = false` para preservar los nombres de claims estándar (`sub`, `email`, `role`).
- [ ] Los Access Tokens expiran estrictamente en **10 minutos** y se mantienen únicamente en memoria en el cliente.
- [ ] Los Refresh Tokens se generan con 64 bytes criptográficos aleatorios (`RandomNumberGenerator.Fill`) y en la base de datos se almacena únicamente su hash **`SHA-256`** de 64 caracteres.
- [ ] Cada cadena de renovación posee un `FamilyId` (`Guid v7`). Si un token revocado o previamente reemplazado es presentado en `/api/v1/auth/refresh`, se ejecuta un `ExecuteUpdateAsync` atómico que invalida inmediatamente toda la familia activa de esa sesión.
- [ ] El cliente `JwtAuthenticationHandler` sincroniza peticiones concurrentes ante `401 Unauthorized` mediante `SemaphoreSlim(1, 1)`, garantizando una única llamada al endpoint de refresco.
- [ ] Los Refresh Tokens en el cliente de escritorio se persisten cifrados en el almacén seguro nativo del sistema operativo (`PlatformSecureTokenStorage` con `libsecret` en Linux, `DPAPI` en Windows, `Keychain` en macOS).

---

## 4. Motor Agéntico Pi RPC, Streaming SSE y Puerta de Aprobación
- [ ] `PiRpcSessionBridge` ejecuta `pi` con los argumentos estrictos `--mode rpc --no-session`.
- [ ] El puente redirecciona de manera no bloqueante `StandardInput`, `StandardOutput` y `StandardError`, procesando eventos en formato JSONL.
- [ ] No se almacenan sesiones en el disco de Pi; toda la memoria conversacional y auditoría reside exclusivamente en PostgreSQL 17.
- [ ] Se mantiene la abstracción `IChatClient` (`Microsoft.Extensions.AI`) para posibilitar agentes multi-proveedor o de respaldo.
- [ ] `IApprovalGate` gestiona `TaskCompletionSource<ApprovalDecision>` con temporizador de timeout (5 minutos) sin bloquear hilos del thread pool.
- [ ] Las solicitudes de confirmación (`extension_ui_request`) emiten el evento SSE `sandbox.approval_required` y pausan la ejecución hasta recibir `POST /api/v1/sessions/{id}/approvals`, respondiendo con `extension_ui_response` a Pi.
- [ ] El streaming de eventos hacia el cliente utiliza `TypedResults.ServerSentEvents` en ASP.NET Core 10 y `System.Net.ServerSentEvents.SseParser` en el SDK cliente.
- [ ] La caída imprevista del subproceso `pi` es detectada inmediatamente, marcando la sesión como `Faulted` y notificando a la UI sin colapsar el servicio web.

---

## 5. Frontend Avalonia UI con ArmoniaDesignTokens
- [ ] La arquitectura sigue el patrón MVVM con `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`), sin lógica de negocio en el code-behind (`.axaml.cs`).
- [ ] Todos los componentes visuales consumen los tokens centralizados en `ArmoniaDesignTokens.axaml`.
- [ ] El streaming de texto no satura el hilo `Dispatcher.UIThread`; los deltas se acumulan en memoria y se vuelcan a la vista a 60 FPS mediante `PeriodicTimer` (cadencia de 16 ms).
- [ ] Se integra `Avalonia.AvaloniaEdit` para bloques de código con resaltado de sintaxis y renderizado de diffs.
- [ ] El diálogo `SandboxApprovalDialog` es plenamente accesible mediante atajos de teclado (`Enter` para aprobar, `Ctrl+Enter` para sesión, `Esc` para denegar) y muestra el tiempo restante de expiración.

---

## 6. Pruebas y Validación de Calidad
- [ ] Pruebas unitarias en `OpenHarness.UnitTests` cubren lógica de entidades, generación de hash de tokens y ViewModels.
- [ ] Pruebas de integración en `OpenHarness.IntegrationTests` con **Testcontainers.PostgreSql** validan el esquema real, índices vectoriales HNSW, concurrencia `xmin` y la rotación/revocación por `FamilyId`.
- [ ] Cobertura de código superior al 85% en las capas de Dominio y Aplicación.
- [ ] Trazabilidad completa: todas las tareas y pruebas referencian sus IDs correspondientes (`FR-XXX`, `SC-XXX`, `EC-XXX`).
