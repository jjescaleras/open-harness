# Lista de Verificación de Calidad: Configuración Global (Feature 003)

## 1. Persistencia
- [x] La configuración vive en PostgreSQL (`harness.global_settings`), no en archivos locales.
- [x] Una sola fila global garantizada por índice único en `scope`.
- [x] `general` es `jsonb` mapeado con `.ToJson()`; enums guardados como texto legible.
- [x] Concurrencia optimista con `xmin` y autor del último cambio registrado.
- [x] La migración `AddGlobalSettings` solo crea la tabla y se aplicó limpiamente sobre PostgreSQL real.

## 2. Seguridad
- [x] Lectura solo con usuario autenticado (401 sin token).
- [x] Escritura solo con rol `admin` mediante la política `AdminOnly` (403 para otros roles).
- [x] La UI oculta Configuración a quien no es administrador y deshabilita la edición si `CanEdit` es falso.
- [ ] El registro impide autoasignarse el rol `admin` (EC-015, pendiente fuera de esta feature).
- [ ] API keys cifradas con AES-256-GCM y clave maestra externa (FR-023, feature de Conexiones).

## 3. API y Cliente
- [x] Valores inválidos responden 400 con los campos afectados.
- [x] `expectedVersion` desactualizada responde 409 sin sobrescribir.
- [x] `HarnessApiClient` verificado contra la API real (Saved / Conflict / Forbidden / Invalid).

## 4. UI
- [x] Configuración aparece con submenús anidados General, Conexiones y Modelos; se expande y contrae.
- [x] El encabezado muestra la ruta "Configuración / General".
- [x] La página General contiene todas las opciones de la referencia y la versión actual.
- [x] Cambios sin guardar detectados; Guardar (`Ctrl+S`), Descartar y Recargar funcionan.
- [x] Aviso visible en modo sin conexión.
- [x] Los controles usan el tema oscuro para coincidir con los tokens Armonia.

## 5. Internacionalización
- [x] Ningún texto visible del Dashboard o de Configuración está fijo en XAML o ViewModels.
- [x] Español como idioma neutro en `Strings.resx`; la clase tipada se genera en el build de CLI.
- [x] Prueba que verifica que toda clave `{i18n:Tr}` existe en los recursos.

## 6. Calidad
- [x] `dotnet build OpenHarness.slnx -c Release` sin advertencias.
- [x] Pruebas unitarias en verde.
- [x] Verificación manual bajo Xvfb del menú, la página General y Conexiones.
