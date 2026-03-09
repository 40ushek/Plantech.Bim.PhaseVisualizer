using Plantech.Bim.Custom.Debug;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Plantech.Bim.Custom.Host;

internal sealed class HostWindow : Window
{
    private readonly TextBox _output;
    private readonly TextBlock _status;

    public HostWindow()
    {
        Title = "Plantech Bim Custom Host";
        Width = 900;
        Height = 700;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;

        var root = new DockPanel
        {
            Margin = new Thickness(16),
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 12),
        };
        DockPanel.SetDock(header, Dock.Top);

        header.Children.Add(new TextBlock
        {
            Text = "Interactive check for CUSTOM.PT.Filtered01",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        });

        header.Children.Add(new TextBlock
        {
            Text = "Pick an object in Tekla, get its id, and call CUSTOM.PT.Filtered01 directly.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
        });

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 12),
        };
        DockPanel.SetDock(controls, Dock.Top);

        var pickButton = new Button
        {
            Content = "Pick Object In Tekla",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 12, 0),
            MinWidth = 160,
        };
        pickButton.Click += (_, _) => PickAndEvaluateObject();
        controls.Children.Add(pickButton);

        _status = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Brushes.DarkSlateGray,
            Text = "Ready.",
        };
        DockPanel.SetDock(_status, Dock.Top);

        _output = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
        };

        var clearButton = new Button
        {
            Content = "Clear",
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 90,
        };
        clearButton.Click += (_, _) => ClearOutput();
        controls.Children.Add(clearButton);

        root.Children.Add(header);
        root.Children.Add(controls);
        root.Children.Add(_status);
        root.Children.Add(_output);

        Content = root;
    }

    private void ClearOutput()
    {
        _status.Text = "Ready.";
        _output.Text = string.Empty;
    }

    private void PickAndEvaluateObject()
    {
        try
        {
            var picker = new Picker();
            var picked = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_OBJECT, "Pick object for CUSTOM.PT.Filtered01");
            if (picked is not ModelObject modelObject)
            {
                _status.Text = "No object was picked.";
                return;
            }

            var objectId = modelObject.Identifier.ID;
            var intValue = FilteredPluginDebugRunner.GetIntegerProperty(objectId);
            var doubleValue = FilteredPluginDebugRunner.GetDoubleProperty(objectId);
            var stringValue = FilteredPluginDebugRunner.GetStringProperty(objectId);

            _status.Text = $"Plugin called for object {objectId}. Integer={intValue}.";
            _output.Text =
$"ObjectId: {objectId}{Environment.NewLine}" +
$"ObjectType: {modelObject.GetType().Name}{Environment.NewLine}" +
$"Plugin: CUSTOM.PT.Filtered01{Environment.NewLine}" +
$"GetIntegerProperty: {intValue}{Environment.NewLine}" +
$"GetDoubleProperty: {doubleValue}{Environment.NewLine}" +
$"GetStringProperty: {stringValue}";
        }
        catch (Exception ex)
        {
            _status.Text = "Evaluation failed.";
            _output.Text = ex.ToString();
        }
    }
}
