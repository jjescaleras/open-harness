using FluentAssertions;
using OpenHarness.Application.Features.Settings;
using OpenHarness.Domain.Enums;
using OpenHarness.Domain.ValueObjects;
using Xunit;

namespace OpenHarness.UnitTests.Settings;

public class GeneralSettingsMapperTests
{
    private static GeneralSettingsDto ValidDto() => GeneralSettingsMapper.ToDto(new GeneralSettings());

    [Fact]
    public void TryToDomain_WithDefaults_RoundTrips()
    {
        var ok = GeneralSettingsMapper.TryToDomain(ValidDto(), out var settings, out var errors);

        ok.Should().BeTrue();
        errors.Should().BeEmpty();
        GeneralSettingsMapper.ToDto(settings).Should().Be(ValidDto());
    }

    [Fact]
    public void TryToDomain_ParsesEnumNamesCaseInsensitively()
    {
        var dto = ValidDto() with { DefaultPermissionMode = "readonly", Appearance = "DARK" };

        GeneralSettingsMapper.TryToDomain(dto, out var settings, out _).Should().BeTrue();

        settings.DefaultPermissionMode.Should().Be(SandboxMode.ReadOnly);
        settings.Appearance.Should().Be(AppearanceMode.Dark);
    }

    [Theory]
    [InlineData("Neon")]
    [InlineData("42")]
    [InlineData("")]
    public void TryToDomain_RejectsUnknownOrNumericEnumValues(string appearance)
    {
        var dto = ValidDto() with { Appearance = appearance };

        GeneralSettingsMapper.TryToDomain(dto, out _, out var errors).Should().BeFalse();
        errors.Should().ContainSingle().Which.Should().Be(nameof(GeneralSettingsDto.Appearance));
    }

    [Theory]
    [InlineData(GeneralSettings.MinFontSizePx - 1)]
    [InlineData(GeneralSettings.MaxFontSizePx + 1)]
    public void TryToDomain_RejectsFontSizeOutOfRange(int fontSize)
    {
        var dto = ValidDto() with { ConversationFontSizePx = fontSize };

        GeneralSettingsMapper.TryToDomain(dto, out _, out var errors).Should().BeFalse();
        errors.Should().Contain(nameof(GeneralSettingsDto.ConversationFontSizePx));
    }

    [Fact]
    public void TryToDomain_RejectsUnsupportedLanguage()
    {
        var dto = ValidDto() with { Language = "xx" };

        GeneralSettingsMapper.TryToDomain(dto, out _, out var errors).Should().BeFalse();
        errors.Should().Contain(nameof(GeneralSettingsDto.Language));
    }
}
