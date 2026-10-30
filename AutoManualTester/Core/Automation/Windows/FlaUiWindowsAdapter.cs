using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using AutoManualTester.Core.Models;

namespace AutoManualTester.Core.Automation.Windows;

/// <summary>
/// First concrete desktop adapter. It deliberately exposes a small, auditable
/// operation vocabulary instead of allowing arbitrary code from an AI plan.
/// </summary>
public sealed class FlaUiWindowsAdapter : IApplicationAutomationAdapter
{
    private Application? _application;
    private UIA3Automation? _automation;
    private Window? _mainWindow;

    public AutomationPlatform Platform => AutomationPlatform.WindowsDesktop;

    public Task AttachAsync(ApplicationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target.Platform != AutomationPlatform.WindowsDesktop)
            throw new ArgumentException("The FlaUI adapter only supports Windows desktop targets.", nameof(target));

        if (target.ProcessId is int pid)
            _application = Application.Attach(pid);
        else if (!string.IsNullOrWhiteSpace(target.ExecutablePath) && File.Exists(target.ExecutablePath))
            _application = Application.Launch(target.ExecutablePath);
        else
            throw new InvalidOperationException("A valid executable path or process ID is required.");

        _automation = new UIA3Automation();
        _mainWindow = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(15));
        if (_mainWindow is null)
            throw new InvalidOperationException("The target process started, but no accessible main window was found.");

        return Task.CompletedTask;
    }

    public Task<ApplicationObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = RequireWindow();
        var elements = new List<ObservedElement>();

        foreach (var element in window.FindAllDescendants())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var p = element.Properties;
                elements.Add(new ObservedElement(
                    p.Name.ValueOrDefault ?? string.Empty,
                    element.ControlType.ToString(),
                    p.AutomationId.ValueOrDefault ?? string.Empty,
                    p.IsEnabled.ValueOrDefault,
                    p.HelpText.ValueOrDefault,
                    p.Value.ValueOrDefault,
                    null));
            }
            catch
            {
                // UI elements can disappear while a dynamic screen is rendering.
            }
        }

        return Task.FromResult(new ApplicationObservation(
            window.Title, elements, null, DateTimeOffset.UtcNow));
    }

    public Task<ActionExecutionResult> ExecuteAsync(TestAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var element = ResolveElement(action.Parameters);
        var operation = action.Operation.Trim().ToLowerInvariant();

        switch (operation)
        {
            case "click":
                Mouse.Click(element.GetClickablePoint());
                return Task.FromResult(new ActionExecutionResult(true, $"Clicked '{element.Name}'.", null));
            case "type":
                Mouse.Click(element.GetClickablePoint());
                Keyboard.Type(action.Parameters.TryGetValue("text", out var text) ? text : string.Empty);
                return Task.FromResult(new ActionExecutionResult(true, "Typed text into the selected control.", null));
            case "clear-and-type":
                Mouse.Click(element.GetClickablePoint());
                Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                Keyboard.Type(action.Parameters.TryGetValue("text", out var replacement) ? replacement : string.Empty);
                return Task.FromResult(new ActionExecutionResult(true, "Replaced text in the selected control.", null));
            case "press-enter":
                Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
                return Task.FromResult(new ActionExecutionResult(true, "Pressed Enter.", null));
            default:
                return Task.FromResult(new ActionExecutionResult(false, null, null,
                    $"Unsupported Windows action '{action.Operation}'."));
        }
    }

    public Task<AssertionResult> VerifyAsync(TestAssertion assertion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var kind = assertion.Kind.Trim().ToLowerInvariant();
        var parameters = assertion.Expected;

        if (kind == "element-exists")
        {
            var found = TryResolveElement(parameters, out var element);
            return Task.FromResult(new AssertionResult(found,
                "A matching UI element exists", found ? $"Found '{element!.Name}'" : "No matching element was found", null));
        }

        if (kind == "element-enabled")
        {
            var found = TryResolveElement(parameters, out var element);
            var enabled = found && element!.IsEnabled;
            return Task.FromResult(new AssertionResult(enabled,
                "Matching element is enabled", found ? $"Enabled={element!.IsEnabled}" : "Element not found", null));
        }

        if (kind == "text-contains")
        {
            var found = TryResolveElement(parameters, out var element);
            var actual = found ? (element!.Properties.Value.ValueOrDefault ?? element.Name) : "Element not found";
            var expected = parameters.TryGetValue("text", out var value) ? value : string.Empty;
            var passed = found && actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(new AssertionResult(passed, $"Text contains '{expected}'", actual, null));
        }

        return Task.FromResult(new AssertionResult(false, assertion.Description,
            $"Unsupported assertion kind '{assertion.Kind}'.", null));
    }

    public Task DetachAsync(CancellationToken cancellationToken)
    {
        _mainWindow = null;
        _automation?.Dispose();
        _automation = null;
        _application = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _automation?.Dispose();
        _automation = null;
        _mainWindow = null;
        _application = null;
        return ValueTask.CompletedTask;
    }

    private Window RequireWindow() =>
        _mainWindow ?? throw new InvalidOperationException("No Windows application is attached.");

    private AutomationElement ResolveElement(IReadOnlyDictionary<string, string> parameters)
    {
        if (TryResolveElement(parameters, out var element))
            return element!;
        throw new InvalidOperationException("Could not find a matching UI element. Provide automationId or name in the action parameters.");
    }

    private bool TryResolveElement(IReadOnlyDictionary<string, string> parameters, out AutomationElement? match)
    {
        var window = RequireWindow();
        parameters.TryGetValue("automationId", out var automationId);
        parameters.TryGetValue("name", out var name);
        match = window.FindAllDescendants().FirstOrDefault(e =>
        {
            try
            {
                var p = e.Properties;
                var idMatches = !string.IsNullOrWhiteSpace(automationId) &&
                    string.Equals(p.AutomationId.ValueOrDefault, automationId, StringComparison.Ordinal);
                var nameMatches = !string.IsNullOrWhiteSpace(name) &&
                    string.Equals(p.Name.ValueOrDefault, name, StringComparison.OrdinalIgnoreCase);
                return idMatches || nameMatches;
            }
            catch { return false; }
        });
        return match is not null;
    }
}
