using System.Diagnostics;
using AutoManualTester.Core.Models;
using AutoManualTester.Core.Safety;

namespace AutoManualTester.Core.Engine;

public sealed class TestOrchestrator
{
    private readonly IApplicationAutomationAdapter _adapter;
    private readonly ExecutionPolicy _policy;
    private readonly IActionApproval _approval;

    public TestOrchestrator(
        IApplicationAutomationAdapter adapter,
        ExecutionPolicy policy,
        IActionApproval approval)
    {
        _adapter = adapter;
        _policy = policy;
        _approval = approval;
        _policy.Validate();
    }

    public async Task<TestRunResult> RunAsync(
        ApplicationTarget target,
        TestCase testCase,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var steps = new List<StepResult>();
        var findings = new List<TestFinding>();
        var status = TestStatus.Running;
        var sequence = 0;

        try
        {
            if (_adapter.Platform != target.Platform)
                throw new InvalidOperationException($"Adapter platform {_adapter.Platform} cannot test {target.Platform}.");

            await _adapter.AttachAsync(target, cancellationToken);

            foreach (var action in testCase.Actions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnforceLimits(++sequence, stopwatch.Elapsed);

                if (_policy.RequiresApproval(action) &&
                    !await _approval.RequestApprovalAsync(action, cancellationToken))
                {
                    status = TestStatus.Blocked;
                    progress?.Report($"Blocked pending approval: {action.Description}");
                    break;
                }

                if (_policy.MinimumActionInterval > TimeSpan.Zero)
                    await Task.Delay(_policy.MinimumActionInterval, cancellationToken);

                var stepStarted = DateTimeOffset.UtcNow;
                progress?.Report($"Executing {sequence}/{testCase.Actions.Count}: {action.Description}");
                var execution = await _adapter.ExecuteAsync(action, cancellationToken);
                var stepStatus = execution.Succeeded ? TestStatus.Passed : TestStatus.Failed;

                steps.Add(new StepResult(
                    sequence, action.Description, stepStatus, null,
                    execution.ActualResult, execution.EvidencePath,
                    stepStarted, DateTimeOffset.UtcNow, execution.Error));

                if (!execution.Succeeded)
                {
                    findings.Add(new TestFinding(
                        Guid.NewGuid(), $"Action failed: {action.Description}",
                        execution.Error ?? "The automation adapter could not complete the action.",
                        FindingSeverity.High, FindingConfidence.Confirmed,
                        execution.EvidencePath, DateTimeOffset.UtcNow));
                    status = TestStatus.Failed;
                    break;
                }
            }

            if (status == TestStatus.Running)
            {
                foreach (var assertion in testCase.Assertions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnforceLimits(sequence, stopwatch.Elapsed);
                    progress?.Report($"Verifying: {assertion.Description}");
                    var verified = await _adapter.VerifyAsync(assertion, cancellationToken);

                    steps.Add(new StepResult(
                        ++sequence, $"Verify: {assertion.Description}",
                        verified.Passed ? TestStatus.Passed : TestStatus.Failed,
                        verified.Expected, verified.Actual, verified.EvidencePath,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, verified.Error));

                    if (!verified.Passed)
                    {
                        findings.Add(new TestFinding(
                            Guid.NewGuid(), $"Verification failed: {assertion.Description}",
                            $"Expected: {verified.Expected}\nActual: {verified.Actual}",
                            FindingSeverity.High, FindingConfidence.Confirmed,
                            verified.EvidencePath, DateTimeOffset.UtcNow));
                    }
                }

                status = findings.Count == 0 ? TestStatus.Passed : TestStatus.Failed;
            }
        }
        catch (OperationCanceledException)
        {
            status = TestStatus.Cancelled;
        }
        catch (Exception ex)
        {
            status = TestStatus.Failed;
            findings.Add(new TestFinding(
                Guid.NewGuid(), "Test run error", ex.Message,
                FindingSeverity.High, FindingConfidence.Confirmed,
                null, DateTimeOffset.UtcNow));
        }
        finally
        {
            try { await _adapter.DetachAsync(CancellationToken.None); }
            catch (Exception ex)
            {
                progress?.Report($"Detach warning: {ex.Message}");
            }
        }

        stopwatch.Stop();
        return new TestRunResult(runId, target, status, steps, findings, started, DateTimeOffset.UtcNow);
    }

    private void EnforceLimits(int actionCount, TimeSpan elapsed)
    {
        if (actionCount > _policy.MaximumActions)
            throw new InvalidOperationException($"Execution stopped: maximum action limit ({_policy.MaximumActions}) reached.");
        if (elapsed > _policy.MaximumDuration)
            throw new TimeoutException($"Execution stopped: maximum duration ({_policy.MaximumDuration}) exceeded.");
    }
}
