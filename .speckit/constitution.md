# Constitución Técnica de Open Harness

## Preámbulo
Esta constitución establece las leyes inmutables, principios de ingeniería, estándares de calidad y convenciones arquitectónicas para el desarrollo y evolución de **Open Harness**. Todos los contribuidores y agentes autónomos deben adherirse estrictamente a estas directrices.

---

## Artículo I: Pila Tecnológica y Plataforma de Ejecución
1. **Runtime y Lenguaje**:
   - Todo el backend, SDK de cliente y capa visual deben construirse exclusivamente sobre **.NET 10.0** utilizando las características modernas de **C# 14**.
   - Se debe mantener activado el modo estricto de tipos nulos (`<Nullable>enable</Nullable>`) y tratamiento de advertencias como errores en compilaciones de release (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
2. **Formato de Solución**:
   - La solución debe gestionarse en formato `.slnx` (`OpenHarness.slnx`) con gestión centralizada de dependencias mediante Central Package Management (`Directory.Packages.props`).
3. **Persistencia y Base de Datos**:
   - **PostgreSQL 17** con la extensión `pgvector` (`pgvector/pgvector:pg17`) es la **única fuente de verdad** para datos relacionales, documentos semiestructurados `jsonb` y embeddings semánticos.
   - El acceso a datos se realizará exclusivamente a través de **EF Core 10** (`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.*`).

---

## Artículo II: Principios Arquitectónicos
1. **Clean Architecture con Vertical Slices**:
   - El núcleo de dominio (`OpenHarness.Domain`) debe ser completamente independiente de frameworks, bases de datos o librerías de infraestructura.
   - Las características de la aplicación (`OpenHarness.Application`) deben organizarse por *Vertical Slices* (Auth, Sessions, Approvals, Workspaces, Agents), garantizando cohesión y bajo acoplamiento.
2. **Identificadores Secuenciales de Alto Rendimiento**:
   - Todas las entidades de persistencia deben utilizar como clave primaria `Guid` generado con `Guid.CreateVersion7()`, optimizado para índices B-Tree en PostgreSQL sin fragmentación.
3. **Persistencia Semiestructurada y Búsqueda Vectorial**:
   - Los argumentos y resultados de ejecución de herramientas deben almacenarse en columnas nativas `jsonb` indexadas con GIN (`jsonb_path_ops`).
   - Las configuraciones complejas y metadatos se mapean utilizando la funcionalidad nativa `.ToJson()` de EF Core.
   - Los turnos conversacionales deben soportar indexación vectorial HNSW sobre embeddings `vector(1536)` para recuperación semántica.

---

## Artículo III: Seguridad y Autenticación JWT Zero-Allocation
1. **Ciclo de Vida de Tokens**:
   - La autenticación entre clientes y backend debe implementarse mediante **JSON Web Tokens (JWT)** utilizando `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`) para garantizar cero asignaciones innecesarias en el hot path.
   - Los **Access Tokens** deben tener un tiempo de vida corto (entre 10 y 15 minutos) y residir únicamente en memoria volátil en los clientes.
2. **Rotación y Detección de Robo de Refresh Tokens**:
   - Los **Refresh Tokens** deben ser cadenas criptográficas aleatorias de 64 bytes (`RandomNumberGenerator.Fill`). En base de datos se almacena únicamente su resumen criptográfico `SHA-256`.
   - Cada cadena de refresco pertenece a un `FamilyId` (`Guid v7`). Si un token ya reemplazado o revocado es presentado para refresco, el sistema debe revocar inmediatamente toda la familia de tokens activa y reportar una brecha de seguridad.
3. **Almacenamiento Seguro Multiplataforma**:
   - En el cliente de escritorio (`OpenHarness.Client.Core` / `OpenHarness.UI.Avalonia`), los refresh tokens deben persistirse cifrados en el almacén seguro nativo del sistema operativo:
     - Linux: `libsecret` / Secret Service API.
     - Windows: Data Protection API (`DPAPI`).
     - macOS: Keychain Services API.

---

## Artículo IV: Motor de Agentes y Ejecución Pi RPC
1. **Integración Nativa de Pi**:
   - El motor principal de ejecución de herramientas de código y razonamiento es **Pi (`pi --mode rpc --no-session`)**.
   - Se ejecuta como un subproceso gestionado por `PiRpcSessionBridge` en `OpenHarness.Infrastructure`, comunicándose mediante líneas JSON delimitadas por salto de línea (JSONL) sobre `StandardInput` y `StandardOutput`.
   - El parámetro `--no-session` es obligatorio: Pi no gestionará sesiones en disco local; toda la persistencia de estado y turnos se delega a PostgreSQL 17 mediante EF Core 10.
2. **Abstracción Multi-Proveedor**:
   - Se debe mantener la interfaz `IChatClient` de `Microsoft.Extensions.AI` para permitir agentes complementarios, modelos de respaldo y pruebas sin depender estrictamente del binario `pi`.
3. **Sandbox y Puerta de Aprobación Interactiva (`IApprovalGate`)**:
   - Toda herramienta que modifique el entorno (`bash`, `write`, `edit`) o comandos de riesgo deben ser validados contra las políticas `WorkspacePolicy`.
   - Cuando una acción requiera consentimiento humano, `PiRpcSessionBridge` intercepta el evento `extension_ui_request`, bloquea asíncronamente mediante `IApprovalGate` (`TaskCompletionSource<bool>`), emite un evento SSE `sandbox.approval_required` a la UI y espera la decisión del usuario (`extension_ui_response`).

---

## Artículo V: Experiencia de Usuario y UI Avalonia
1. **Patrón MVVM y Reactividad**:
   - La interfaz gráfica en `OpenHarness.UI.Avalonia` debe regirse por el patrón **MVVM** asistido por los generadores de código de `CommunityToolkit.Mvvm 8.4.*`.
   - No se permite lógica de negocio o de orquestación en el code-behind de los archivos `.axaml.cs`.
2. **Tokens de Diseño Armonia**:
   - Toda la paleta de colores, tipografía, espaciados y estados visuales deben consumir las definiciones centralizadas en `ArmoniaDesignTokens.axaml`.
3. **Streaming Fluido a 60 FPS**:
   - El consumo de eventos SSE no debe saturar el hilo principal de la interfaz (`Dispatcher.UIThread`). Los deltas de texto deben acumularse en búferes de memoria y volcarse a la vista a una cadencia de 16 milisegundos (`PeriodicTimer`).
4. **Editor y Diffs**:
   - La visualización de cambios en archivos de código debe emplear `Avalonia.AvaloniaEdit` con resaltado de sintaxis `TextMateSharp` y presentación de diferencias (diffs) unificados o lado a lado.

---

## Artículo VI: Estándares de Calidad y Pruebas
1. **Pirámide de Pruebas**:
   - Pruebas unitarias en `OpenHarness.UnitTests` para lógica de dominio, slices de aplicación y ViewModels.
   - Pruebas de integración en `OpenHarness.IntegrationTests` utilizando **Testcontainers** para levantar instancias reales de PostgreSQL 17 con `pgvector` en entornos aislados.
2. **Trazabilidad y Cobertura**:
   - Cada funcionalidad desarrollada debe referenciar explícitamente sus identificadores de especificación (`FR-xxx`, `US-xxx`, `SC-xxx`, `EC-xxx`), garantizando trazabilidad total desde la especificación hasta la implementación.
