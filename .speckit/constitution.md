# Constitución Técnica de Open Harness

## Preámbulo
Esta constitución establece las directrices inmutables, principios de ingeniería, estándares de calidad y convenciones arquitectónicas para el desarrollo y evolución de **Open Harness**. Cualquier contribución, refactorización o desarrollo por parte de ingenieros o agentes autónomos debe adherirse rigurosamente a estos principios.

---

## Artículo I: Stack Tecnológico y Plataforma de Ejecución
1. **Runtime y Lenguaje Moderno Estricto**:
   - Todo el backend, SDK de cliente y capa visual deben construirse exclusivamente sobre **.NET 10.0 (`net10.0`)** utilizando las características modernas de **C# 14**.
   - Se debe mantener activado el análisis estricto de tipos nulos (`<Nullable>enable</Nullable>`) y tratamiento estricto de advertencias como errores en compilaciones de release (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
2. **Formato de Solución y Dependencias**:
   - La solución debe gestionarse en formato XML moderno `.slnx` (`OpenHarness.slnx`).
   - La gestión de versiones de paquetes se realiza de forma centralizada mediante Central Package Management (CPM) en `Directory.Packages.props`. Ningún proyecto `.csproj` individual debe declarar versiones directas en `PackageReference`.
3. **Persistencia como Única Fuente de Verdad**:
   - **PostgreSQL 17** con la extensión `pgvector` (`pgvector/pgvector:pg17`) es la **única fuente de verdad** para datos relacionales, estados de sesión, auditoría de herramientas y embeddings vectoriales.
   - Todo el acceso a datos se realiza a través de **Entity Framework Core 10** (`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.*`).

---

## Artículo II: Principios Arquitectónicos
1. **Clean Architecture Pragmática con Vertical Slices**:
   - La solución se desacopla en capas bien delimitadas con responsabilidades unívocas:
     - `OpenHarness.Domain`: Entidades, Value Objects, Enums e interfaces puras. Completamente agnóstico de frameworks externos.
     - `OpenHarness.Application`: Casos de uso y lógica de orquestación organizados en *Vertical Slices* (Auth, Sessions, Approvals, Workspaces).
     - `OpenHarness.Infrastructure`: Implementaciones concretas de EF Core 10, puente Pi RPC, seguridad JWT y proveedores externos.
     - `OpenHarness.Api`: Exposición Minimal API en ASP.NET Core 10, autenticación y streaming SSE nativo.
     - `OpenHarness.Client.Core`: SDK cliente desacoplado, gestión de tokens reactiva y cliente SSE.
     - `OpenHarness.UI.Avalonia`: Capa de presentación visual multiplataforma en Avalonia UI.
2. **Identificadores Secuenciales de Alto Rendimiento**:
   - Todas las entidades de persistencia deben generar su clave primaria en C# mediante **`Guid.CreateVersion7()`** para maximizar la localidad de caché y prevenir la fragmentación de índices B-Tree en PostgreSQL.
3. **Convención y Esquema de Base de Datos**:
   - Todas las tablas, índices y secuencias pertenecen al esquema dedicado `harness`.
   - Se aplica `UseSnakeCaseNamingConvention()` para nombres de tablas y columnas.
   - Concurrencia optimista forzosa mediante la columna del sistema PostgreSQL `xmin` mapeada como RowVersion.
4. **Persistencia Híbrida Semiestructurada y Búsqueda Vectorial**:
   - Columnas `jsonb` con índices GIN (`jsonb_path_ops`) para argumentos (`Arguments`) y resultados (`Result`) de herramientas.
   - Objetos de configuración tipados mapeados transparentemente con la funcionalidad nativa `.ToJson()` de EF Core.
   - Embeddings de 1536 dimensiones en columnas `vector(1536)` indexadas con **HNSW** (`vector_cosine_ops`) para recuperación semántica.

---

## Artículo III: Seguridad y Autenticación JWT Zero-Allocation
1. **Validación y Emisión Zero-Allocation**:
   - La emisión y validación de tokens en ASP.NET Core 10 debe utilizar **`JsonWebTokenHandler`** (`Microsoft.IdentityModel.JsonWebTokens`) para eliminar asignaciones innecesarias en el *hot path*.
   - Configuración de `AddJwtBearer` con **`MapInboundClaims = false`** para preservar los nombres de claims estándar (`sub`, `email`, `role`) sin overhead de mapeo SOAP legacy.
2. **Ciclo de Vida de Tokens**:
   - **Access Tokens**: Vida útil de **10 minutos**, almacenados exclusivamente en memoria volátil del cliente (`AuthSessionState`).
   - **Refresh Tokens**: Vida útil de **7 días**, generados con 64 bytes criptográficos aleatorios (`RandomNumberGenerator.Fill`). En base de datos se almacena únicamente su resumen criptográfico en formato hash **`SHA-256`** hexadecimal (64 caracteres).
3. **Detección de Robo por Familia (`FamilyId`)**:
   - Cada cadena de renovación comparte un identificador `FamilyId` (`Guid.CreateVersion7()`).
   - Si se presenta un Refresh Token revocado o ya rotado, se activa la invalidación atómica de toda la familia activa de esa sesión mediante **`ExecuteUpdateAsync`**, cerrando la sesión de inmediato y alertando de potencial brecha de seguridad.
4. **Refresco Reactivo Concurrente en Cliente**:
   - El SDK `OpenHarness.Client.Core` utiliza `JwtAuthenticationHandler` (`DelegatingHandler`) respaldado por un **`SemaphoreSlim(1, 1)`** para interceptar respuestas `401 Unauthorized`.
   - El primer hilo ejecuta la rotación; los hilos concurrentes detectan el token actualizado y reintentan sin saturar el endpoint de autenticación con múltiples solicitudes de refresco.
5. **Almacenamiento Seguro Multiplataforma**:
   - En el frontend de escritorio, los Refresh Tokens se guardan cifrados mediante `PlatformSecureTokenStorage` utilizando los llaveros seguros del sistema operativo: Secret Service (`libsecret`) en Linux, DPAPI en Windows y Keychain en macOS.

---

## Artículo IV: Motor Agéntico Basado en Pi RPC y Streaming SSE
1. **Integración Nativa de Pi en Modo RPC**:
   - El motor de ejecución de código, edición difusa y manipulación de entorno es **Pi (`pi --mode rpc --no-session`)**.
   - Se ejecuta como un subproceso supervisado por `PiRpcSessionBridge` en `OpenHarness.Infrastructure`, comunicándose mediante líneas JSONL asíncronas sobre `StandardInput`, `StandardOutput` y `StandardError`.
   - La bandera `--no-session` es obligatoria: Pi opera sin estado en disco local; toda la memoria y auditoría conversacional reside exclusivamente en PostgreSQL 17 mediante EF Core 10.
2. **Abstracción Multi-Proveedor**:
   - Se mantiene la interfaz `IChatClient` de `Microsoft.Extensions.AI` para permitir agentes alternativos y pruebas de integración sin dependencia rígida del ejecutable `pi`.
3. **Compuerta de Aprobación de Sandbox Asíncrona (`IApprovalGate`)**:
   - Cuando una herramienta requiera autorización según `WorkspacePolicy`, Pi emite un evento `extension_ui_request`.
   - El backend captura la solicitud en `IApprovalGate` utilizando un `TaskCompletionSource<ApprovalDecision>`, cambia el estado de la sesión a `WaitingForApproval` y notifica a la UI mediante Server-Sent Events.
   - Al recibir la aprobación o denegación del usuario (`POST /api/v1/sessions/{id}/approvals`), se resuelve la promesa asíncrona y se escribe la respuesta `extension_ui_response` en el `StandardInput` de Pi, sin bloquear ningún hilo del servidor.
4. **Streaming SSE Nativo**:
   - La transmisión de eventos en tiempo real se expone mediante **`TypedResults.ServerSentEvents`** en ASP.NET Core 10, transmitiendo deltas de texto, pensamiento y ciclo de vida de herramientas.
   - En el cliente, el consumo se realiza mediante **`System.Net.ServerSentEvents.SseParser`**.

---

## Artículo V: Experiencia de Usuario y UI Avalonia
1. **Patrón MVVM Estricto**:
   - La interfaz en `OpenHarness.UI.Avalonia` debe seguir el patrón MVVM utilizando los generadores de código de `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
   - Se prohíbe lógica de negocio o comunicación de red en el code-behind (`.axaml.cs`).
2. **Tokens de Diseño Armonia**:
   - Todo componente visual debe consumir las variables semánticas definidas en `ArmoniaDesignTokens.axaml` (paleta corporativa oscura, tipografía, radios de curvatura y espaciados).
3. **Streaming Fluido a 60 FPS**:
   - Para no degradar el hilo de renderizado (`Dispatcher.UIThread`), los deltas de texto SSE se encolan en búferes de memoria y se vacían hacia la UI a una tasa controlada de 16 ms (60 FPS) mediante un `PeriodicTimer`.
4. **Inspección de Código y Diálogos de Seguridad**:
   - Integración de `Avalonia.AvaloniaEdit` para diffs coloreados y resaltado de sintaxis.
   - El diálogo `SandboxApprovalDialog` debe ser accesible y controlable mediante atajos de teclado (`Enter` para aprobar, `Ctrl+Enter` para recordar en la sesión, `Esc` para rechazar).

---

## Artículo VI: Calidad, Testing y Trazabilidad SDD
1. **Estrategia de Pruebas Automatizadas**:
   - Pruebas unitarias completas en `OpenHarness.UnitTests` para entidades de dominio, lógica de JWT y ViewModels.
   - Pruebas de integración en `OpenHarness.IntegrationTests` con **Testcontainers.PostgreSql** para verificar migraciones, índices vectoriales HNSW, concurrencia `xmin` y rotación de tokens sobre un motor PostgreSQL 17 real.
2. **Trazabilidad de Requisitos**:
   - Cada componente implementado debe mapearse directamente contra los identificadores de especificación (`FR-XXX`, `US-XXX`, `SC-XXX`, `EC-XXX`), garantizando cumplimiento absoluto con el modelo Spec-Driven Development (SDD).
