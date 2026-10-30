using System.Diagnostics;
using FlaUI.Core;
using FlaUI.UIA3;

namespace AutoManualTester;

public sealed class MainForm : Form
{
    private readonly TextBox _exePath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button _browse = new() { Text = "Select application...", AutoSize = true };
    private readonly Button _launch = new() { Text = "Launch / Attach", AutoSize = true, Enabled = false };
    private readonly Button _inspect = new() { Text = "Inspect controls", AutoSize = true, Enabled = false };
    private readonly Button _stop = new() { Text = "Stop target", AutoSize = true, Enabled = false };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _status = new() { Text = "Select a Windows application to begin.", AutoSize = true };
    private string? _selectedPath;
    private Process? _targetProcess;
    private FlaUI.Core.Application? _automationApp;
    private UIA3Automation? _automation;

    public MainForm()
    {
        Text = "Auto Manual Tester";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(850, 550);

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(10), ColumnCount = 4, RowCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(_exePath, 0, 0);
        top.SetColumnSpan(_exePath, 1);
        top.Controls.Add(_browse, 1, 0);
        top.Controls.Add(_launch, 2, 0);
        top.Controls.Add(_inspect, 3, 0);
        top.Controls.Add(_stop, 3, 1);
        top.Controls.Add(_status, 0, 1);
        top.SetColumnSpan(_status, 3);

        _grid.Columns.Add("Name", "Name");
        _grid.Columns.Add("Type", "Control Type");
        _grid.Columns.Add("AutomationId", "Automation ID");
        _grid.Columns.Add("Enabled", "Enabled");
        _grid.Columns.Add("HelpText", "Help Text");

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 450 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_log);
        Controls.Add(split);
        Controls.Add(top);

        _browse.Click += (_, _) => Browse();
        _launch.Click += (_, _) => LaunchTarget();
        _inspect.Click += (_, _) => InspectControls();
        _stop.Click += (_, _) => StopTarget();
        FormClosing += (_, _) => DisposeAutomation();
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", Title = "Choose the application to test" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _selectedPath = dialog.FileName;
        _exePath.Text = _selectedPath;
        _launch.Enabled = true;
        Log($"Selected: {_selectedPath}");
    }

    private void LaunchTarget()
    {
        if (string.IsNullOrWhiteSpace(_selectedPath)) return;
        try
        {
            if (_targetProcess is { HasExited: false })
            {
                _automationApp = FlaUI.Core.Application.Attach(_targetProcess.Id);
            }
            else
            {
                _targetProcess = Process.Start(new ProcessStartInfo(_selectedPath) { UseShellExecute = true });
                if (_targetProcess is null) throw new InvalidOperationException("Windows did not return a process handle.");
                _targetProcess.WaitForInputIdle(10000);
                _automationApp = FlaUI.Core.Application.Attach(_targetProcess.Id);
            }

            _automation?.Dispose();
            _automation = new UIA3Automation();
            _inspect.Enabled = true;
            _stop.Enabled = true;
            _status.Text = $"Attached to PID {_targetProcess.Id}";
            Log($"Attached to process {_targetProcess.ProcessName} (PID {_targetProcess.Id}).");
        }
        catch (Exception ex)
        {
            Log($"Launch/attach failed: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Unable to launch or attach", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void InspectControls()
    {
        if (_automationApp is null || _automation is null) return;
        try
        {
            var window = _automationApp.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
            if (window is null)
            {
                Log("No accessible main window was found.");
                return;
            }

            _grid.Rows.Clear();
            var elements = window.FindAllDescendants();
            foreach (var element in elements)
            {
                try
                {
                    var info = element.Properties;
                    _grid.Rows.Add(
                        Safe(() => info.Name.ValueOrDefault),
                        Safe(() => element.ControlType.ToString()),
                        Safe(() => info.AutomationId.ValueOrDefault),
                        Safe(() => info.IsEnabled.ValueOrDefault.ToString()),
                        Safe(() => info.HelpText.ValueOrDefault));
                }
                catch
                {
                    // Ignore controls that disappear while the target UI is changing.
                }
            }

            _status.Text = $"Inspected {elements.Length} controls";
            Log($"Inspection completed. Found {elements.Length} descendant controls.");
        }
        catch (Exception ex)
        {
            Log($"Inspection failed: {ex.Message}");
        }
    }

    private static string Safe(Func<string?> getter)
    {
        try { return getter() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private void StopTarget()
    {
        try
        {
            if (_targetProcess is { HasExited: false })
            {
                var answer = MessageBox.Show(this,
                    "This will close the selected application. Unsaved work in that application may be lost. Continue?",
                    "Confirm close", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
                _targetProcess.CloseMainWindow();
            }
            Log("Close request sent to target application.");
        }
        catch (Exception ex)
        {
            Log($"Could not close target: {ex.Message}");
        }
    }

    private void Log(string message) => _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    private void DisposeAutomation()
    {
        _automation?.Dispose();
        _automation = null;
        _automationApp = null;
    }
}