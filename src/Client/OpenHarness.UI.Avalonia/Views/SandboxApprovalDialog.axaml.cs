using Avalonia.Controls;
using Avalonia.Input;
using OpenHarness.UI.Avalonia.ViewModels;

namespace OpenHarness.UI.Avalonia.Views;

public partial class SandboxApprovalDialog : Window
{
    public SandboxApprovalDialog()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        KeyDown += OnWindowKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SandboxApprovalViewModel vm)
        {
            vm.RequestClose += OnViewModelRequestClose;
        }
    }

    private void OnViewModelRequestClose(bool result)
    {
        Close(result);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SandboxApprovalViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (vm.DenyCommand.CanExecute(null))
            {
                vm.DenyCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (vm.ApproveAndRememberCommand.CanExecute(null))
                {
                    vm.ApproveAndRememberCommand.Execute(null);
                }
            }
            else
            {
                if (vm.ApproveOnceCommand.CanExecute(null))
                {
                    vm.ApproveOnceCommand.Execute(null);
                }
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is SandboxApprovalViewModel vm)
        {
            vm.RequestClose -= OnViewModelRequestClose;
            vm.Dispose();
        }
        base.OnClosed(e);
    }
}

