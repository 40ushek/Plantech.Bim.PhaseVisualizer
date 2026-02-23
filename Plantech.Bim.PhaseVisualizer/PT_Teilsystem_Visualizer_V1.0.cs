using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using Tekla.Structures.Filtering;
using Tekla.Structures.Filtering.Categories;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

public sealed class Macro
{
    private static TeilsystemSelectionForm _popup;

    [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
    public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime)
    {
        if (_popup != null && !_popup.IsDisposed)
        {
            try
            {
                _popup.SaveCurrentSelectionState();
                _popup.Close();
                _popup.Dispose();
            }
            catch { }
        }

        var teilsysteme = GetAllPhasesFromModel();
        _popup = new TeilsystemSelectionForm(teilsysteme);
        _popup.TopMost = true;
        _popup.BringToFront();
        _popup.Activate();
        _popup.ShowDialog();
    }

    private static List<Teilsystem> GetAllPhasesFromModel()
    {
        var model = new Model();
        var phases = new List<Teilsystem>();
        var phaseCollection = model.GetPhases();
        var enumerator = phaseCollection.GetEnumerator();
        var savedStates = LoadSelectionState();

        while (enumerator.MoveNext())
        {
            var phase = enumerator.Current as Phase;
            if (phase == null) continue;

            var nummer = phase.PhaseNumber;
            var teilsystem = new Teilsystem
            {
                Nummer = nummer,
                Name = phase.PhaseName,
                Aktiv = false,
                OhneGitterroste = false,
                OhneBestand = false
            };

            if (savedStates.ContainsKey(nummer))
            {
                var saved = savedStates[nummer];
                teilsystem.Aktiv = saved.Aktiv;
                teilsystem.OhneGitterroste = saved.OhneGitterroste;
                teilsystem.OhneBestand = saved.OhneBestand;
            }

            phases.Add(teilsystem);
        }

        return phases.OrderBy(t => t.Nummer).ToList();
    }

    public static void ApplyFilter(List<Teilsystem> selectedTeilsysteme)
    {
        var filterExpressions = CreateFilter(selectedTeilsysteme);
        ApplyFilterToActiveView(filterExpressions);
    }

    private static BinaryFilterExpressionCollection CreateFilter(List<Teilsystem> teilsysteme)
    {
        var expressions = new BinaryFilterExpressionCollection();

        foreach (var teilsystem in teilsysteme)
        {
            var phaseExpression = new ObjectFilterExpressions.Phase();
            var phaseValue = new NumericConstantFilterExpression(teilsystem.Nummer);
            var phaseFilter = new BinaryFilterExpression(phaseExpression, NumericOperatorType.IS_EQUAL, phaseValue);

            var inner = new BinaryFilterExpressionCollection();
            inner.Add(new BinaryFilterExpressionItem(phaseFilter, BinaryFilterOperatorType.BOOLEAN_AND));

            if (teilsystem.OhneBestand)
            {
                var bestandExpression = new ObjectFilterExpressions.CustomString("PT_INFO_BESTAND");
                var bestandValue = new StringConstantFilterExpression("1");
                var notEqual = new BinaryFilterExpression(bestandExpression, StringOperatorType.IS_NOT_EQUAL, bestandValue);
                inner.Add(new BinaryFilterExpressionItem(notEqual, BinaryFilterOperatorType.BOOLEAN_AND));
            }

            if (teilsystem.OhneGitterroste)
            {
                var profile = new TemplateFilterExpressions.CustomString("ASSEMBLY.MAINPART.PROFILE");
                var giroValue = new StringConstantFilterExpression("GIRO");
                var notStarts = new BinaryFilterExpression(profile, StringOperatorType.NOT_STARTS_WITH, giroValue);
                inner.Add(new BinaryFilterExpressionItem(notStarts, BinaryFilterOperatorType.BOOLEAN_AND));
            }

            expressions.Add(new BinaryFilterExpressionItem(inner, BinaryFilterOperatorType.BOOLEAN_OR));
        }

        return expressions;
    }

