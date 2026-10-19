using OpenHarness.Domain.Common;
using OpenHarness.Domain.ValueObjects;

namespace OpenHarness.Domain.Entities;

/// <summary>
/// Installation-wide configuration. A single row identified by <see cref="DefaultScope"/>;
/// only administrators may modify it.
/// </summary>
public sealed class GlobalSettings : IAuditableEntity
{
    public const string DefaultScope = "global";

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Scope { get; set; } = DefaultScope;
    public GeneralSettings General { get; set; } = new();
    public Guid? UpdatedByUserId { get; set; }
    public uint RowVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
