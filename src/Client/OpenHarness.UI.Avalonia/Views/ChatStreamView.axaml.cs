using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpenHarness.UI.Avalonia.ViewModels;

namespace OpenHarness.UI.Avalonia.Views;

public partial class ChatStreamView : UserControl
{
    public ChatStreamView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        var promptBox = this.FindControl<TextBox>("PromptTextBox");
        if (promptBox is not null)
        {
            promptBox.KeyDown += OnPromptKeyDown;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ChatStreamViewModel vm)
        {
            vm.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");
            scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (DataContext is ChatStreamViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                e.Handled = true;
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}

