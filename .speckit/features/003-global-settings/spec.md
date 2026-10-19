# Especificación de Requerimientos: Configuración Global (Feature 003)

## 1. Resumen Ejecutivo
Open Harness necesita una **configuración global de la instalación** que persista en el servidor, empezando por las preferencias generales y preparando el terreno para registrar los endpoints y API keys de los proveedores de modelos. Esta feature agrega:

- Persistencia de la configuración global en **PostgreSQL** (única fuente de verdad, Constitución Art. I.3), editable **solo por administradores**.
- Endpoints REST para leer y actualizar la configuración, con control de concurrencia optimista.
- Una sección **Configuración** en el menú del Dashboard con submenús anidados (**General**, **Conexiones**, **Modelos**) y la página **General** completa.
- Infraestructura de **internacionalización (i18n)** en la UI, con español como idioma neutro.

Los submenús **Conexiones** y **Modelos** quedan como "Pronto"; Conexiones (endpoints y API keys cifradas) es la siguiente feature.

---

## 2. Historias de Usuario

### US-008: Administración de la Configuración Global
- **Como** administrador de la instalación,
- **Quiero** ajustar desde la aplicación las preferencias generales (permisos por defecto, idioma, apariencia, tipografía y comportamiento del agente),
- **Para** que todos los clientes compartan la misma configuración persistida en el servidor.

### US-009: Interfaz en el Idioma del Usuario
- **Como** usuario de habla hispana,
- **Quiero** que la interfaz se muestre en español y que en el futuro pueda cambiarse de idioma,
- **Para** trabajar en mi idioma sin depender de textos fijos en el código.

---

## 3. Requisitos Funcionales

- **FR-017**: La configuración global se persiste en la tabla `harness.global_settings` como **una única fila** identificada por `scope = 'global'` (índice único). Las preferencias generales se guardan en la columna `general` (`jsonb`, mapeada con `.ToJson()` y enums como texto), con concurrencia optimista por `xmin`, auditoría (`created_at_utc`, `updated_at_utc`) y el autor del último cambio (`updated_by_user_id`, FK a `users` con `ON DELETE SET NULL`).
- **FR-018**: La API expone `GET /api/v1/settings/general` para cualquier usuario autenticado y `PUT /api/v1/settings/general` **solo para el rol `admin`** (política `AdminOnly`). Si no hay fila guardada, el GET devuelve los valores predeterminados **sin escribir**. El PUT valida los valores (enums conocidos por nombre, idioma soportado y tamaño de fuente entre 10 y 24 px) y acepta `expectedVersion`: si no coincide con la versión guardada responde **409 Conflict** en lugar de sobrescribir.
- **FR-019**: `OpenHarness.Client.Core` ofrece `ISettingsApiClient` (implementado por `HarnessApiClient`) con `GetGeneralSettingsAsync` y `UpdateGeneralSettingsAsync`, que traduce 200/409/403/400 a `ClientSettingsSaveStatus` (`Saved`, `Conflict`, `Forbidden`, `Invalid`).
- **FR-020**: El menú del Dashboard incluye **Configuración** (solo visible para administradores) con submenús anidados **General**, **Conexiones** y **Modelos**. Al elegir Configuración se despliega el submenú y se abre General; volver a elegirla lo contrae. El encabezado muestra la ruta ("Configuración / General"). Conexiones y Modelos muestran "Pronto" y una pantalla provisional.
- **FR-021**: La página **General** muestra, en filas con título y descripción: Permisos, Idioma, Apariencia (Claro/Oscuro/Sistema), Tamaño de fuente, Detalle del trabajo, Cuándo contraer el detalle del trabajo, Mostrar vista de código, Atajos de teclado (botón deshabilitado, "Disponible próximamente"), Envío mientras el agente trabaja, Rendimiento y uso, Enviar el registro de sesión y la Versión actual. Detecta cambios sin guardar y ofrece **Guardar cambios** (`Ctrl+S`), **Descartar** y **Recargar**, con mensajes de estado para guardado, conflicto, permiso denegado, valores inválidos y errores de conexión.
- **FR-022**: Todos los textos del Dashboard y de Configuración se obtienen de recursos `.resx` (`Localization/Strings.resx`, idioma neutro **español**) mediante `Localizer` (observable, permite cambiar de idioma en caliente) y la extensión XAML `{i18n:Tr Clave}`. Agregar un idioma consiste en añadir `Strings.{código}.resx` y registrarlo en `Localizer.SupportedLanguages` y `GeneralSettingsMapper.SupportedLanguages`.
- **FR-023** *(diseño acordado; se implementa en la feature de Conexiones)*: Las API keys de los proveedores se guardan en la BD **cifradas con AES-256-GCM** usando una clave maestra **fuera de la BD** (variable de entorno o secreto), con versión de clave para rotación. La API nunca devuelve una key en claro, solo enmascarada.

---

## 4. Criterios de Éxito Medibles

- **SC-011**: Un GET sin configuración guardada devuelve los valores predeterminados y la tabla sigue vacía.
- **SC-012**: Un usuario sin rol `admin` recibe **403** en el PUT; sin token, **401** en ambos endpoints.
- **SC-013**: Un PUT con `expectedVersion` desactualizada responde **409** y no modifica la fila.
- **SC-014**: Toda clave `{i18n:Tr …}` usada en los `.axaml` existe en `Strings.resx` (verificado por prueba automatizada).

---

## 5. Casos Límite

### EC-011: Dos Administradores Editan a la Vez
- **Manejo**: El segundo en guardar recibe `SettingsConflict` (409) por `expectedVersion` o por `xmin`; la UI conserva sus cambios y le pide recargar antes de volver a guardar.

### EC-012: Carrera al Crear la Primera Fila
- **Manejo**: Si dos administradores guardan por primera vez simultáneamente, el índice único sobre `scope` rechaza la segunda inserción (SQLSTATE 23505) y se responde `SettingsConflict`.

### EC-013: Cliente sin Conexión con el Servidor
- **Manejo**: La página General muestra los valores predeterminados y el aviso "Sin conexión con el servidor"; Guardar permanece deshabilitado.

### EC-014: Valor Desconocido desde el Servidor
- **Manejo**: Si el servidor devuelve un valor que la UI no conoce (p. ej. de una versión más nueva), el control selecciona la primera opción en lugar de fallar.

### EC-015: Autoasignación del Rol `admin` en el Registro *(riesgo existente)*
- **Escenario**: `POST /api/v1/auth/register` acepta el campo `role`, por lo que cualquiera puede registrarse como `admin` y saltarse la restricción de esta feature.
- **Manejo**: Fuera del alcance de esta feature; queda registrado como riesgo a corregir (el registro debe ignorar `role` o requerir un administrador).
