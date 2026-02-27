using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.UI.Controls.Toggle;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Plantech.Bim.PhaseVisualizer.UI;

public partial class PhaseVisualizerView : UserControl
{
    private const string SpacerColumnKey = "__spacer";

    private PhaseVisualizerViewModel? _viewModel;
    private CheckBox? _selectAllCheckBox;
    private bool _isUpdatingSelectAllCheckBox;
    private bool _isInitialized;
    private bool _isReloadingRows;

    public event EventHandler? RequestClose;

    public PhaseVisualizerView()
    {
        InitializeComponent();
    }

    internal void Initialize(PhaseVisualizerViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        if (!_isInitialized)
        {
            RowsGrid.PreviewMouseLeftButtonDown += RowsGrid_PreviewMouseLeftButtonDown;
            RowsGrid.Sorting += RowsGrid_Sorting;
            _isInitialized = true;
        }

        ReloadRows(
            saveCurrentState: false,
            restoreShowAllPhasesFromState: true,
            forceReloadFromModel: false);
    }

    internal void TrySaveState()
    {
        if (_viewModel == null)
        {
            return;
        }

        try
        {
            RowsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            RowsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            _viewModel.SetTableLayoutState(CaptureTableLayoutState());
            _viewModel.SaveState();
        }
        catch
        {
            // Keep close operation non-blocking if state persistence fails.
        }
    }

