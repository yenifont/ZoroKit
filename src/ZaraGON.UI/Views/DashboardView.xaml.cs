using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZaraGON.UI.ViewModels;

namespace ZaraGON.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();

    private void ShortcutCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && DataContext is DashboardViewModel vm)
        {
            ICommand? command = element.Tag?.ToString() switch
            {
                "Terminal" => vm.OpenTerminalCommand,
                "PhpMyAdmin" => vm.OpenPhpMyAdminCommand,
                "Web" => vm.OpenWebCommand,
                "Root" => vm.OpenRootCommand,
                _ => null
            };
            command?.Execute(null);
        }
    }
}
