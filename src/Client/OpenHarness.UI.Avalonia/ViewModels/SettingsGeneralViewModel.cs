using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenHarness.Client.Core.Http;
using OpenHarness.Client.Core.Models;
using OpenHarness.UI.Avalonia.Localization;

namespace OpenHarness.UI.Avalonia.ViewModels;

public enum SettingsStatusKind
{
    None,
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// "Configuración › General" page. Edits a local copy of the global settings and persists it
/// through <see cref="ISettingsApiClient"/>; only administrators can save.
/// </summary>
public partial class SettingsGeneralViewModel : ViewModelBase
{
    public const int MinFontSizePx = 10;
    public const int MaxFontSizePx = 24;

    private static readonly ClientGeneralSettings Defaults = new(
        DefaultPermissionMode: "WorkspaceWrite",
        Language: Localizer.DefaultLanguage,
        Appearance: "System",
        ConversationFontSizePx: 14,
        WorkDetails: "Detailed",
        CollapseWorkDetails: "OnCompletion",
        ShowCodingView: true,
        SendBehaviorWhileBusy: "Queue",
        PerformanceAndUsage: "Detailed",
        UploadSessionLog: false);

    private readonly ISettingsApiClient? _settingsApiClient;
    private ClientGeneralSettings _savedSnapshot = Defaults;
    private uint _version;
    private bool _isApplying;

    public SettingsGeneralViewModel(ISettingsApiClient? settingsApiClient = null, bool canEdit = true)
    {
        _settingsApiClient = settingsApiClient;
        CanEdit = canEdit;
        AppVersion = ResolveAppVersion();
        Apply(Defaults);

        if (_settingsApiClient is null)
        {
            SetStatus(SettingsStatusKind.Warning, "Settings_Status_Offline");
        }
    }

    public IReadOnlyList<LocalizedOption> PermissionOptions { get; } =
    [
        new("ReadOnly", "Settings_General_Permission_ReadOnly"),
        new("WorkspaceWrite", "Settings_General_Permission_WorkspaceWrite"),
        new("DangerFullAccess", "Settings_General_Permission_DangerFullAccess")
    ];

    public IReadOnlyList<LocalizedOption> LanguageOptions { get; } =
        Localizer.SupportedLanguages.Select(code => new LocalizedOption(code, $"Settings_General_Language_{code}")).ToList();

    public IReadOnlyList<LocalizedOption> AppearanceOptions { get; } =
    [
        new("Light", "Settings_General_Appearance_Light", "☀"),
        new("Dark", "Settings_General_Appearance_Dark", "☾"),
        new("System", "Settings_General_Appearance_System", "🖥")
    ];

    public IReadOnlyList<LocalizedOption> WorkDetailsOptions { get; } = CreateDetailOptions();

    public IReadOnlyList<LocalizedOption> CollapseOptions { get; } =
    [
        new("Never", "Settings_General_Collapse_Never"),
        new("OnCompletion", "Settings_General_Collapse_OnCompletion"),
        new("Always", "Settings_General_Collapse_Always")
    ];

    public IReadOnlyList<LocalizedOption> SendBehaviorOptions { get; } =
    [
        new("Queue", "Settings_General_SendBehavior_Queue"),
        new("Interrupt", "Settings_General_SendBehavior_Interrupt")
    ];

    public IReadOnlyList<LocalizedOption> PerformanceUsageOptions { get; } = CreateDetailOptions();

    public bool CanEdit { get; }
    public bool IsReadOnly => !CanEdit;
    public bool IsOffline => _settingsApiClient is null;
    public decimal FontSizeMinimum => MinFontSizePx;
    public decimal FontSizeMaximum => MaxFontSizePx;
    public string AppVersion { get; }
    public string VersionText => Localizer.Instance.Format("Settings_General_Version", AppVersion);

    [ObservableProperty] private LocalizedOption _selectedPermission = null!;
    [ObservableProperty] private LocalizedOption _selectedLanguage = null!;
    [ObservableProperty] private LocalizedOption _selectedAppearance = null!;
    [ObservableProperty] private decimal _fontSizePx;
    [ObservableProperty] private LocalizedOption _selectedWorkDetails = null!;
    [ObservableProperty] private LocalizedOption _selectedCollapse = null!;
    [ObservableProperty] private bool _showCodingView;
    [ObservableProperty] private LocalizedOption _selectedSendBehavior = null!;
    [ObservableProperty] private LocalizedOption _selectedPerformanceUsage = null!;
    [ObservableProperty] private bool _uploadSessionLog;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    private bool _isBusy;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private SettingsStatusKind _statusKind;

    public bool HasStatus => StatusKind != SettingsStatusKind.None;

    public string StatusColor => StatusKind switch
    {
        SettingsStatusKind.Success => "#10B981",
        SettingsStatusKind.Warning => "#F59E0B",
        SettingsStatusKind.Error => "#EF4444",
        _ => "#94A3B8"
    };

