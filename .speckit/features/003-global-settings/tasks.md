# Tareas de Implementación: Configuración Global (Feature 003)

- [x] **T7.1: Modelo de Dominio de Configuración Global**
  - **Dependencias**: `T1.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Domain/Entities/GlobalSettings.cs`, `src/Backend/OpenHarness.Domain/ValueObjects/GeneralSettings.cs`, `src/Backend/OpenHarness.Domain/Enums/*.cs`, `src/Backend/OpenHarness.Domain/Common/Roles.cs`
  - **Trazabilidad**: `FR-017`
  - **Criterio de Aceptación**: Entidad con `Guid.CreateVersion7()`, `scope`, `GeneralSettings`, autor y `RowVersion`.

- [x] **T7.2: Mapeo EF Core y Migración `AddGlobalSettings`**
  - **Dependencias**: `T7.1`, `T1.4`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Infrastructure/Persistence/Configurations/GlobalSettingsConfiguration.cs`, `src/Backend/OpenHarness.Infrastructure/Persistence/Migrations/*_AddGlobalSettings.cs`
  - **Trazabilidad**: `FR-017`, `EC-012`
  - **Criterio de Aceptación**: Tabla `harness.global_settings` con `jsonb`, índice único en `scope`, `xmin` y FK `ON DELETE SET NULL`; migración aplicada sobre PostgreSQL real.

- [x] **T7.3: Servicio y Validación de Configuración**
  - **Dependencias**: `T7.2`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Application/Features/Settings/*.cs`
  - **Trazabilidad**: `FR-018`, `SC-011`, `SC-013`, `EC-011`, `EC-012`
  - **Criterio de Aceptación**: GET con valores predeterminados sin escribir; PUT con validación, `expectedVersion` y conversión de conflictos a `SettingsConflict`.

- [x] **T7.4: Endpoints y Política `AdminOnly`**
  - **Dependencias**: `T7.3`
  - **Archivos Objetivo**: `src/Backend/OpenHarness.Api/Endpoints/SettingsEndpoints.cs`, `src/Backend/OpenHarness.Api/Program.cs`
  - **Trazabilidad**: `FR-018`, `SC-012`
  - **Criterio de Aceptación**: 401 sin token, 403 sin rol `admin`, 400 con valores inválidos, 409 con versión desactualizada.

- [x] **T7.5: Cliente de Configuración en Client.Core**
  - **Dependencias**: `T7.4`
  - **Archivos Objetivo**: `src/Client/OpenHarness.Client.Core/Http/ISettingsApiClient.cs`, `src/Client/OpenHarness.Client.Core/Http/HarnessApiClient.cs`, `src/Client/OpenHarness.Client.Core/Models/SettingsModels.cs`
  - **Trazabilidad**: `FR-019`
  - **Criterio de Aceptación**: Lectura y guardado contra la API real, con mapeo de 200/409/403/400.

- [x] **T7.6: Infraestructura de Internacionalización**
  - **Dependencias**: `T5.1`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/Localization/*`, `src/Client/OpenHarness.UI.Avalonia/OpenHarness.UI.Avalonia.csproj`
  - **Trazabilidad**: `FR-022`, `SC-014`, `US-009`
  - **Criterio de Aceptación**: `Strings.resx` en español con clase tipada generada en el build, `Localizer` observable y `{i18n:Tr}`; Dashboard y menú traducidos.

- [x] **T7.7: Menú con Submenús Anidados**
  - **Dependencias**: `T6.6`, `T7.6`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/ViewModels/NavigationItemViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/ViewModels/MainWindowViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/Views/MainWindow.axaml`
  - **Trazabilidad**: `FR-020`
  - **Criterio de Aceptación**: Configuración (solo admin) con General, Conexiones y Modelos anidados; expandir/contraer; ruta en el encabezado; páginas resueltas por `ViewLocator`.

- [x] **T7.8: Página Configuración › General**
  - **Dependencias**: `T7.5`, `T7.7`
  - **Archivos Objetivo**: `src/Client/OpenHarness.UI.Avalonia/ViewModels/SettingsGeneralViewModel.cs`, `src/Client/OpenHarness.UI.Avalonia/Views/SettingsGeneralView.axaml`
  - **Trazabilidad**: `FR-021`, `EC-013`, `EC-014`, `US-008`
  - **Criterio de Aceptación**: Todas las opciones de la referencia, detección de cambios, Guardar/Descartar/Recargar y estados.

- [x] **T7.9: Pruebas Automatizadas**
  - **Dependencias**: `T7.3`, `T7.8`
  - **Archivos Objetivo**: `tests/OpenHarness.UnitTests/Settings/*.cs`, `tests/OpenHarness.UnitTests/UI/LocalizerTests.cs`, `tests/OpenHarness.UnitTests/UI/MainWindowViewModelTests.cs`
  - **Trazabilidad**: `FR-017`–`FR-022`, `SC-011`, `SC-013`, `SC-014`
  - **Criterio de Aceptación**: Validación, servicio (EF InMemory), ViewModel de General, navegación y cobertura de claves i18n.
