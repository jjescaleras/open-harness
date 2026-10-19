using OpenHarness.Application.Common.Models;

namespace OpenHarness.Application.Features.Settings;

public interface ISettingsService
{
    Task<GeneralSettingsResponse> GetGeneralAsync(CancellationToken cancellationToken = default);

    Task<Result<GeneralSettingsResponse>> UpdateGeneralAsync(
        UpdateGeneralSettingsRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default);
}