    private void BuildColumns()
    {
        if (_viewModel == null)
        {
            return;
        }

        RowsGrid.Columns.Clear();
        var centeredTemplateCellStyle = CreateCenteredTemplateCellStyle();

        _selectAllCheckBox = new CheckBox
        {
            IsThreeState = true,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            ToolTip = "Select/Deselect all rows",
        };
        _selectAllCheckBox.Click += SelectAllCheckBox_Click;

        var selectAllHeaderStyle = new Style(typeof(DataGridColumnHeader));
        selectAllHeaderStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        selectAllHeaderStyle.Setters.Add(new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Center));
        selectAllHeaderStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));

        RowsGrid.Columns.Add(new DataGridTemplateColumn
        {
            Header = _selectAllCheckBox,
            HeaderStyle = selectAllHeaderStyle,
            CellStyle = centeredTemplateCellStyle,
            CellTemplate = CreateCenteredCheckBoxTemplate("[__selected]", isReadOnly: false),
            CellEditingTemplate = CreateCenteredCheckBoxTemplate("[__selected]", isReadOnly: false),
            Width = 48,
            CanUserSort = false,
            IsReadOnly = false,
            SortMemberPath = "__selected",
        });

        foreach (var column in _viewModel.Columns)
        {
            if (column.Type == PhaseValueType.Boolean)
            {
                var useSwitchToggle = column.IsEditable;
                RowsGrid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = column.Label,
                    IsReadOnly = !column.IsEditable,
                    CellStyle = centeredTemplateCellStyle,
                    CellTemplate = useSwitchToggle
                        ? CreateCenteredSwitchToggleTemplate($"[{column.Key}]", !column.IsEditable)
                        : CreateCenteredCheckBoxTemplate($"[{column.Key}]", !column.IsEditable),
                    CellEditingTemplate = useSwitchToggle
                        ? CreateCenteredSwitchToggleTemplate($"[{column.Key}]", !column.IsEditable)
                        : CreateCenteredCheckBoxTemplate($"[{column.Key}]", !column.IsEditable),
                    Width = 110,
                    SortMemberPath = column.Key,
                });
            }
            else
            {
                RowsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = column.Label,
                    Binding = new Binding($"[{column.Key}]")
                    {
                        Mode = column.IsEditable ? BindingMode.TwoWay : BindingMode.OneWay,
                        UpdateSourceTrigger = column.IsEditable
                            ? UpdateSourceTrigger.PropertyChanged
                            : UpdateSourceTrigger.Default,
                    },
                    IsReadOnly = !column.IsEditable,
                    Width = column.Key == "phase_number"
                        ? new DataGridLength(78)
                        : new DataGridLength(1, DataGridLengthUnitType.Star),
                    SortMemberPath = column.Key,
                });
            }
        }

        // Trailing spacer makes right-side resizing of real columns easier.
        RowsGrid.Columns.Add(new DataGridTemplateColumn
        {
            Header = string.Empty,
            IsReadOnly = true,
            CanUserSort = false,
            CanUserReorder = false,
            CanUserResize = false,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 28,
            SortMemberPath = SpacerColumnKey,
            CellTemplate = CreateEmptyCellTemplate(),
            CellEditingTemplate = CreateEmptyCellTemplate(),
        });

        RowsGrid.ItemsSource = _viewModel.RowsView;
        ApplySavedTableLayout();
        UpdateSelectAllCheckBoxState();
    }

    private PhaseTableLayoutState CaptureTableLayoutState()
    {
        var layout = new PhaseTableLayoutState();
        foreach (var column in RowsGrid.Columns)
        {
            var key = column.SortMemberPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)
                || string.Equals(key, SpacerColumnKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var width = column.ActualWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0d)
            {
                width = column.Width.DisplayValue;
            }

            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0d)
            {
                width = 80d;
            }

            layout.Columns.Add(new PhaseTableColumnLayoutState
            {
                Key = key,
                DisplayIndex = column.DisplayIndex,
                Width = width,
            });

            if (column.SortDirection.HasValue)
            {
                layout.Sort = new PhaseTableSortLayoutState
                {
                    ColumnKey = key,
                    Descending = column.SortDirection.Value == ListSortDirection.Descending,
                };
            }
        }

        return layout;
    }

    private void ApplySavedTableLayout()
    {
        if (_viewModel == null || RowsGrid.Columns.Count == 0)
        {
            return;
        }

        var layout = _viewModel.GetTableLayoutState();
        if (layout == null)
        {
            return;
        }

        var columnsByKey = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in RowsGrid.Columns)
        {
            var key = column.SortMemberPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                columnsByKey[key] = column;
            }
        }

        foreach (var savedColumn in layout.Columns ?? new List<PhaseTableColumnLayoutState>())
        {
            if (savedColumn == null
                || string.IsNullOrWhiteSpace(savedColumn.Key)
                || !columnsByKey.TryGetValue(savedColumn.Key, out var targetColumn))
            {
                continue;
            }

            if (savedColumn.Width > 0d
                && !double.IsNaN(savedColumn.Width)
                && !double.IsInfinity(savedColumn.Width))
            {
                targetColumn.Width = new DataGridLength(savedColumn.Width, DataGridLengthUnitType.Pixel);
            }
        }

        foreach (var savedColumn in layout.Columns ?? new List<PhaseTableColumnLayoutState>())
        {
            if (savedColumn == null
                || string.IsNullOrWhiteSpace(savedColumn.Key)
                || !columnsByKey.TryGetValue(savedColumn.Key, out var targetColumn))
            {
                continue;
            }

            var maxIndex = Math.Max(0, RowsGrid.Columns.Count - 1);
            var targetIndex = savedColumn.DisplayIndex;
            if (targetIndex < 0)
            {
                targetIndex = 0;
            }
            else if (targetIndex > maxIndex)
            {
                targetIndex = maxIndex;
            }

            try
            {
                targetColumn.DisplayIndex = targetIndex;
            }
            catch
            {
                // Keep layout restore best-effort.
            }
        }

        if (columnsByKey.TryGetValue(SpacerColumnKey, out var spacerColumn))
        {
            spacerColumn.DisplayIndex = RowsGrid.Columns.Count - 1;
        }

        var sort = layout.Sort;
        if (sort == null
            || string.IsNullOrWhiteSpace(sort.ColumnKey)
            || !columnsByKey.TryGetValue(sort.ColumnKey, out var sortColumn)
            || sortColumn.DisplayIndex == 0)
        {
            return;
        }

        var direction = sort.Descending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        foreach (var column in RowsGrid.Columns)
        {
            if (!ReferenceEquals(column, sortColumn))
            {
                column.SortDirection = null;
            }
        }

        sortColumn.SortDirection = direction;
        _viewModel.TrySortRows(sort.ColumnKey, direction);
    }

    private static DataTemplate CreateCenteredCheckBoxTemplate(string bindingPath, bool isReadOnly)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        root.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
        root.SetValue(MarginProperty, new Thickness(0));

        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        checkBox.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        checkBox.SetValue(MarginProperty, new Thickness(0));
        checkBox.SetValue(PaddingProperty, new Thickness(0));
        checkBox.SetValue(MinWidthProperty, 16d);
        checkBox.SetValue(MinHeightProperty, 16d);
        checkBox.SetValue(IsEnabledProperty, !isReadOnly);
        checkBox.SetBinding(
            ToggleButton.IsCheckedProperty,
            new Binding(bindingPath)
            {
                Mode = isReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });

        root.AppendChild(checkBox);

        return new DataTemplate
        {
            VisualTree = root,
        };
    }

    private static DataTemplate CreateEmptyCellTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.PaddingProperty, new Thickness(0));
        border.SetValue(IsHitTestVisibleProperty, false);

        return new DataTemplate
        {
            VisualTree = border,
        };
    }

    private static DataTemplate CreateCenteredSwitchToggleTemplate(string bindingPath, bool isReadOnly)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        root.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
        root.SetValue(MarginProperty, new Thickness(0));

        var toggle = new FrameworkElementFactory(typeof(SwitchToggleControl));
        toggle.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        toggle.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        toggle.SetValue(MarginProperty, new Thickness(0));
        toggle.SetValue(WidthProperty, 34d);
        toggle.SetValue(SwitchToggleControl.ShowLabelProperty, false);
        toggle.SetValue(SwitchToggleControl.ShowStateTextProperty, false);
        toggle.SetValue(IsEnabledProperty, !isReadOnly);
        toggle.SetBinding(
            SwitchToggleControl.IsCheckedProperty,
            new Binding(bindingPath)
            {
                Mode = isReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });

        root.AppendChild(toggle);

        return new DataTemplate
        {
            VisualTree = root,
        };
    }

    private static Style CreateCenteredTemplateCellStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
        return style;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ReloadRows(
            saveCurrentState: true,
            restoreShowAllPhasesFromState: false,
            forceReloadFromModel: true);
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.OpenLogFile();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        // Force commit in-place checkbox edits before collecting selected rows.
        RowsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RowsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _viewModel.Apply();
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        RowsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RowsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_viewModel.LoadPreset())
        {
            BuildColumns();
        }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        RowsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RowsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _viewModel.SavePreset();
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.DeletePreset();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void ShowAllPhasesToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _isReloadingRows)
        {
            return;
        }

        var showAllPhases = ShowAllPhasesToggle.IsChecked == true;
        _viewModel.ShowAllPhases = showAllPhases;
        ReloadRows(
            saveCurrentState: true,
            restoreShowAllPhasesFromState: false,
            forceReloadFromModel: false);
    }

    private void SearchScopeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _isReloadingRows)
        {
            return;
        }

        var useVisibleViewsForSearch = SearchScopeToggle.IsChecked == true;
        _viewModel.UseVisibleViewsForSearch = useVisibleViewsForSearch;
        ReloadRows(
            saveCurrentState: true,
            restoreShowAllPhasesFromState: false,
            forceReloadFromModel: false);
    }

    private void ShowObjectCountToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _isReloadingRows)
        {
            return;
        }

        _viewModel.ShowObjectCountInStatus = ShowObjectCountToggle.IsChecked == true;
        ReloadRows(
            saveCurrentState: true,
            restoreShowAllPhasesFromState: false,
            forceReloadFromModel: false);
    }

    private void RowsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        if (source == null)
        {
            return;
        }

        // Clicking empty table area should clear UI selection instead of selecting a nearby row.
        var rowUnderMouse = FindParent<DataGridRow>(source);
        if (rowUnderMouse == null
            && FindParent<DataGridColumnHeader>(source) == null
            && FindParent<ScrollBar>(source) == null)
        {
            if (RowsGrid.SelectedItems.Count > 0)
            {
                RowsGrid.UnselectAll();
                RowsGrid.CurrentCell = new DataGridCellInfo();
                e.Handled = true;
            }

            return;
        }

        var cell = FindParent<DataGridCell>(source);
        if (cell == null || cell.Column == null)
        {
            return;
        }

        var sortKey = cell.Column.SortMemberPath;
        var isBooleanColumn = !string.IsNullOrWhiteSpace(sortKey)
            && _viewModel.RowsView.Table.Columns.Contains(sortKey)
            && _viewModel.RowsView.Table.Columns[sortKey].DataType == typeof(bool);

        // For editable boolean columns, toggle immediately on first click
        // instead of requiring "select cell first, click second".
        if (isBooleanColumn
            && !cell.Column.IsReadOnly
            && cell.Column.DisplayIndex != 0)
        {
            var boolRow = FindParent<DataGridRow>(cell);
            if (boolRow?.Item is System.Data.DataRowView clickedRowView)
            {
                if (clickedRowView.Row.Table.Columns.Contains(sortKey)
                    && clickedRowView.Row[sortKey] is bool currentValue)
                {
                    var targetValue = !currentValue;
                    var appliedToSelection = false;

                    if (RowsGrid.SelectedItems.Count > 1)
                    {
                        var selectedRows = new List<System.Data.DataRowView>();
                        var clickedInSelection = false;
                        foreach (var selectedItem in RowsGrid.SelectedItems)
                        {
                            if (selectedItem is not System.Data.DataRowView selectedRowView)
                            {
                                continue;
                            }

                            selectedRows.Add(selectedRowView);
                            if (ReferenceEquals(selectedRowView, clickedRowView))
                            {
                                clickedInSelection = true;
                            }
                        }

                        if (clickedInSelection)
                        {
                            foreach (var selectedRow in selectedRows)
                            {
                                if (selectedRow.Row.Table.Columns.Contains(sortKey))
                                {
                                    selectedRow.Row[sortKey] = targetValue;
                                }
                            }

                            appliedToSelection = true;
                        }
                    }

                    if (!appliedToSelection)
                    {
                        clickedRowView.Row[sortKey] = targetValue;
                    }

                    e.Handled = true;
                    return;
                }
            }
        }

        if (cell.Column.DisplayIndex != 0)
        {
            return;
        }

        var row = FindParent<DataGridRow>(cell);
        if (row?.Item is not System.Data.DataRowView dataRowView)
        {
            return;
        }

        var current = dataRowView.Row["__selected"] is bool flag && flag;
        var selectionTargetValue = !current;
        var selectionAppliedToSelection = false;

        if (RowsGrid.SelectedItems.Count > 1)
        {
            var selectedRows = new List<System.Data.DataRowView>();
            var clickedInSelection = false;
            foreach (var selectedItem in RowsGrid.SelectedItems)
            {
                if (selectedItem is not System.Data.DataRowView selectedRowView)
                {
                    continue;
                }

                selectedRows.Add(selectedRowView);
                if (ReferenceEquals(selectedRowView, dataRowView))
                {
                    clickedInSelection = true;
                }
            }

            if (clickedInSelection)
            {
                foreach (var selectedRow in selectedRows)
                {
                    selectedRow.Row["__selected"] = selectionTargetValue;
                }

                selectionAppliedToSelection = true;
            }
        }

        if (!selectionAppliedToSelection)
        {
            dataRowView.Row["__selected"] = selectionTargetValue;
        }

        UpdateSelectAllCheckBoxState();
        e.Handled = true;
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _isUpdatingSelectAllCheckBox || _selectAllCheckBox == null)
        {
            return;
        }

        var targetChecked = _selectAllCheckBox.IsChecked == true;
        RowsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RowsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        foreach (var item in _viewModel.RowsView)
        {
            if (item is System.Data.DataRowView rowView)
            {
                rowView.Row["__selected"] = targetChecked;
            }
        }

        UpdateSelectAllCheckBoxState();
    }

    private void UpdateSelectAllCheckBoxState()
    {
        if (_viewModel == null || _selectAllCheckBox == null)
        {
            return;
        }

        var total = 0;
        var selected = 0;
        foreach (var item in _viewModel.RowsView)
        {
            if (item is not System.Data.DataRowView rowView)
            {
                continue;
            }

            total++;
            if (rowView.Row["__selected"] is bool flag && flag)
            {
                selected++;
            }
        }

        _isUpdatingSelectAllCheckBox = true;
        try
        {
            _selectAllCheckBox.IsChecked = total == 0
                ? false
                : selected == 0
                    ? false
                    : selected == total
                        ? true
                        : (bool?)null;
        }
        finally
        {
            _isUpdatingSelectAllCheckBox = false;
        }
    }

    private void RowsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (_viewModel == null)
        {
            e.Handled = true;
            return;
        }

        if (e.Column?.DisplayIndex == 0)
        {
            e.Handled = true;
            return;
        }

        var targetColumn = e.Column;
        if (targetColumn == null)
        {
            e.Handled = true;
            return;
        }

        var sortKey = targetColumn.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortKey))
        {
            e.Handled = true;
            return;
        }

        try
        {
            RowsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            RowsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var nextDirection = targetColumn.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            foreach (var column in RowsGrid.Columns)
            {
                if (!ReferenceEquals(column, targetColumn))
                {
                    column.SortDirection = null;
                }
            }

            targetColumn.SortDirection = nextDirection;
            _viewModel.TrySortRows(sortKey, nextDirection);
            _viewModel.SetTableLayoutState(CaptureTableLayoutState());
        }
        catch
        {
            // Keep window alive even if sorting fails for a specific column/value set.
        }
        finally
        {
            e.Handled = true;
        }
    }

    private static T? FindParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void ReloadRows(
        bool saveCurrentState = true,
        bool restoreShowAllPhasesFromState = false,
        bool forceReloadFromModel = false)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (saveCurrentState)
        {
            TrySaveState();
        }

        try
        {
            _isReloadingRows = true;
            _viewModel.Load(
                restoreShowAllPhasesFromState: restoreShowAllPhasesFromState,
                forceReloadFromModel: forceReloadFromModel);
            BuildColumns();
        }
        finally
        {
            _isReloadingRows = false;
        }
    }
}
