using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace OpenHarness.UI.Avalonia.Controls;

public class DiffColorizingTransformer : DocumentColorizingTransformer
{
    private static readonly IBrush AddedForeground = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush AddedBackground = new SolidColorBrush(Color.Parse("#163323"));
    private static readonly IBrush RemovedForeground = new SolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush RemovedBackground = new SolidColorBrush(Color.Parse("#3B181E"));
    private static readonly IBrush HeaderForeground = new SolidColorBrush(Color.Parse("#38BDF8"));

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);
        if (string.IsNullOrEmpty(lineText)) return;

        if (lineText.StartsWith('+') && !lineText.StartsWith("+++"))
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(AddedForeground);
                element.TextRunProperties.SetBackgroundBrush(AddedBackground);
            });
        }
        else if (lineText.StartsWith('-') && !lineText.StartsWith("---"))
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(RemovedForeground);
                element.TextRunProperties.SetBackgroundBrush(RemovedBackground);
            });
        }
        else if (lineText.StartsWith("@@"))
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(HeaderForeground);
            });
        }
    }
}

