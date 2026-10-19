using FluentAssertions;
using OpenHarness.UI.Avalonia.Localization;
using Xunit;

namespace OpenHarness.UnitTests.UI;

public class LocalizerTests
{
    [Fact]
    public void Indexer_ReturnsSpanishByDefault()
    {
        Localizer.Instance.Culture.Name.Should().Be("es");
        Localizer.Instance["Menu_Settings"].Should().Be("Configuración");
    }

    [Fact]
    public void Indexer_WithUnknownKey_ReturnsVisibleMarker()
    {
        Localizer.Instance["Does_Not_Exist"].Should().Be("#Does_Not_Exist#");
    }

    [Fact]
    public void Format_InsertsArguments()
    {
        Localizer.Instance.Format("Settings_General_Version", "1.2.3").Should().Be("Versión actual: 1.2.3");
    }

    [Fact]
    public void SetLanguage_WithUnsupportedCode_KeepsDefault()
    {
        Localizer.Instance.SetLanguage("xx");

        Localizer.Instance.Culture.Name.Should().Be(Localizer.DefaultLanguage);
    }

    [Fact]
    public void EveryResourceKeyUsedInXaml_Exists()
    {
        var root = FindRepoRoot();
        var xamlFiles = Directory.GetFiles(Path.Combine(root, "src", "Client", "OpenHarness.UI.Avalonia"), "*.axaml", SearchOption.AllDirectories);
        var keys = xamlFiles
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"\{i18n:Tr ([A-Za-z0-9_]+)\}"))
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        keys.Should().NotBeEmpty();
        keys.Where(k => Strings.ResourceManager.GetString(k) is null).Should().BeEmpty();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenHarness.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
