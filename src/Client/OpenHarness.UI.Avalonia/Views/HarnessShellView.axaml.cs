using Avalonia.Controls;
using OpenHarness.UI.Avalonia.ViewModels;

namespace OpenHarness.UI.Avalonia.Views;

public partial class HarnessShellView : UserControl
{
    public HarnessShellView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is HarnessShellViewModel vm)
        {
            vm.ShowApprovalDialogRequested += OnShowApprovalDialogRequested;
        }
    }

    private async Task<bool> OnShowApprovalDialogRequested(SandboxApprovalViewModel approvalVm)
    {
        var parentWindow = this.VisualRoot as Window;
        var dialog = new SandboxApprovalDialog
        {
            DataContext = approvalVm
        };

        if (parentWindow is not null)
        {
            return await dialog.ShowDialog<bool>(parentWindow);
        }

        dialog.Show();
        return false;
    }
}

