using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfCursors = System.Windows.Input.Cursors;

namespace ZaraGON.UI.Views;

public enum DialogType
{
    Info,
    Warning,
    Error,
    Confirm
}

public partial class ModernDialog : Window
{
    public bool DialogConfirmed { get; private set; }
    public string InputValue => InputBox.Text;

    public ModernDialog()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => DragMove();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogConfirmed = false;
                Close();
            }
        };
    }

    public static ModernDialog Create(DialogType type, string message, string title)
    {
        var dialog = new ModernDialog();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;

        switch (type)
        {
            case DialogType.Info:
                dialog.SetIcon("InformationOutline", "#1A8CFF", "#E8F4FF");
                dialog.OkButton.Content = "Tamam";
                dialog.SetButtonStyle(dialog.OkButton, "#1A8CFF", "#0070E0");
                break;
            case DialogType.Warning:
                dialog.SetIcon("AlertOutline", "#F59E0B", "#FFFBEB");
                dialog.OkButton.Content = "Tamam";
                dialog.SetButtonStyle(dialog.OkButton, "#F59E0B", "#D97706");
                break;
            case DialogType.Error:
                dialog.SetIcon("CloseCircleOutline", "#EF4444", "#FEF2F2");
                dialog.OkButton.Content = "Tamam";
                dialog.SetButtonStyle(dialog.OkButton, "#EF4444", "#DC2626");
                break;
            case DialogType.Confirm:
                dialog.SetIcon("HelpCircleOutline", "#1A8CFF", "#E8F4FF");
                dialog.OkButton.Content = "Evet";
                dialog.SetButtonStyle(dialog.OkButton, "#1A8CFF", "#0070E0");
                dialog.CancelButton.Content = "Hay\u0131r";
                dialog.CancelButton.Visibility = Visibility.Visible;
                break;
        }

        return dialog;
    }

    public static ModernDialog CreateInput(string message, string title, string defaultValue)
    {
        var dialog = new ModernDialog();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.SetIcon("TextBoxOutline", "#1A8CFF", "#E8F4FF");
        dialog.OkButton.Content = "Tamam";
        dialog.SetButtonStyle(dialog.OkButton, "#1A8CFF", "#0070E0");
        dialog.CancelButton.Content = "\u0130ptal";
        dialog.CancelButton.Visibility = Visibility.Visible;
        dialog.InputBox.Visibility = Visibility.Visible;
        dialog.InputBox.Text = defaultValue;
        dialog.Loaded += (_, _) =>
        {
            dialog.InputBox.Focus();
            dialog.InputBox.SelectAll();
        };
        return dialog;
    }

    private void SetIcon(string iconKind, string iconColor, string bgColor)
    {
        DialogIcon.Kind = (MaterialDesignThemes.Wpf.PackIconKind)
            Enum.Parse(typeof(MaterialDesignThemes.Wpf.PackIconKind), iconKind);
        DialogIcon.Foreground = new SolidColorBrush((Color)WpfColorConverter.ConvertFromString(iconColor));
        IconBorder.Background = new SolidColorBrush((Color)WpfColorConverter.ConvertFromString(bgColor));
    }

    private void SetButtonStyle(WpfButton button, string bgColor, string hoverColor)
    {
        var bg = (Color)WpfColorConverter.ConvertFromString(bgColor);
        var hover = (Color)WpfColorConverter.ConvertFromString(hoverColor);

        var style = new Style(typeof(WpfButton));
        style.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(bg)));
        style.Setters.Add(new Setter(ForegroundProperty, WpfBrushes.White));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(CursorProperty, WpfCursors.Hand));
        style.Setters.Add(new Setter(FontWeightProperty, FontWeights.Medium));

        var template = new ControlTemplate(typeof(WpfButton));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
        borderFactory.SetValue(Border.PaddingProperty,
            new TemplateBindingExtension(PaddingProperty));
        borderFactory.Name = "border";

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger
        {
            Property = IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(hover), "border"));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(TemplateProperty, template));
        button.Style = style;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogConfirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogConfirmed = false;
        Close();
    }
}
