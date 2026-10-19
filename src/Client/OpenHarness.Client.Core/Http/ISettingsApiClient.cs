using OpenHarness.Client.Core.Models;

namespace OpenHarness.Client.Core.Http;

public interface ISettingsApiClient
{
    Task<ClientGeneralSettingsResponse> GetGeneralSettingsAsync(CancellationToken cancellationToken = default);

    /// <param name="expectedVersion">Version returned by the last read; a mismatch yields <see cref="ClientSettingsSaveStatus.Conflict"/>.</param>
    Task<ClientSettingsSaveResult> UpdateGeneralSettingsAsync(
        ClientGeneralSettings settings,
        uint expectedVersion,
        CancellationToken cancellationToken = default);
}
