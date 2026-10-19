# Especificación de Requerimientos: Plataforma Open Harness

## 1. Visión General
**Open Harness** es una plataforma empresarial para la orquestación de agentes de IA, ejecución controlada de herramientas en entornos aislados (*sandboxing*), persistencia relacional/vectorial y streaming de alta velocidad a una interfaz gráfica nativa. La arquitectura combina el alto rendimiento de **.NET 10**, la robustez de **PostgreSQL 17 (EF Core 10)**, el motor de ejecución de código **Pi (`pi --mode rpc --no-session`)** y el renderizado multiplataforma acelerado de **Avalonia UI 11.x**.

---

## 2. Historias de Usuario (User Stories)

- **US-001: Autenticación y Gestión de Sesión Transparente**
  - *Como* desarrollador o analista que utiliza la plataforma,
  - *Quiero* autenticarme con mis credenciales y mantener mi sesión de trabajo activa sin interrupciones ni deslogueos inesperados,
  - *Para* trabajar de manera continua mientras mis tokens se rotan de forma segura y transparente en segundo plano.

- **US-002: Interacción Fluida con Agente Inteligente Pi**
  - *Como* usuario que interactúa con el asistente de código,
  - *Quiero* enviar instrucciones complejas y visualizar la respuesta generativa, razonamiento interno y uso de herramientas en tiempo real,
  - *Para* recibir asistencia inmediata sin latencia percibida ni bloqueos en la interfaz gráfica.

- **US-003: Control y Aprobación de Sandbox de Seguridad**
  - *Como* usuario responsable o administrador de infraestructura,
  - *Quiero* que cualquier comando potencialmente destructivo o modificación del sistema de archivos requiera mi confirmación interactiva,
  - *Para* garantizar que el agente no altere archivos críticos ni ejecute código peligroso sin mi supervisión.

- **US-004: Inspección de Diffs y Cambios de Código**
  - *Como* ingeniero de software,
  - *Quiero* inspeccionar los cambios propuestos en los archivos mediante una vista de diffs con sintaxis coloreada antes de que se consoliden en disco,
  - *Para* verificar la exactitud y calidad del código generado por el agente.

- **US-005: Auditoría Completa y Persistencia de Sesiones**
  - *Como* auditor de sistemas u operador,
  - *Quiero* que cada mensaje, comando ejecutado, respuesta y consumo de tokens quede registrado con trazabilidad estricta en PostgreSQL 17,
  - *Para* auditar el historial de ejecuciones, medir costos y recuperar sesiones pasadas de forma confiable.

---

## 3. Requisitos Funcionales (Functional Requirements)

### Fase 1: Infraestructura Base y Persistencia
- **FR-001**: La solución debe estructurarse en formato XML moderno `.slnx` sobre .NET 10 y C# 14, con Central Package Management (`Directory.Packages.props`) y soporte de despliegue contenerizado para PostgreSQL 17 con la extensión `pgvector`.
- **FR-002**: El modelo de dominio debe contener las entidades `User`, `RefreshToken`, `WorkspacePolicy`, `AgentSession`, `ConversationTurn` y `ToolExecution`, utilizando exclusivamente `Guid.CreateVersion7()` como identificadores primarios secuenciales.
- **FR-003**: El contexto de datos `OpenHarnessDbContext` en EF Core 10 debe configurar mapeo nativo `jsonb` con índices GIN `jsonb_path_ops` para argumentos y resultados de herramientas, configuración de modelos con `.ToJson()`, control de concurrencia optimista con `xmin` e indexación vectorial HNSW en `ConversationTurn.Embedding`.

### Fase 2: Autenticación JWT y Seguridad
- **FR-004**: El sistema debe exponer endpoints de autenticación (`/api/v1/auth/login`, `/register`, `/refresh`, `/revoke`) implementando hash de contraseñas seguro y validaciones de cuenta activa.
- **FR-005**: La emisión y validación de Access Tokens JWT debe realizarse mediante `JsonWebTokenHandler` en .NET 10 con un tiempo de expiración estricto de 10 a 15 minutos y `MapInboundClaims = false`.
- **FR-006**: Los Refresh Tokens deben generarse como cadenas criptográficas de 64 bytes (`RandomNumberGenerator`), almacenando su hash SHA-256 y un `FamilyId` común; si se detecta la presentación de un token previamente rotado, el sistema debe revocar inmediatamente toda la familia de tokens asociada.

### Fase 3: Motor del Agente Pi RPC y Aprobaciones
- **FR-007**: La infraestructura debe incluir `PiRpcSessionBridge` para lanzar y supervisar el subproceso `pi --mode rpc --no-session`, comunicándose de forma bidireccional mediante líneas JSONL en `stdin`/`stdout`.
- **FR-008**: La arquitectura debe conservar la abstracción `IChatClient` (`Microsoft.Extensions.AI`) para permitir el uso de modelos LLM directos y proveedores alternativos sin acoplamiento forzado a Pi.
- **FR-009**: El sistema debe implementar `IApprovalGate` mediante `TaskCompletionSource` para interceptar solicitudes `extension_ui_request` de Pi cuando la política `WorkspacePolicy` requiera confirmación humana, deteniendo la ejecución sin bloquear hilos del servidor.
- **FR-010**: El backend debe transmitir eventos en tiempo real hacia los clientes utilizando Server-Sent Events nativo de ASP.NET Core 10 (`TypedResults.ServerSentEvents`) con eventos estructurados (`text.delta`, `thinking.delta`, `tool.started`, `tool.completed`, `sandbox.approval_required`, `turn.completed`).
- **FR-011**: El sistema debe exponer el endpoint `POST /api/v1/sessions/{id}/approvals` para recibir la decisión de aprobación o rechazo del usuario y retransmitirla a Pi como `extension_ui_response`.

