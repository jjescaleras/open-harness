# Plan Técnico de Implementación: Dashboard como Ventana Principal (Feature 002)

## 1. Arquitectura de Ventanas

```
App.OnFrameworkInitializationCompleted
        |
        v
+-------------------------------------------+        OpenChatCommand        +------------------------------------+
| MainWindow (Dashboard)                    | ----------------------------> | IChatWindowService                 |
|  DataContext: MainWindowViewModel         |                               |  ChatWindowService (UI.Avalonia)   |
|  - Kpis: DashboardKpiViewModel[]          |                               |  - crea / activa MainChatWindow    |
|  - RecentSessions: SessionItemViewModel[] |                               +------------------------------------+
+-------------------------------------------+                                              |
                                                                                           v
                                                         +-----------------------------------------------+
                                                         | MainChatWindow (antes MainWindow)             |
                                                         |  DataContext: HarnessShellViewModel           |
                                                         |  Contenido: HarnessShellView (3 paneles)      |
                                                         +-----------------------------------------------+
```

## 2. Cambios por Archivo

| Archivo | Cambio |
|---|---|
| `Views/MainWindow.axaml(.cs)` → `Views/MainChatWindow.axaml(.cs)` | Renombrado (`git mv`) y actualización de `x:Class` y nombre de clase. |
| `ViewModels/MainViewModel.cs` → `ViewModels/MainWindowViewModel.cs` | Renombrado; ahora contiene KPIs, sesiones recientes de ejemplo y `OpenChatCommand`. |
| `ViewModels/DashboardKpiViewModel.cs` | Nuevo: tarjeta KPI con `KpiKind` y `AccentColor`. |
| `Services/IChatWindowService.cs`, `Services/ChatWindowService.cs` | Nuevos: abren o activan `MainChatWindow`, manteniendo los ViewModels libres de tipos de ventana (Constitución, Art. V.1). |
| `Views/MainWindow.axaml(.cs)` | Nuevo Dashboard: encabezado con botón "Open Chat" (`Ctrl+K`), aviso de datos de ejemplo, grid de 4 KPIs y lista de sesiones recientes. |
| `App.axaml.cs` | `desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel(new ChatWindowService()) }`. |
| `ViewModels/NavigationItemViewModel.cs` | Nuevo: opción de menú (`NavigationTarget`, título, icono, `IsAvailable`, `IsSelected`). |
| `ViewModels/MainWindowViewModel.cs` (paneles) | `MenuItems`, `SelectedMenuItem`, `NavigateCommand`, `IsMenuPanelVisible` (por defecto `true`), `IsAuxPanelVisible` (por defecto `false`), `AuxPanelTitle`, `AuxPanelContent` y comandos de alternancia. |
| `Views/MainWindow.axaml` (paneles) | Layout `Auto, *, Auto`: panel multiusos (300 px, izquierda), página central y panel de menús (220 px, derecha); estilos `panel-toggle`, `panel-close` y `menu-item` con tokens Armonia. |
| `tests/OpenHarness.UnitTests/UI/MainWindowViewModelTests.cs` | Pruebas con un `IChatWindowService` falso, incluidas visibilidad de paneles y navegación. |

## 3. Decisiones
- **Layout de paneles**: el multiusos a la izquierda y oculto al iniciar; los menús a la derecha y visibles. Ocultar un panel lo quita del layout (`IsVisible`), así el contenido central aprovecha todo el ancho.
- **Panel multiusos genérico**: `AuxPanelContent` acepta cualquier `ViewModelBase`, de modo que futuras features (filtros, detalles, notificaciones) solo tienen que asignar un ViewModel con su vista.

- **Ventana aparte en lugar de navegación en una sola ventana**: respeta la separación pedida (`MainWindow` y `MainChatWindow` como `Window`) y permite tener el Dashboard visible mientras se trabaja en el chat.
- **Servicio de ventanas en lugar de evento en code-behind**: el ViewModel queda testeable sin entorno gráfico y el code-behind sigue sin lógica.
- **Datos de ejemplo explícitos**: `IsSampleData` muestra un aviso para no confundir valores de muestra con métricas reales.

## 4. Trabajo Futuro (fuera de alcance)
- Conectar los KPIs y sesiones recientes a `ISessionsApiClient` y desactivar `IsSampleData`.
- Abrir `MainChatWindow` con una sesión concreta preseleccionada al hacer clic en una sesión reciente.
- Inyección de dependencias (`Microsoft.Extensions.DependencyInjection`) para construir ViewModels y servicios.
