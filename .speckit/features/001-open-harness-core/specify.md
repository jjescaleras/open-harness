# Especificación de Requerimientos: Open Harness Core (Feature 001)

## 1. Resumen Ejecutivo
**Open Harness** es una plataforma empresarial moderna diseñada para la orquestación segura y de alto rendimiento de agentes de inteligencia artificial y ejecución supervisada de herramientas en entornos controlados (*sandboxing*). Construida sobre la base de **.NET 10** (`net10.0`) y **C# 14**, aprovecha la potencia de **PostgreSQL 17** (`pgvector/pgvector:pg17`) y **EF Core 10** como única fuente de verdad persistente, el motor de ejecución de código **Pi (`pi --mode rpc --no-session`)** como ejecutor agéntico asíncrono, una arquitectura de seguridad JWT zero-allocation con detección proactiva de robo de tokens por `FamilyId`, y una interfaz gráfica nativa reactiva desarrollada con **Avalonia UI** y el sistema de diseño corporativo **ArmoniaDesignTokens**.

Esta especificación formaliza los requisitos funcionales, historias de usuario, criterios de éxito cuantitativos y el manejo riguroso de casos límite para las cinco fases fundamentales del núcleo de Open Harness.

---

## 2. Historias de Usuario (User Stories)

### US-001: Autenticación Segura y Rotación de Sesión Transparente
- **Como** usuario del sistema (desarrollador u operador),
- **Quiero** autenticarme mediante credenciales corporativas y mantener una sesión prolongada sin desconexiones repentinas,
- **Para** interactuar con la plataforma continuamente mientras los tokens de acceso y refresco rotan de manera transparente, segura y cifrada en el almacén nativo de mi sistema operativo.

### US-002: Ejecución Asíncrona y Streaming Fluido con Pi RPC
- **Como** usuario interactuando con el agente inteligente,
- **Quiero** enviar consignas técnicas y observar la respuesta generativa, el razonamiento interno y las invocaciones de herramientas transmitidas en tiempo real por SSE,
- **Para** recibir asistencia cognitiva inmediata con retroalimentación visual a 60 FPS sin bloqueos en la interfaz gráfica.

### US-003: Supervisión y Aprobación Interactiva de Sandbox
- **Como** administrador de políticas de seguridad u operador de infraestructura,
- **Quiero** que cualquier herramienta sensible o potencialmente destructiva (ejecución bash, escritura de archivos) solicite mi autorización explícita antes de ejecutarse,
- **Para** mantener control absoluto sobre los cambios en el sistema de archivos y mitigar riesgos operacionales.

### US-004: Inspección de Cambios de Código y Auditoría de Herramientas
- **Como** ingeniero de software,
- **Quiero** examinar en un panel de código con resaltado de sintaxis los diffs exactos que el agente propone aplicar, junto con los argumentos y resultados de cada herramienta,
- **Para** validar la corrección técnica antes de autorizar la consolidación de los cambios en el repositorio.

### US-005: Registro Inmutable y Persistencia Centralizada
- **Como** auditor técnico o responsable de cumplimiento,
- **Quiero** que cada interacción, turno de conversación, consumo de tokens y llamada de herramienta quede registrado con claves secuenciales `Guid v7` en PostgreSQL 17,
- **Para** auditar retrospectivamente cualquier sesión de trabajo, analizar costos y disponer de recuperación semántica mediante vectores.

---

## 3. Requisitos Funcionales (Functional Requirements)

### Fase 1: Dominio, EF Core 10 y Persistencia PostgreSQL 17
- **FR-001**: La solución debe estructurarse en formato XML moderno `.slnx` sobre .NET 10 (`net10.0`) y C# 14, con `<Nullable>enable</Nullable>`, Central Package Management en `Directory.Packages.props` y soporte Docker Compose para PostgreSQL 17 con extensiones `vector`, `uuid-ossp` y `pg_trgm`.
- **FR-002**: El modelo de dominio (`OpenHarness.Domain`) debe definir las entidades `User`, `RefreshToken`, `WorkspacePolicy`, `AgentSession`, `ConversationTurn` y `ToolExecution`, generando todas sus claves primarias en memoria mediante **`Guid.CreateVersion7()`** para evitar fragmentación en índices B-Tree.
- **FR-003**: El contexto de datos `OpenHarnessDbContext` en EF Core 10 debe configurarse en el esquema dedicado `harness` con `UseSnakeCaseNamingConvention()`, mapeando documentos de configuración mediante `.ToJson()`, columnas nativas `jsonb` con índices **GIN (`jsonb_path_ops`)** para argumentos y resultados de herramientas, control de concurrencia optimista con la columna de sistema **`xmin`** en sesiones, e indexación vectorial **HNSW (`vector(1536)`)** sobre `ConversationTurn.Embedding`.