### Fase 4: SDK Cliente Core
- **FR-012**: El SDK `OpenHarness.Client.Core` debe proporcionar un `JwtAuthenticationHandler` basado en `DelegatingHandler` con `SemaphoreSlim(1, 1)` para interceptar respuestas 401, ejecutar un único refresco de token concurrente y reintentar las peticiones en vuelo de manera transparente.
- **FR-013**: El SDK debe implementar `PlatformSecureTokenStorage` para persistir los Refresh Tokens cifrados en el almacén del sistema operativo (`libsecret` en Linux, `DPAPI` en Windows, `Keychain` en macOS).

### Fase 5: Frontend UI Avalonia
- **FR-014**: La interfaz de escritorio en `OpenHarness.UI.Avalonia` debe desarrollarse con Avalonia UI 11.x y MVVM (`CommunityToolkit.Mvvm`), incorporando un shell de 3 paneles (árbol de sesiones, vista de conversación/streaming e inspector de herramientas) y estilos centralizados en `ArmoniaDesignTokens`.
- **FR-015**: La vista de streaming debe acumular deltas en un búfer de 16 ms (60 FPS) gobernado por `PeriodicTimer`, integrar el editor `AvaloniaEdit` para inspección de diffs de código y desplegar el diálogo modal accesible `SandboxApprovalDialog` con atajos de teclado (`Enter` para aprobar, `Ctrl+Enter` para sesión, `Esc` para denegar).

---

## 4. Criterios de Éxito Medibles (Success Criteria)

- **SC-001**: El despliegue inicial de PostgreSQL 17 con `pgvector` y la ejecución de migraciones EF Core 10 se completa en menos de 5 segundos.
- **SC-002**: La generación y validación de un Access Token JWT en .NET 10 consume menos de 1 milisegundo en condiciones de carga nominal.
- **SC-003**: Ante un intento de reutilización de un Refresh Token ya rotado, el 100% de los tokens de la familia `FamilyId` quedan revocados en base de datos en menos de 50 milisegundos.
- **SC-004**: La inicialización del puente `PiRpcSessionBridge` y el inicio del flujo JSONL sobre `stdin`/`stdout` se produce en menos de 300 milisegundos.
- **SC-005**: El 100% de las herramientas sujetas a política de confirmación (`WorkspacePolicy`) pausan el flujo del agente y emiten el evento `sandbox.approval_required` sin generar bloqueos en el runtime.
- **SC-006**: La interfaz gráfica mantiene un refresco fluido de 60 cuadros por segundo durante el streaming intensivo de tokens sin reportar caídas de rendimiento en el hilo de UI.
- **SC-007**: Ante expiración de token durante peticiones concurrentes, se emite una única petición HTTP al endpoint de refresco, resolviendo exitosamente todas las peticiones encoladas.
- **SC-008**: La cobertura de pruebas automatizadas en lógica de dominio, persistencia, autenticación y ciclo de aprobación supera el 90%.

---

## 5. Casos Límite y Manejo de Excepciones (Edge Cases)

- **EC-001: Caída o Terminación Inesperada del Proceso Pi**
  - Si el subproceso `pi` finaliza abruptamente con código de salida distinto de 0 o es cancelado por el sistema, `PiRpcSessionBridge` debe detectar el cierre del stream `stdout`, actualizar el estado del turno y la sesión a `Failed` en PostgreSQL 17 y notificar a la UI mediante un evento SSE de error sin dejar procesos huérfanos.
- **EC-002: Intento Concurrente de Reutilización de Refresh Token (Ataque Man-In-The-Middle)**
  - Si dos peticiones simultáneas presentan el mismo Refresh Token viejo, la transacción de EF Core con aislamiento de lectura o bloqueo pesimista en PostgreSQL debe detectar el conflicto, revocar inmediatamente el `FamilyId` completo, cancelar ambas sesiones y registrar un incidente de seguridad en el log.
- **EC-003: Tiempo de Espera (Timeout) en Aprobación de Sandbox**
  - Si el usuario no responde al diálogo de aprobación `SandboxApprovalDialog` dentro del tiempo máximo configurado (por ejemplo, 5 minutos), `IApprovalGate` debe expirar la promesa (`TaskCompletionSource`), marcar la ejecución de la herramienta como `DeniedByTimeout` y enviar a Pi `{"type": "extension_ui_response", "confirmed": false}`.
- **EC-004: Desconexión de Red Durante Streaming SSE**
  - Si la conexión HTTP SSE entre el cliente Avalonia y la API se interrumpe, `HarnessSseClient` debe reintentar la conexión con retroceso exponencial (*exponential backoff*) y reanudar el estado consultando el último `SequenceNumber` recibido.
- **EC-005: Entrada JSONL Inválida o Malformada desde Pi**
  - Si el subproceso `pi` emite una línea de texto que no cumple con el esquema JSON esperado, el lector asíncrono no debe fallar; debe aislar la línea en el log de diagnóstico y continuar procesando los siguientes eventos válidos.
- **EC-006: Concurrencia de Edición en Sesión (`xmin` Stale Data)**
  - Si dos agentes o peticiones intentan modificar simultáneamente los atributos de `AgentSession`, EF Core 10 capturará `DbUpdateConcurrencyException` mediante la columna `xmin` de PostgreSQL, aplicando reintento o notificando al usuario.
