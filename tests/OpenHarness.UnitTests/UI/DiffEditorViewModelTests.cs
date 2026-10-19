using FluentAssertions;
using OpenHarness.UI.Avalonia.ViewModels;
using Xunit;

namespace OpenHarness.UnitTests.UI;

public class DiffEditorViewModelTests
{
    [Fact]
    public void SettingContent_CalculatesAddedAndRemovedLines()
    {
        // Arrange
        var vm = new DiffEditorViewModel();
        var diff = """
            --- a/file.txt
            +++ b/file.txt
            @@ -1,3 +1,3 @@
            -old line 1
            -old line 2
            +new line 1
            +new line 2
            +new line 3
             unchanged line
            """;

        // Act
        vm.Content = diff;

        // Assert
        vm.AddedLinesCount.Should().Be(3);
        vm.RemovedLinesCount.Should().Be(2);
    }
}

