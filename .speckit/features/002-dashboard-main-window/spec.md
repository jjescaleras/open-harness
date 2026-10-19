# Especificación de Requerimientos: Dashboard como Ventana Principal (Feature 002)

## 1. Resumen Ejecutivo
Hasta la Feature 001, la ventana de arranque de `OpenHarness.UI.Avalonia` (`MainWindow`) alojaba directamente el espacio de trabajo conversacional de tres paneles (`HarnessShellView`). Esta feature prepara la UI para que la ventana principal sea un **Dashboard**: el espacio de chat se renombra a **`MainChatWindow`** y se crea un nuevo **`MainWindow`** que actúa como punto de entrada con indicadores (KPIs) y sesiones recientes, desde el cual se abre el chat en una ventana aparte.

En esta entrega el Dashboard muestra **datos de ejemplo**; la conexión con la API (`ISessionsApiClient`) queda fuera de alcance y se abordará en una feature posterior.

---

## 2. Historias de Usuario (User Stories)

### US-006: Vista General al Iniciar la Aplicación
- **Como** operador o desarrollador,
- **Quiero** que al abrir Open Harness se muestre un Dashboard con el estado general (sesiones activas, aprobaciones pendientes, consumo de tokens y ejecuciones de herramientas),
- **Para** entender de un vistazo la actividad de la plataforma antes de entrar a una sesión concreta.

### US-007: Acceso Directo al Espacio de Chat
- **Como** usuario del agente,
- **Quiero** abrir el espacio conversacional desde el Dashboard con un botón o atajo de teclado,
- **Para** trabajar con el agente en una ventana dedicada sin perder la vista general.

---

## 3. Requisitos Funcionales (Functional Requirements)

- **FR-011**: La ventana que aloja `HarnessShellView` debe renombrarse de `MainWindow` a **`MainChatWindow`** (`Views/MainChatWindow.axaml` y su code-behind), conservando su `DataContext` `HarnessShellViewModel` y su comportamiento actual.
- **FR-012**: Debe existir un nuevo **`MainWindow`** (`Views/MainWindow.axaml`) configurado como `desktop.MainWindow` en `App.axaml.cs`, enlazado al ViewModel **`MainWindowViewModel`** (renombrado del sobrante de plantilla `MainViewModel`) y construido solo con recursos de `ArmoniaDesignTokens.axaml`.
- **FR-013**: El Dashboard debe mostrar una fila de tarjetas KPI (`DashboardKpiViewModel`: título, valor, leyenda y tipo `KpiKind` con color de acento alineado con los tokens Armonia) y una lista de sesiones recientes (`SessionItemViewModel`), alimentadas con datos de ejemplo e indicando visiblemente que son datos de ejemplo (`IsSampleData`).
- **FR-014**: El comando `OpenChatCommand` de `MainWindowViewModel` debe abrir `MainChatWindow` como **ventana independiente** a través de la abstracción `IChatWindowService`, sin referenciar tipos de ventana desde el ViewModel. Si el chat ya está abierto, se activa en lugar de crear un duplicado. El botón admite el atajo `Ctrl+K`.

---

## 4. Criterios de Éxito Medibles (Success Criteria)

- **SC-007**: Al iniciar la aplicación se muestra el Dashboard (`MainWindow`) y no el chat.
- **SC-008**: Pulsar "Open Chat" (o `Ctrl+K`) abre exactamente una instancia de `MainChatWindow`; pulsaciones repetidas no crean ventanas adicionales.
- **SC-009**: `dotnet build -c Release` compila sin advertencias y las pruebas de `MainWindowViewModel` pasan sin necesidad de un entorno gráfico (servicio de ventanas sustituible por un doble de prueba).

---

## 5. Casos Límite (Edge Cases)

### EC-007: Cierre del Chat y Reapertura
- **Escenario**: El usuario cierra `MainChatWindow` y vuelve a pulsar "Open Chat".
- **Manejo**: `ChatWindowService` libera su referencia en el evento `Closed` y crea una ventana nueva con un `HarnessShellViewModel` limpio.

### EC-008: Cierre del Dashboard con el Chat Abierto
- **Escenario**: El usuario cierra el Dashboard mientras `MainChatWindow` sigue abierta.
- **Manejo**: Con el `ShutdownMode` por defecto (`OnLastWindowClose`) la aplicación sigue en ejecución hasta cerrar la última ventana.

### EC-009: ViewModel sin Servicio de Ventanas
- **Escenario**: `MainWindowViewModel` se instancia sin `IChatWindowService` (diseñador XAML o pruebas).
- **Manejo**: `OpenChatCommand` no hace nada y no lanza excepciones.