### Fase 2: Seguridad y Autenticación JWT Zero-Allocation
- **FR-004**: El sistema debe exponer endpoints de autenticación (`/api/v1/auth/login`, `/register`, `/refresh`, `/revoke`) utilizando **`JsonWebTokenHandler`** con `MapInboundClaims = false`, emitiendo **Access Tokens de 10 minutos** y **Refresh Tokens rotatorios de 7 días** generados con 64 bytes criptográficos aleatorios.
- **FR-005**: La base de datos debe almacenar exclusivamente el hash **`SHA-256`** de los Refresh Tokens junto con su `FamilyId` (`Guid v7`). Si se detecta un intento de reutilización de un token revocado o previamente rotado, el sistema debe ejecutar de forma atómica un **`ExecuteUpdateAsync`** invalidando toda la familia de tokens activa de la sesión y registrando una alerta de seguridad.

### Fase 3: Motor del Agente Pi RPC, Streaming SSE y Puerta de Aprobación
- **FR-006**: La infraestructura debe incorporar **`PiRpcSessionBridge`** para orquestar el subproceso **`pi --mode rpc --no-session`** comunicándose mediante líneas JSONL asíncronas sobre `StandardInput`, `StandardOutput` y `StandardError`, persistiendo todo el estado y los turnos exclusivamente en PostgreSQL 17.
- **FR-007**: La arquitectura debe proveer la abstracción **`IChatClient`** (`Microsoft.Extensions.AI`) para permitir el intercambio de proveedores LLM y modelos de respaldo sin modificar la lógica de la aplicación.
- **FR-008**: El sistema debe implementar la compuerta asíncrona **`IApprovalGate`** (`TaskCompletionSource<ApprovalDecision>`) para interceptar solicitudes `extension_ui_request` de Pi y evaluar `WorkspacePolicy`. El backend debe transmitir eventos en tiempo real hacia los clientes mediante **`TypedResults.ServerSentEvents`** (`text.delta`, `thinking.delta`, `tool.started`, `sandbox.approval_required`, `tool.completed`, `turn.completed`) y resolver la promesa al recibir `POST /api/v1/sessions/{id}/approvals`, enviando `extension_ui_response` a Pi sin bloquear hilos del servidor.

### Fase 4: SDK Cliente Core
- **FR-009**: El proyecto `OpenHarness.Client.Core` debe encapsular la comunicación HTTP y streaming en un SDK cliente que incluye:
  - `JwtAuthenticationHandler` (`DelegatingHandler`) con **`SemaphoreSlim(1, 1)`** para interceptar respuestas 401, ejecutar un refresco transparente y reintentar peticiones concurrentes sin duplicación de llamadas.
  - `PlatformSecureTokenStorage` para persistencia cifrada del Refresh Token en llaveros del SO (`libsecret` en Linux, `DPAPI` en Windows, `Keychain` en macOS).
  - `HarnessSseClient` para deserialización eficiente de eventos SSE mediante **`System.Net.ServerSentEvents.SseParser`** con reconexión automática.

### Fase 5: Interfaz de Usuario Avalonia con ArmoniaDesignTokens
- **FR-010**: La aplicación visual `OpenHarness.UI.Avalonia` debe desarrollarse bajo el patrón MVVM (`CommunityToolkit.Mvvm`), incorporando:
  - Sistema de tokens de diseño centralizado en `ArmoniaDesignTokens.axaml` (estilos oscuros, tipografía y espaciados).
  - Layout ergonómico de tres paneles (árbol de sesiones, conversación con streaming y visor de herramientas).
  - Búfer de streaming de texto desacoplado que actualiza la UI a 60 FPS mediante `PeriodicTimer` (cadencia de 16 ms) para no saturar el hilo principal.
  - Integración de `Avalonia.AvaloniaEdit` para inspección de código y diffs con sintaxis coloreada.
  - Diálogo modal `SandboxApprovalDialog` con atajos de teclado accesibles (`Enter` para autorizar, `Ctrl+Enter` para recordar sesión, `Esc` para denegar).

---

## 4. Criterios de Éxito Medibles (Success Criteria)

- **SC-001**: El despliegue de PostgreSQL 17 y la ejecución de migraciones EF Core 10 (`InitialHarnessSchema`) se completa de forma limpia y desatendida en menos de 5 segundos.
- **SC-002**: La validación de un Access Token JWT con `JsonWebTokenHandler` consume menos de 0.5 milisegundos por solicitud bajo condiciones de tráfico nominal, sin asignaciones de memoria innecesarias.
- **SC-003**: Ante la detección de reutilización de un Refresh Token revocado, el 100% de los tokens de la familia `FamilyId` quedan invalidados en base de datos en menos de 30 milisegundos mediante `ExecuteUpdateAsync`.
- **SC-004**: El subproceso `pi --mode rpc --no-session` inicializa su canal JSONL y responde al primer prompt en menos de 300 milisegundos.
- **SC-005**: Las solicitudes de confirmación de sandbox (`extension_ui_request`) pausan la ejecución del agente sin bloquear ningún hilo en el pool de .NET y emiten el evento SSE `sandbox.approval_required` en menos de 50 milisegundos.
- **SC-006**: La interfaz gráfica de Avalonia mantiene una tasa sostenida de 60 FPS durante ráfagas de streaming intensivo (> 80 tokens/segundo) sin degradar la capacidad de respuesta ni congelar el hilo de UI.

