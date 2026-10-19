using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using OpenHarness.UI.Avalonia.ViewModels;

namespace OpenHarness.UI.Avalonia.Controls;

public partial class DiffEditorControl : UserControl
{
    private readonly DiffColorizingTransformer _diffTransformer = new();

    public DiffEditorControl()
    {
        InitializeComponent();

        var editor = this.FindControl<TextEditor>("Editor");
        if (editor is not null)
        {
            editor.TextArea.TextView.LineTransformers.Add(_diffTransformer);
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DiffEditorViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateEditorText(vm.Content);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiffEditorViewModel.Content))
        {
            if (DataContext is DiffEditorViewModel vm)
            {
                UpdateEditorText(vm.Content);
            }
        }
    }

    private void UpdateEditorText(string text)
    {
        var editor = this.FindControl<TextEditor>("Editor");
        if (editor is not null)
        {
            editor.Document = new TextDocument(text ?? string.Empty);
        }
    }
}

