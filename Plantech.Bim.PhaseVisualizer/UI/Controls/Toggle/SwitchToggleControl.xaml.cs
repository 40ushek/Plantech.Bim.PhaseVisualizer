using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Plantech.Bim.PhaseVisualizer.UI.Controls.Toggle;

public partial class SwitchToggleControl : UserControl
{
    private static readonly Brush OnForeground = new SolidColorBrush(Color.FromRgb(11, 87, 208));
    private static readonly Brush OffForeground = new SolidColorBrush(Color.FromRgb(75, 85, 99));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SwitchToggleControl),
        new PropertyMetadata("Toggle"));

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked),
        typeof(bool),
        typeof(SwitchToggleControl),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnVisualPropertyChanged));

    public static readonly DependencyProperty OnTextProperty = DependencyProperty.Register(
        nameof(OnText),
        typeof(string),
        typeof(SwitchToggleControl),
        new PropertyMetadata("On", OnVisualPropertyChanged));

    public static readonly DependencyProperty OffTextProperty = DependencyProperty.Register(
        nameof(OffText),
        typeof(string),
        typeof(SwitchToggleControl),
        new PropertyMetadata("Off", OnVisualPropertyChanged));

    public static readonly DependencyProperty ShowStateTextProperty = DependencyProperty.Register(
        nameof(ShowStateText),
        typeof(bool),
        typeof(SwitchToggleControl),
        new PropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty ShowLabelProperty = DependencyProperty.Register(
        nameof(ShowLabel),
        typeof(bool),
        typeof(SwitchToggleControl),
        new PropertyMetadata(true, OnVisualPropertyChanged));

    public event RoutedEventHandler? Toggled;

    public SwitchToggleControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string OnText
    {
        get => (string)GetValue(OnTextProperty);
        set => SetValue(OnTextProperty, value);
    }

    public string OffText
    {
        get => (string)GetValue(OffTextProperty);
        set => SetValue(OffTextProperty, value);
    }

    public bool ShowStateText
    {
        get => (bool)GetValue(ShowStateTextProperty);
        set => SetValue(ShowStateTextProperty, value);
    }

    public bool ShowLabel
    {
        get => (bool)GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStateText();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SwitchToggleControl)d).UpdateStateText();
    }

    private void OnSwitchToggled(object sender, RoutedEventArgs e)
    {
        UpdateStateText();
        Toggled?.Invoke(this, e);
    }

    private void UpdateStateText()
    {
        if (ShowLabel)
        {
            if (LabelTextBlock != null)
            {
                LabelTextBlock.Visibility = Visibility.Visible;
            }
        }
        else
        {
            if (LabelTextBlock != null)
            {
                LabelTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        if (StateTextBlock == null)
        {
            return;
        }

        StateTextBlock.Visibility = ShowStateText ? Visibility.Visible : Visibility.Collapsed;
        if (!ShowStateText)
        {
            return;
        }

        StateTextBlock.Text = IsChecked ? OnText : OffText;
        StateTextBlock.Foreground = IsChecked ? OnForeground : OffForeground;
    }
}
