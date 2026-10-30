namespace AutoManualTester.Core.Safety;

public sealed record ExecutionPolicy
{
    public int MaximumActions { get; init; } = 100;
    public TimeSpan MaximumDuration { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MinimumActionInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public bool AllowDestructiveActions { get; init; }
    public bool AllowExternalSideEffects { get; init; }

    public void Validate()
    {
        if (MaximumActions is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(MaximumActions));
        if (MaximumDuration <= TimeSpan.Zero || MaximumDuration > TimeSpan.FromHours(4))
            throw new ArgumentOutOfRangeException(nameof(MaximumDuration));
        if (MinimumActionInterval < TimeSpan.Zero || MinimumActionInterval > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(MinimumActionInterval));
    }

    public bool RequiresApproval(Core.Models.TestAction action) =>
        (action.IsDestructive && !AllowDestructiveActions) ||
        (action.HasExternalSideEffect && !AllowExternalSideEffects);
}

public interface IActionApproval
{
    ValueTask<bool> RequestApprovalAsync(
        Core.Models.TestAction action,
        CancellationToken cancellationToken);
}
