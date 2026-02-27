using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfCursors = System.Windows.Input.Cursors;

namespace ZoroKit.UI.Views;

public partial class DatabaseSelectionDialog : Window
{
    private readonly List<WpfCheckBox> _checkBoxes = [];
    private readonly List<WpfRadioButton> _radioButtons = [];
    private bool _suppressSelectAll;
    private bool _isMultiSelectField;

    public bool DialogConfirmed { get; private set; }
    public List<string> SelectedDatabases { get; } = [];
    public bool IsNewDatabase { get; private set; }

    public DatabaseSelectionDialog()
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

    /// <summary>
    /// Creates a multi-select dialog for backup (CheckBoxes + "Tümünü Seç").
    /// </summary>
    public static DatabaseSelectionDialog CreateForBackup(List<string> databases)
    {
        var dialog = new DatabaseSelectionDialog();
        dialog._isMultiSelectField = true;
        dialog.Owner = WpfApp.Current.MainWindow;
        dialog.TitleText.Text = "Veritabanı Yedekle";
        dialog.SubtitleText.Text = "Yedeklenecek veritabanlarını seçin";
        dialog.SelectAllCheckBox.Visibility = Visibility.Visible;
        dialog.SetOkButton("Yedekle", "#22C55E", "#16A34A");

        foreach (var db in databases)
        {
            var cb = new WpfCheckBox
            {
                Content = db,
                Tag = db,
                FontSize = 12.5,
                Foreground = (WpfBrush)WpfApp.Current.FindResource("PrimaryTextBrush"),
                Margin = new Thickness(6, 4, 6, 4),
                Cursor = WpfCursors.Hand
            };
            cb.Checked += dialog.Item_CheckChanged;
            cb.Unchecked += dialog.Item_CheckChanged;
            dialog._checkBoxes.Add(cb);
            dialog.DatabaseListPanel.Children.Add(cb);
        }

        return dialog;
    }

    /// <summary>
    /// Creates a single-select dialog for import (RadioButtons + "Yeni veritabanı oluştur").
    /// </summary>
    public static DatabaseSelectionDialog CreateForImport(List<string> databases)
    {
        var dialog = new DatabaseSelectionDialog();
        dialog._isMultiSelectField = false;
        dialog.Owner = WpfApp.Current.MainWindow;
        dialog.TitleText.Text = "Hedef Veritabanı Seçin";
        dialog.SubtitleText.Text = "SQL dosyasının içe aktarılacağı veritabanını seçin";
        dialog.SelectAllCheckBox.Visibility = Visibility.Collapsed;
        dialog.SetOkButton("İçe Aktar", "#1A8CFF", "#0070E0");

        // "Yeni veritabanı oluştur" option first
        var newDbRadio = new WpfRadioButton
        {
            GroupName = "DbSelect",
            Tag = "__new__",
            Margin = new Thickness(6, 4, 6, 4),
            Cursor = WpfCursors.Hand
        };
        var newDbContent = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        newDbContent.Children.Add(new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.DatabasePlus,
            Width = 14,
            Height = 14,
            Foreground = (WpfBrush)WpfApp.Current.FindResource("SuccessBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });
        newDbContent.Children.Add(new TextBlock
        {
            Text = "Yeni veritabanı oluştur",
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Foreground = (WpfBrush)WpfApp.Current.FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        newDbRadio.Content = newDbContent;
        dialog._radioButtons.Add(newDbRadio);
        dialog.DatabaseListPanel.Children.Add(newDbRadio);

        // Separator
        dialog.DatabaseListPanel.Children.Add(new Separator
        {
            Margin = new Thickness(6, 4, 6, 4),
            Background = (WpfBrush)WpfApp.Current.FindResource("BorderBrush")
        });

        foreach (var db in databases)
        {
            var rb = new WpfRadioButton
            {
                Content = db,
                Tag = db,
                GroupName = "DbSelect",
                FontSize = 12.5,
                Foreground = (WpfBrush)WpfApp.Current.FindResource("PrimaryTextBrush"),
                Margin = new Thickness(6, 4, 6, 4),
                Cursor = WpfCursors.Hand
            };
            dialog._radioButtons.Add(rb);
            dialog.DatabaseListPanel.Children.Add(rb);
        }

        // Select first existing DB by default if any, otherwise new
        if (dialog._radioButtons.Count > 1)
            dialog._radioButtons[1].IsChecked = true;
        else
            dialog._radioButtons[0].IsChecked = true;

        return dialog;
    }

    private void SetOkButton(string text, string bgColor, string hoverColor)
    {
        OkButton.Content = text;

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
        borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
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
        OkButton.Style = style;
    }

    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAll) return;
        _suppressSelectAll = true;
        foreach (var cb in _checkBoxes)
            cb.IsChecked = true;
        _suppressSelectAll = false;
    }

    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAll) return;
        _suppressSelectAll = true;
        foreach (var cb in _checkBoxes)
            cb.IsChecked = false;
        _suppressSelectAll = false;
    }

    private void Item_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAll) return;

        var allChecked = _checkBoxes.All(cb => cb.IsChecked == true);
        var noneChecked = _checkBoxes.All(cb => cb.IsChecked != true);

        _suppressSelectAll = true;
        SelectAllCheckBox.IsChecked = allChecked ? true : noneChecked ? false : null;
        _suppressSelectAll = false;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatabases.Clear();
        IsNewDatabase = false;

        if (_isMultiSelectField)
        {
            // Backup mode: collect checked items
            foreach (var cb in _checkBoxes)
            {
                if (cb.IsChecked == true && cb.Tag is string dbName)
                    SelectedDatabases.Add(dbName);
            }

            if (SelectedDatabases.Count == 0)
            {
                // Nothing selected — don't close
                return;
            }
        }
        else
        {
            // Import mode: get selected radio
            var selected = _radioButtons.FirstOrDefault(rb => rb.IsChecked == true);
            if (selected == null) return;

            if (selected.Tag is string tag && tag == "__new__")
            {
                IsNewDatabase = true;
            }
            else if (selected.Tag is string dbName)
            {
                SelectedDatabases.Add(dbName);
            }
        }

        DialogConfirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogConfirmed = false;
        Close();
    }
}
