using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class DiffEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isDiffMode = true;

    [ObservableProperty]
    private string _language = "diff";

    [ObservableProperty]
    private int _addedLinesCount;

    [ObservableProperty]
    private int _removedLinesCount;

    partial void OnContentChanged(string value)
    {
        CalculateStats(value);
    }

    private void CalculateStats(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            AddedLinesCount = 0;
            RemovedLinesCount = 0;
            return;
        }

        int added = 0;
        int removed = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('+') && !line.StartsWith("+++")) added++;
            else if (line.StartsWith('-') && !line.StartsWith("---")) removed++;
        }

        AddedLinesCount = added;
        RemovedLinesCount = removed;
    }
}

