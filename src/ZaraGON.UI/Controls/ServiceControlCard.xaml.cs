using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZaraGON.Core.Enums;

namespace ZaraGON.UI.Controls;

public partial class ServiceControlCard : UserControl
{
    public static readonly DependencyProperty ServiceNameProperty =
        DependencyProperty.Register(nameof(ServiceName), typeof(string), typeof(ServiceControlCard), new PropertyMetadata("Service"));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(ServiceStatus), typeof(ServiceControlCard), new PropertyMetadata(ServiceStatus.Stopped));

    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register(nameof(Version), typeof(string), typeof(ServiceControlCard), new PropertyMetadata("N/A"));

    public static readonly DependencyProperty StartCommandProperty =
        DependencyProperty.Register(nameof(StartCommand), typeof(ICommand), typeof(ServiceControlCard));

    public static readonly DependencyProperty StopCommandProperty =
        DependencyProperty.Register(nameof(StopCommand), typeof(ICommand), typeof(ServiceControlCard));

    public static readonly DependencyProperty RestartCommandProperty =
        DependencyProperty.Register(nameof(RestartCommand), typeof(ICommand), typeof(ServiceControlCard));

    public string ServiceName
    {
        get => (string)GetValue(ServiceNameProperty);
        set => SetValue(ServiceNameProperty, value);
    }

    public ServiceStatus Status
    {
        get => (ServiceStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Version
    {
        get => (string)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public ICommand? StartCommand
    {
        get => (ICommand?)GetValue(StartCommandProperty);
        set => SetValue(StartCommandProperty, value);
    }

    public ICommand? StopCommand
    {
        get => (ICommand?)GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public ICommand? RestartCommand
    {
        get => (ICommand?)GetValue(RestartCommandProperty);
        set => SetValue(RestartCommandProperty, value);
    }

    public ServiceControlCard()
    {
        InitializeComponent();
    }
}
