# Plan Técnico de Implementación: Configuración Global (Feature 003)

## 1. Decisión de Persistencia: Base de Datos, no JSON

| Opción | Ventajas | Desventajas |
|---|---|---|
| **PostgreSQL (elegida)** | Única fuente de verdad (Constitución Art. I.3); compartida por todos los clientes; concurrencia `xmin`; auditoría; respaldos junto con el resto de los datos | Requiere migración |
| Archivo JSON | Simple de editar a mano | Segunda fuente de verdad; local a una máquina; sin control de concurrencia ni de permisos; inadecuado para secretos |

Un JSON local solo tendría sentido para preferencias de la propia máquina (tamaño de ventana, paneles abiertos), que no forman parte de esta feature.

## 2. Modelo de Datos

```
harness.global_settings
+----------------------+-----------------------------------------------+
| id                   | uuid (v7, PK)                                 |
| scope                | varchar(64), UNIQUE ('global')                |
| general              | jsonb (.ToJson, enums como texto)             |
| updated_by_user_id   | uuid? FK users(id) ON DELETE SET NULL         |
| xmin                 | xid (concurrencia optimista)                  |
| created_at_utc       | timestamptz                                   |
| updated_at_utc       | timestamptz                                   |
+----------------------+-----------------------------------------------+
```

`GeneralSettings` (value object): `DefaultPermissionMode` (`SandboxMode`), `Language`, `Appearance` (`AppearanceMode`), `ConversationFontSizePx`, `WorkDetails` (`DetailLevel`), `CollapseWorkDetails` (`CollapseWorkDetailsMode`), `ShowCodingView`, `SendBehaviorWhileBusy` (`BusySendBehavior`), `PerformanceAndUsage` (`DetailLevel`), `UploadSessionLog`.

Migración: `AddGlobalSettings` (solo crea la tabla y sus índices).

## 3. API

| Método | Ruta | Autorización | Respuestas |
|---|---|---|---|
| GET | `/api/v1/settings/general` | Autenticado | 200 `{ settings, version, updatedAtUtc }` |
| PUT | `/api/v1/settings/general` | Política `AdminOnly` (rol `admin`) | 200 · 400 `InvalidSettings` · 403 · 409 `SettingsConflict` |

Cuerpo del PUT: `{ settings: GeneralSettingsDto, expectedVersion: uint? }`. Los enums viajan por nombre (`"WorkspaceWrite"`) para que el cliente no dependa del ensamblado de Dominio.

## 4. Cifrado de API Keys (diseño para la feature de Conexiones)

- **Algoritmo**: AES-256-GCM (cifrado autenticado) con nonce aleatorio de 12 bytes por valor; se guardan `ciphertext`, `nonce`, `tag` y `key_version`.
- **Clave maestra**: fuera de la BD (`OPENHARNESS_MASTER_KEY` por variable de entorno, user-secrets o un gestor de secretos). Un respaldo robado de la BD no basta para leer las keys.
- **Rotación**: `key_version` permite descifrar con la clave anterior y recifrar con la nueva.
- **Exposición**: la API devuelve la key solo enmascarada (`sk-…a1b2`); el descifrado ocurre únicamente en el backend al invocar al proveedor.

## 5. Internacionalización

```
Strings.resx (neutro = es) ──► Strings (clase generada por MSBuild, sin Visual Studio)
                                   │
                    Localizer.Instance["Clave"]  ◄── ViewModels (LocalizedOption, NavigationItemViewModel, estados)
                                   │  INotifyPropertyChanged("Item[]") al cambiar de idioma
                    {i18n:Tr Clave}  ◄── XAML (ReflectionBinding OneWay al indexador)
```

- Los ViewModels guardan **claves** (no textos) y exponen el texto traducido como propiedad calculada que se refresca al cambiar el idioma.
- Al guardar la configuración, `Localizer.SetLanguage` aplica el idioma elegido.

## 6. UI

- `NavigationItemViewModel` gana `Children`, `Parent` e `IsExpanded`; el menú dibuja los hijos en un recuadro anidado bajo el padre, con chevron.
- `MainWindowViewModel.CurrentPage` aloja la página activa y `ViewLocator` la resuelve a su vista (`SettingsGeneralView`, `ComingSoonView`). La página General se conserva al navegar para no perder cambios sin guardar.
- `App.axaml` fija `RequestedThemeVariant="Dark"`: los tokens Armonia son una paleta oscura y, con el tema claro del sistema, los controles Fluent quedaban con bajo contraste.

## 7. Archivos

| Capa | Archivos |
|---|---|
| Domain | `Entities/GlobalSettings.cs`, `ValueObjects/GeneralSettings.cs`, `Enums/{AppearanceMode,DetailLevel,CollapseWorkDetailsMode,BusySendBehavior}.cs`, `Common/Roles.cs` |
| Application | `Features/Settings/{SettingsDtos,ISettingsService,SettingsService,GeneralSettingsMapper}.cs`, `IOpenHarnessDbContext` |
| Infrastructure | `Persistence/Configurations/GlobalSettingsConfiguration.cs`, `OpenHarnessDbContext`, migración `AddGlobalSettings` |
| Api | `Endpoints/SettingsEndpoints.cs`, política `AdminOnly` en `Program.cs` |
| Client.Core | `Http/ISettingsApiClient.cs`, `HarnessApiClient`, `Models/SettingsModels.cs`, `Authentication/ClientRoles.cs` |
| UI | `Localization/{Strings.resx,Localizer,TrExtension}.cs`, `ViewModels/{SettingsGeneral,ComingSoon,NavigationItem,MainWindow}ViewModel.cs`, `ViewModels/LocalizedOption.cs`, `Views/{SettingsGeneralView,ComingSoonView,MainWindow}.axaml`, `App.axaml` |
| Tests | `Settings/{GeneralSettingsMapper,SettingsService,SettingsGeneralViewModel}Tests.cs`, `UI/{MainWindowViewModel,Localizer}Tests.cs` |

## 8. Pendientes (fuera de alcance)
- Submenús **Conexiones** (endpoints + API keys cifradas, FR-023) y **Modelos**.
- Conectar la UI a la API: requiere la pantalla de inicio de sesión, que aún no existe; hasta entonces la página General funciona en modo sin conexión.
- Aplicar en vivo las preferencias guardadas (apariencia, tamaño de fuente en el chat, etc.).
- Traducir con i18n la ventana de chat (`MainChatWindow`).
- Corregir EC-015 (registro con rol `admin`).
