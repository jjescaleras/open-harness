# Tareas de Implementación: Dashboard como Ventana Principal (Feature 002)

- [x] **T6.1: Renombrar MainWindow a MainChatWindow**
  - **Dependencias**: `T5.2`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Views/MainChatWindow.axaml`, `src/Client/OpenHarness.UI.Avalonia/Views/MainChatWindow.axaml.cs`
  - **Trazabilidad**: `FR-011`
  - **Criterio de Aceptación**: La ventana de chat conserva `HarnessShellView` y `HarnessShellViewModel` bajo el nuevo nombre; no quedan referencias a la clase anterior para el chat.

- [x] **T6.2: MainWindowViewModel y KPIs de Ejemplo**
  - **Dependencias**: `T6.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/ViewModels/MainWindowViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/ViewModels/DashboardKpiViewModel.cs`
  - **Trazabilidad**: `FR-012`, `FR-013`, `EC-009`
  - **Criterio de Aceptación**: `MainViewModel` renombrado a `MainWindowViewModel` con 4 KPIs, sesiones recientes de ejemplo, `IsSampleData` y `OpenChatCommand`.

- [x] **T6.3: Servicio de Apertura del Chat**
  - **Dependencias**: `T6.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Services/IChatWindowService.cs`, `src/Client/OpenHarness.UI.Avalonia/Services/ChatWindowService.cs`
  - **Trazabilidad**: `FR-014`, `SC-008`, `EC-007`
  - **Criterio de Aceptación**: Abre `MainChatWindow` como ventana independiente, reutiliza la instancia abierta y la libera al cerrarse.

- [x] **T6.4: Vista Dashboard y Arranque**
  - **Dependencias**: `T6.2`, `T6.3`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Views/MainWindow.axaml`, `src/Client/OpenHarness.UI.Avalonia/App.axaml.cs`
  - **Trazabilidad**: `FR-012`, `FR-013`, `FR-014`, `SC-007`
  - **Criterio de Aceptación**: La aplicación arranca en el Dashboard construido con tokens Armonia; el botón "Open Chat" (`Ctrl+K`) abre el chat.

- [x] **T6.5: Pruebas Unitarias del Dashboard**
  - **Dependencias**: `T6.2`, `T6.3`
  - **Archivos Objetivo**: `tests/OpenHarness.UnitTests/UI/MainWindowViewModelTests.cs`
  - **Trazabilidad**: `FR-013`, `FR-014`, `SC-009`, `EC-009`
  - **Criterio de Aceptación**: Pruebas de carga de datos de ejemplo, delegación de `OpenChatCommand`, ausencia de servicio y colores de acento por `KpiKind`.

- [x] **T6.6: Panel de Menús Ocultable (Derecha)**
  - **Dependencias**: `T6.4`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/ViewModels/NavigationItemViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/ViewModels/MainWindowViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/Views/MainWindow.axaml`
  - **Trazabilidad**: `FR-015`, `SC-010`, `EC-010`
  - **Criterio de Aceptación**: Menú visible por defecto con Dashboard, Chat, Sessions y Settings; se oculta con `☰`/`Ctrl+B`; Chat abre `MainChatWindow`.

- [x] **T6.7: Panel Multiusos Oculto por Defecto (Izquierda)**
  - **Dependencias**: `T6.4`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/ViewModels/MainWindowViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/Views/MainWindow.axaml`
  - **Trazabilidad**: `FR-016`, `SC-010`
  - **Criterio de Aceptación**: Panel oculto al iniciar; se muestra/oculta con `◧`/`Ctrl+J` y se cierra con `✕`; contenido intercambiable vía `AuxPanelContent`.