    [RelayCommand(CanExecute = nameof(CanReload))]
    public async Task ReloadAsync()
    {
        if (_settingsApiClient is null) return;

        IsBusy = true;
        SetStatus(SettingsStatusKind.Info, "Settings_Status_Loading");
        try
        {
            var response = await _settingsApiClient.GetGeneralSettingsAsync();
            AcceptServerState(response);
            SetStatus(SettingsStatusKind.None);
        }
        catch (HttpRequestException)
        {
            SetStatus(SettingsStatusKind.Error, "Settings_Status_LoadFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanReload() => _settingsApiClient is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SaveAsync()
    {
        if (_settingsApiClient is null) return;

        IsBusy = true;
        SetStatus(SettingsStatusKind.Info, "Settings_Status_Saving");
        try
        {
            var result = await _settingsApiClient.UpdateGeneralSettingsAsync(Snapshot(), _version);
            switch (result.Status)
            {
                case ClientSettingsSaveStatus.Saved:
                    AcceptServerState(result.Response!);
                    Localizer.Instance.SetLanguage(_savedSnapshot.Language);
                    SetStatus(SettingsStatusKind.Success, "Settings_Status_Saved");
                    break;
                case ClientSettingsSaveStatus.Conflict:
                    SetStatus(SettingsStatusKind.Warning, "Settings_Status_Conflict");
                    break;
                case ClientSettingsSaveStatus.Forbidden:
                    SetStatus(SettingsStatusKind.Error, "Settings_Status_Forbidden");
                    break;
                default:
                    StatusKind = SettingsStatusKind.Error;
                    StatusMessage = Localizer.Instance.Format("Settings_Status_Invalid", result.ErrorMessage ?? string.Empty);
                    break;
            }
        }
        catch (HttpRequestException)
        {
            SetStatus(SettingsStatusKind.Error, "Settings_Status_SaveFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave() => CanEdit && IsDirty && !IsBusy && _settingsApiClient is not null;

    [RelayCommand(CanExecute = nameof(IsDirty))]
    public void Discard()
    {
        Apply(_savedSnapshot);
        if (!IsOffline) SetStatus(SettingsStatusKind.None);
    }

    /// <summary>Current form values in wire format.</summary>
    public ClientGeneralSettings Snapshot() => new(
        SelectedPermission.Value,
        SelectedLanguage.Value,
        SelectedAppearance.Value,
        (int)FontSizePx,
        SelectedWorkDetails.Value,
        SelectedCollapse.Value,
        ShowCodingView,
        SelectedSendBehavior.Value,
        SelectedPerformanceUsage.Value,
        UploadSessionLog);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(StatusKind):
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(StatusColor));
                return;
            case nameof(IsDirty) or nameof(IsBusy) or nameof(StatusMessage) or nameof(HasStatus) or nameof(StatusColor):
                return;
        }

        if (_isApplying) return;

        IsDirty = Snapshot() != _savedSnapshot;
        if (IsDirty && !IsOffline && StatusKind is SettingsStatusKind.None or SettingsStatusKind.Success)
        {
            SetStatus(SettingsStatusKind.Info, "Settings_Status_Unsaved");
        }
        else if (!IsDirty && StatusKind == SettingsStatusKind.Info)
        {
            SetStatus(SettingsStatusKind.None);
        }
    }

    private void AcceptServerState(ClientGeneralSettingsResponse response)
    {
        _version = response.Version;
        _savedSnapshot = response.Settings;
        Apply(response.Settings);
    }

    private void Apply(ClientGeneralSettings s)
    {
        _isApplying = true;
        try
        {
            SelectedPermission = Pick(PermissionOptions, s.DefaultPermissionMode);
            SelectedLanguage = Pick(LanguageOptions, s.Language);
            SelectedAppearance = Pick(AppearanceOptions, s.Appearance);
            FontSizePx = Math.Clamp(s.ConversationFontSizePx, MinFontSizePx, MaxFontSizePx);
            SelectedWorkDetails = Pick(WorkDetailsOptions, s.WorkDetails);
            SelectedCollapse = Pick(CollapseOptions, s.CollapseWorkDetails);
            ShowCodingView = s.ShowCodingView;
            SelectedSendBehavior = Pick(SendBehaviorOptions, s.SendBehaviorWhileBusy);
            SelectedPerformanceUsage = Pick(PerformanceUsageOptions, s.PerformanceAndUsage);
            UploadSessionLog = s.UploadSessionLog;
        }
        finally
        {
            _isApplying = false;
        }

        IsDirty = Snapshot() != _savedSnapshot;
    }

    private void SetStatus(SettingsStatusKind kind, string? messageKey = null)
    {
        StatusKind = kind;
        StatusMessage = messageKey is null ? string.Empty : Localizer.Instance[messageKey];
    }

    // Unknown server values (e.g. from a newer API) fall back to the first option instead of crashing the form.
    private static LocalizedOption Pick(IReadOnlyList<LocalizedOption> options, string value) =>
        options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)) ?? options[0];

    private static List<LocalizedOption> CreateDetailOptions() =>
    [
        new("Hidden", "Settings_General_Detail_Hidden"),
        new("Compact", "Settings_General_Detail_Compact"),
        new("Detailed", "Settings_General_Detail_Detailed")
    ];

    private static string ResolveAppVersion()
    {
        var info = typeof(SettingsGeneralViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }
}
