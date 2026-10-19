using FluentAssertions;
using OpenHarness.Client.Core.Http;
using OpenHarness.Client.Core.Models;
using OpenHarness.UI.Avalonia.ViewModels;
using Xunit;

namespace OpenHarness.UnitTests.Settings;

public class SettingsGeneralViewModelTests
{
    private static readonly ClientGeneralSettings ServerSettings = new(
        "ReadOnly", "es", "Dark", 16, "Compact", "Always", false, "Interrupt", "Hidden", true);

    private sealed class FakeSettingsApiClient : ISettingsApiClient
    {
        public ClientGeneralSettingsResponse Stored { get; set; } = new(ServerSettings, 5, DateTimeOffset.UtcNow);
        public Func<ClientGeneralSettings, uint, ClientSettingsSaveResult>? OnSave { get; set; }
        public List<(ClientGeneralSettings Settings, uint Version)> Saves { get; } = [];

        public Task<ClientGeneralSettingsResponse> GetGeneralSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored);

        public Task<ClientSettingsSaveResult> UpdateGeneralSettingsAsync(
            ClientGeneralSettings settings, uint expectedVersion, CancellationToken cancellationToken = default)
        {
            Saves.Add((settings, expectedVersion));
            var result = OnSave?.Invoke(settings, expectedVersion)
                ?? new ClientSettingsSaveResult(
                    ClientSettingsSaveStatus.Saved,
                    Stored = new ClientGeneralSettingsResponse(settings, Stored.Version + 1, DateTimeOffset.UtcNow));
            return Task.FromResult(result);
        }
    }

    private static async Task<(SettingsGeneralViewModel Vm, FakeSettingsApiClient Api)> LoadedAsync(bool canEdit = true)
    {
        var api = new FakeSettingsApiClient();
        var vm = new SettingsGeneralViewModel(api, canEdit);
        await vm.ReloadAsync();
        return (vm, api);
    }

    [Fact]
    public void WithoutApiClient_ShowsDefaultsOfflineAndCannotSave()
    {
        var vm = new SettingsGeneralViewModel();

        vm.IsOffline.Should().BeTrue();
        vm.StatusKind.Should().Be(SettingsStatusKind.Warning);
        vm.SelectedPermission.Value.Should().Be("WorkspaceWrite");
        vm.SelectedAppearance.Value.Should().Be("System");
        vm.FontSizePx.Should().Be(14);

        vm.SelectedAppearance = vm.AppearanceOptions.Single(o => o.Value == "Dark");

        vm.IsDirty.Should().BeTrue();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Reload_AppliesServerValuesAndIsNotDirty()
    {
        var (vm, _) = await LoadedAsync();

        vm.Snapshot().Should().Be(ServerSettings);
        vm.SelectedAppearance.Label.Should().Be("Oscuro");
        vm.IsDirty.Should().BeFalse();
        vm.HasStatus.Should().BeFalse();
    }

    [Fact]
    public async Task Editing_MarksDirty_AndDiscardRestoresServerValues()
    {
        var (vm, _) = await LoadedAsync();

        vm.FontSizePx = 20;
        vm.ShowCodingView = true;

        vm.IsDirty.Should().BeTrue();
        vm.StatusKind.Should().Be(SettingsStatusKind.Info);
        vm.SaveCommand.CanExecute(null).Should().BeTrue();

        vm.DiscardCommand.Execute(null);

        vm.Snapshot().Should().Be(ServerSettings);
        vm.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task Save_SendsSnapshotWithVersion_AndTracksNewVersion()
    {
        var (vm, api) = await LoadedAsync();

        vm.FontSizePx = 18;
        await vm.SaveAsync();
        vm.UploadSessionLog = false;
        await vm.SaveAsync();

        api.Saves.Should().HaveCount(2);
        api.Saves[0].Version.Should().Be(5);
        api.Saves[0].Settings.ConversationFontSizePx.Should().Be(18);
        api.Saves[1].Version.Should().Be(6);
        vm.IsDirty.Should().BeFalse();
        vm.StatusKind.Should().Be(SettingsStatusKind.Success);
    }

    [Fact]
    public async Task Save_OnConflict_KeepsEditsAndWarns()
    {
        var (vm, api) = await LoadedAsync();
        api.OnSave = (_, _) => new ClientSettingsSaveResult(ClientSettingsSaveStatus.Conflict);

        vm.FontSizePx = 22;
        await vm.SaveAsync();

        vm.IsDirty.Should().BeTrue();
        vm.FontSizePx.Should().Be(22);
        vm.StatusKind.Should().Be(SettingsStatusKind.Warning);
        vm.StatusMessage.Should().Contain("Otro administrador");
    }

    [Fact]
    public async Task Save_OnNetworkError_ReportsError()
    {
        var (vm, api) = await LoadedAsync();
        api.OnSave = (_, _) => throw new HttpRequestException("down");

        vm.FontSizePx = 12;
        await vm.SaveAsync();

        vm.StatusKind.Should().Be(SettingsStatusKind.Error);
        vm.IsBusy.Should().BeFalse();
        vm.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task ReadOnlyUser_CannotSave()
    {
        var (vm, _) = await LoadedAsync(canEdit: false);

        vm.FontSizePx = 12;

        vm.IsReadOnly.Should().BeTrue();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownServerValue_FallsBackToFirstOption()
    {
        var api = new FakeSettingsApiClient
        {
            Stored = new(ServerSettings with { WorkDetails = "FromTheFuture" }, 1, DateTimeOffset.UtcNow)
        };
        var vm = new SettingsGeneralViewModel(api);

        await vm.ReloadAsync();

        vm.SelectedWorkDetails.Should().BeSameAs(vm.WorkDetailsOptions[0]);
    }
}
