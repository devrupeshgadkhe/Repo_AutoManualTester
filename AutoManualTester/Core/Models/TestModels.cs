namespace AutoManualTester.Core.Models;

public enum TestStatus
{
    Draft,
    Planned,
    Running,
    Passed,
    Failed,
    Blocked,
    Cancelled
}

public enum FindingSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public enum FindingConfidence
{
    Potential,
    Confirmed
}

public enum AutomationPlatform
{
    WindowsDesktop,
    Web,
    Android
}

public sealed record ApplicationTarget(
    string Name,
    AutomationPlatform Platform,
    string? ExecutablePath = null,
    int? ProcessId = null,
    string? StartUrl = null,
    string? AndroidPackageId = null);

public sealed record TestAction(
    string Description,
    string Operation,
    IReadOnlyDictionary<string, string> Parameters,
    bool IsDestructive = false,
    bool HasExternalSideEffect = false);

public sealed record TestAssertion(
    string Description,
    string Kind,
    IReadOnlyDictionary<string, string> Expected);

public sealed record TestCase(
    Guid Id,
    string Name,
    string Objective,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<TestAction> Actions,
    IReadOnlyList<TestAssertion> Assertions,
    FindingSeverity Risk = FindingSeverity.Low);

public sealed record StepResult(
    int Sequence,
    string Action,
    TestStatus Status,
    string? Expected,
    string? Actual,
    string? EvidencePath,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string? Error = null);

public sealed record TestFinding(
    Guid Id,
    string Title,
    string Description,
    FindingSeverity Severity,
    FindingConfidence Confidence,
    string? EvidencePath,
    DateTimeOffset DetectedAt);

public sealed record TestRunResult(
    Guid RunId,
    ApplicationTarget Target,
    TestStatus Status,
    IReadOnlyList<StepResult> Steps,
    IReadOnlyList<TestFinding> Findings,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
