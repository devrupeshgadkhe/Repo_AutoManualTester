using AutoManualTester.Core.Models;

namespace AutoManualTester.Core.Engine;

public sealed record ObservedElement(
    string Name,
    string ControlType,
    string AutomationId,
    bool IsEnabled,
    string? HelpText,
    string? Value,
    string? BoundingRectangle);

public sealed record ApplicationObservation(
    string WindowTitle,
    IReadOnlyList<ObservedElement> Elements,
    string? ScreenshotPath,
    DateTimeOffset ObservedAt);

public sealed record ActionExecutionResult(
    bool Succeeded,
    string? ActualResult,
    string? EvidencePath,
    string? Error = null);

public sealed record AssertionResult(
    bool Passed,
    string Expected,
    string Actual,
    string? EvidencePath,
    string? Error = null);

public interface IApplicationAutomationAdapter : IAsyncDisposable
{
    AutomationPlatform Platform { get; }
    Task AttachAsync(ApplicationTarget target, CancellationToken cancellationToken);
    Task<ApplicationObservation> ObserveAsync(CancellationToken cancellationToken);
    Task<ActionExecutionResult> ExecuteAsync(TestAction action, CancellationToken cancellationToken);
    Task<AssertionResult> VerifyAsync(TestAssertion assertion, CancellationToken cancellationToken);
    Task DetachAsync(CancellationToken cancellationToken);
}

public interface ITestPlanner
{
    Task<IReadOnlyList<TestCase>> CreatePlanAsync(
        ApplicationTarget target,
        IReadOnlyList<ApplicationObservation> observations,
        CancellationToken cancellationToken);
}

public interface IAIProvider
{
    string ProviderName { get; }
    Task<string> CompleteAsync(string systemInstruction, string userInput, CancellationToken cancellationToken);
}