    private static void ApplyFilterToActiveView(BinaryFilterExpressionCollection expressions)
    {
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var attributesPath = Path.Combine(modelPath, "attributes");
        var filterNameFull = Path.Combine(attributesPath, "PT_SubsystemSelection");

        var filter = new Filter(expressions);
        filter.CreateFile(FilterExpressionFileType.OBJECT_GROUP_VIEW, filterNameFull);

        var activeView = ViewHandler.GetActiveView();
        if (activeView != null)
        {
            activeView.ViewFilter = "PT_SubsystemSelection";
            activeView.Modify();
        }
    }

    public static void SaveSelectionState(List<Teilsystem> teilsysteme)
    {
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var filePath = Path.Combine(modelPath, "attributes", "PT_SubsystemSelection_state.txt");

        using (var writer = new StreamWriter(filePath))
        {
            foreach (var t in teilsysteme)
                writer.WriteLine($"{t.Nummer}|{t.Aktiv}|{t.OhneGitterroste}|{t.OhneBestand}");
        }
    }

    public static Dictionary<int, Teilsystem> LoadSelectionState()
    {
        var result = new Dictionary<int, Teilsystem>();
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var filePath = Path.Combine(modelPath, "attributes", "PT_SubsystemSelection_state.txt");

        if (!File.Exists(filePath)) return result;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var parts = line.Split('|');
            if (parts.Length != 4) continue;

            if (int.TryParse(parts[0], out int nummer) &&
                bool.TryParse(parts[1], out bool aktiv) &&
                bool.TryParse(parts[2], out bool ohneGitterroste) &&
                bool.TryParse(parts[3], out bool ohneBestand))
            {
                result[nummer] = new Teilsystem
                {
                    Nummer = nummer,
                    Aktiv = aktiv,
                    OhneGitterroste = ohneGitterroste,
                    OhneBestand = ohneBestand
                };
            }
        }

        return result;
    }
}

public class Teilsystem
{
    public int Nummer { get; set; }
    public string Name { get; set; }
    public bool OhneGitterroste { get; set; }
    public bool OhneBestand { get; set; }
    public bool Aktiv { get; set; }
}

public class TeilsystemSelectionForm : Form
{
    private List<Teilsystem> _teilsysteme;
    private DataGridView _dataGridView;
    private ComboBox _dropdown;
    private Button _loadButton;
    private Button _saveButton;
    private Button _deleteButton;
    private Button _okButton;
    private Button _closeButton;