---

## 5. Casos Límite y Manejo de Errores (Edge Cases)

### EC-001: Reutilización de Refresh Token Revocado (Detección de Robo)
- **Escenario**: Un atacante intercepta un Refresh Token viejo e intenta generar un nuevo Access Token mientras el usuario legítimo ya rotó dicho token.
- **Manejo**: El sistema consulta el hash SHA-256 en `harness.refresh_tokens`. Al constatar que `RevokedAtUtc` no es nulo o que `ReplacedByTokenHash` ya existe, invoca de inmediato:
  ```csharp
  await dbContext.RefreshTokens
      .Where(t => t.FamilyId == token.FamilyId && t.RevokedAtUtc == null)
      .ExecuteUpdateAsync(s => s
          .SetProperty(t => t.RevokedAtUtc, DateTimeOffset.UtcNow)
          .SetProperty(t => t.RevocationReason, "Automatic revocation: Compromised token reuse detected"));
  ```
  Se retorna HTTP 401 con código de error `SessionCompromised` y se registra una alerta de auditoría crítica.

### EC-002: Caída o Terminación Inesperada del Subproceso `pi --mode rpc`
- **Escenario**: El proceso `pi` sufre un error fatal (OOM, SIGKILL o salida con código distinto de 0) en medio de la ejecución de una herramienta.
- **Manejo**: `PiRpcSessionBridge` detecta el cierre inesperado del stream `stdout`/`stderr`. Inmediatamente marca el estado de la sesión en base de datos como `Faulted`, cancela cualquier `TaskCompletionSource` pendiente en `IApprovalGate`, notifica a la UI mediante un evento SSE de terminación con error y reinicia el subproceso limpiamente para turnos posteriores.

### EC-003: Colisión de Concurrencia Optimista con `xmin` en Sesión de Agente
- **Escenario**: Dos peticiones concurrentes o un agente en segundo plano intentan actualizar simultáneamente los contadores de tokens o el estado de `AgentSession`.
- **Manejo**: EF Core 10 detecta una discrepancia en la columna de sistema `xmin` de PostgreSQL y lanza una `DbUpdateConcurrencyException`. El servicio recarga la entidad fresca desde la base de datos, consolida los contadores acumulativos de tokens y reintenta la operación de persistencia de forma transparente.

### EC-004: Refresco Concurrente 401 en el Cliente con `SemaphoreSlim(1, 1)`
- **Escenario**: Múltiples peticiones HTTP concurrentes desde la UI reciben respuesta `401 Unauthorized` al expirar simultáneamente el Access Token de 10 minutos.
- **Manejo**: Todas las peticiones convergen en `JwtAuthenticationHandler`. El primer hilo adquiere `await _refreshSemaphore.WaitAsync()`, ejecuta la llamada `POST /api/v1/auth/refresh`, actualiza el token en memoria y libera el semáforo. Los hilos restantes, al ingresar a la sección crítica, comprueban que el token en memoria ya es distinto al que provocó el 401 y proceden a reintentar su petición sin invocar nuevamente el endpoint de refresco.

### EC-005: Timeout en Aprobación Interactiva de Sandbox (`IApprovalGate`)
- **Escenario**: Se presenta el diálogo `SandboxApprovalDialog` pero el usuario no emite una decisión en un plazo determinado (por defecto 5 minutos).
- **Manejo**: El temporizador vinculado al `TaskCompletionSource` expira con un `TimeoutException`. `IApprovalGate` marca la herramienta como `DeniedByTimeout`, emite un evento SSE `tool.denied` y envía a `pi` la respuesta `{"type": "extension_ui_response", "confirmed": false}`, permitiendo al agente continuar su flujo informando que la herramienta fue rechazada por falta de respuesta humana.

### EC-006: Entrada JSONL Malformada o Deserialización Parcial desde Pi
- **Escenario**: El subproceso `pi` emite una línea que no es un objeto JSON válido (por ejemplo, un volcado de depuración no capturado o fragmentación de búfer).
- **Manejo**: El lector asíncrono en `PiRpcSessionBridge` captura `JsonException`, registra la línea sin procesar en el log de diagnóstico de bajo nivel y continúa la lectura del stream sin colapsar el pipeline de procesamiento de eventos ni interrumpir el canal SSE.
