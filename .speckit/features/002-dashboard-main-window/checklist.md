# Lista de Verificación de Calidad: Dashboard como Ventana Principal (Feature 002)

## 1. Estructura de Ventanas
- [x] `MainChatWindow` aloja `HarnessShellView` con `HarnessShellViewModel` y mantiene el comportamiento de la Feature 001.
- [x] `MainWindow` es la ventana de arranque (`desktop.MainWindow`) y muestra el Dashboard.
- [x] El ViewModel sobrante de plantilla `MainViewModel` ya no existe; su sucesor es `MainWindowViewModel`.

## 2. MVVM y Diseño
- [x] Los ViewModels usan `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) y no referencian tipos de ventana.
- [x] El code-behind de `MainWindow` y `MainChatWindow` solo llama a `InitializeComponent()`.
- [x] El Dashboard usa exclusivamente recursos de `ArmoniaDesignTokens.axaml` (colores de acento KPI alineados con los tokens).
- [x] Se indica visiblemente que los datos mostrados son de ejemplo.

## 3. Comportamiento
- [x] "Open Chat" y `Ctrl+K` abren `MainChatWindow` como ventana independiente.
- [x] Pulsaciones repetidas activan la ventana de chat existente en vez de duplicarla.
- [x] Al cerrar el chat, una nueva pulsación abre una ventana nueva.

## 4. Calidad
- [x] `dotnet build OpenHarness.slnx -c Release` compila sin advertencias.
- [x] Las pruebas de `MainWindowViewModelTests` pasan sin entorno gráfico.
- [x] Verificación manual bajo Xvfb: el Dashboard se renderiza y el botón abre la ventana de chat.