    public TeilsystemSelectionForm(List<Teilsystem> teilsysteme)
    {
        _teilsysteme = teilsysteme;

        Text = "Plantech GmbH - Subsystem Visualizer";
        Width = 800;
        Height = 700;
        MinimumSize = new System.Drawing.Size(800, 700);
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        this.Activated += (s, e) => this.TopMost = true;
        this.Deactivate += (s, e) => { this.TopMost = true; this.Activate(); };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10)
        };

        _dropdown = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDown };

        _loadButton = new Button { Text = "Load", Width = 100 };
        _loadButton.Click += (s, e) =>
        {
            var name = _dropdown.Text;
            if (!string.IsNullOrWhiteSpace(name))
                LoadSelectionFromFile(name);
        };

        _saveButton = new Button { Text = "Save", Width = 100 };
        _saveButton.Click += (s, e) =>
        {
            var name = _dropdown.Text;
            if (!string.IsNullOrWhiteSpace(name))
            {
                SaveCurrentSelectionToFile(name);
                LoadSavedSelections();
            }
        };

        _deleteButton = new Button { Text = "Delete", Width = 100 };
        _deleteButton.Click += (s, e) =>
        {
            var name = _dropdown.Text;
            if (string.IsNullOrWhiteSpace(name)) return;

            var result = MessageBox.Show(
                $"Do you really want to delete the saved configuration \"{name}\"?",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                DeleteSelectionFile(name);
                LoadSavedSelections();
            }
        };

        topPanel.Controls.Add(_dropdown);
        topPanel.Controls.Add(_loadButton);
        topPanel.Controls.Add(_saveButton);
        topPanel.Controls.Add(_deleteButton);

        var scrollablePanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        _dataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            DataSource = _teilsysteme
        };

        AddColumns();
        scrollablePanel.Controls.Add(_dataGridView);

        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10)
        };

        _okButton = new Button { Text = "Apply Filter", Width = 150, Height = 60 };
        _okButton.Click += (s, e) =>
        {
            var selected = new List<Teilsystem>();

            foreach (DataGridViewRow row in _dataGridView.Rows)
            {
                var t = row.DataBoundItem as Teilsystem;
                if (t == null) continue;

                t.Aktiv = Convert.ToBoolean(row.Cells["Aktiv"].Value);
                if (t.Aktiv) selected.Add(t);
            }

            Macro.ApplyFilter(selected);
            SaveCurrentSelectionState();
        };

        _closeButton = new Button { Text = "Close", Width = 150, Height = 60 };
        _closeButton.Click += (s, e) =>
        {
            SaveCurrentSelectionState();
            Close();
        };

        bottomPanel.Controls.Add(_okButton);
        bottomPanel.Controls.Add(_closeButton);

        mainLayout.Controls.Add(topPanel, 0, 0);
        mainLayout.Controls.Add(scrollablePanel, 0, 1);
        mainLayout.Controls.Add(bottomPanel, 0, 2);

        Controls.Add(mainLayout);
        LoadSavedSelections();
    }

    private void AddColumns()
    {
        _dataGridView.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "Active",
            DataPropertyName = "Aktiv",
            Width = 60,
            Name = "Aktiv"
        });

        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Subsystem No.",
            DataPropertyName = "Nummer",
            ReadOnly = true,
            Width = 100
        });

        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            DataPropertyName = "Name",
            ReadOnly = true,
            Width = 200
        });

        _dataGridView.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "Exclude Gratings",
            DataPropertyName = "OhneGitterroste",
            Width = 150,
            Name = "OhneGitterroste"
        });

        _dataGridView.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "Exclude Existing",
            DataPropertyName = "OhneBestand",
            Width = 150,
            Name = "OhneBestand"
        });
    }

    private void SaveCurrentSelectionToFile(string name)
    {
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var filePath = Path.Combine(modelPath, "attributes", $"PT_SubsystemSelection_{name}.txt");

        using (var writer = new StreamWriter(filePath))
        {
            foreach (var t in _teilsysteme)
                writer.WriteLine($"{t.Nummer}|{t.Aktiv}|{t.OhneGitterroste}|{t.OhneBestand}");
        }
    }

    private void LoadSelectionFromFile(string name)
    {
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var filePath = Path.Combine(modelPath, "attributes", $"PT_SubsystemSelection_{name}.txt");

        if (!File.Exists(filePath)) return;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var parts = line.Split('|');
            if (parts.Length != 4) continue;

            if (int.TryParse(parts[0], out int nummer) &&
                bool.TryParse(parts[1], out bool aktiv) &&
                bool.TryParse(parts[2], out bool ohneGitterroste) &&
                bool.TryParse(parts[3], out bool ohneBestand))
            {
                var t = _teilsysteme.FirstOrDefault(x => x.Nummer == nummer);
                if (t == null) continue;

                t.Aktiv = aktiv;
                t.OhneGitterroste = ohneGitterroste;
                t.OhneBestand = ohneBestand;
            }
        }

        _dataGridView.Refresh();
    }

    private void DeleteSelectionFile(string name)
    {
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var filePath = Path.Combine(modelPath, "attributes", $"PT_SubsystemSelection_{name}.txt");

        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    private void LoadSavedSelections()
    {
        var model = new Model();
        var modelPath = model.GetInfo().ModelPath;
        var attributesPath = Path.Combine(modelPath, "attributes");

        if (!Directory.Exists(attributesPath)) return;

        var files = Directory.GetFiles(attributesPath, "PT_SubsystemSelection_*.txt");
        _dropdown.Items.Clear();

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file)
                .Replace("PT_SubsystemSelection_", "");
            _dropdown.Items.Add(name);
        }
    }

    public void SaveCurrentSelectionState()
    {
        foreach (DataGridViewRow row in _dataGridView.Rows)
        {
            var t = row.DataBoundItem as Teilsystem;
            if (t == null) continue;

            t.Aktiv = Convert.ToBoolean(row.Cells["Aktiv"].Value);
            t.OhneGitterroste = Convert.ToBoolean(row.Cells["OhneGitterroste"].Value);
            t.OhneBestand = Convert.ToBoolean(row.Cells["OhneBestand"].Value);
        }

        Macro.SaveSelectionState(_teilsysteme);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        this.Activate();
        this.BringToFront();
        this.Focus();
    }
}
