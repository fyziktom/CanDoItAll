using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

internal sealed class ProcessDecisionReviewTestData {
    internal ProcessDecisionReviewTestData() {
        ProcessRunId root = new(Id(1));
        ProcessRunId run = new(Id(2));
        ProcessStepInstanceId step = new(Id(3));
        ProcessDecisionChildAddress address = new(root, run, step, new(new(Id(6)), 1), new(Id(8)));
        Result = new(address, new("review-fixture"), new(Id(7)), new(Id(9)), StrategyOutcome.Succeeded,
            new(12, ProcessDecisionChildOutcome.Accepted, new(Id(10))), new(12, new(TimeSpan.FromSeconds(2).Ticks, 0)));
        var waiting = new StrategyResultReceipt(step, Result.StrategyId, Result.WaitingResultKey, StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked, Hash('a')) { AppliedSequence = 4, ExecutionRunId = new(address.ChildId.Value) };
        State = new(root, run, new(Id(4)), Hash('b'), ProcessRuntimeStatus.Active,
            [new(step, new(Id(5)), ProcessRuntimeStepStatus.Blocked, true, 1, new HashSet<ProcessStepInstanceId>(),
                new HashSet<ArtifactSlotId>(), null, null)],
            [new(new(Id(12)), step, new("review-dispatcher"), DispatchClaimStatus.Completed, 1, Now, Now.AddMinutes(1), null, Result.WaitingResultKey)],
            [waiting], new HashSet<ArtifactSlotId>(), Now) { LaunchAdmissionId = new(Id(11)) };
        Context = new(RuntimeCommandId.New(), new(ProcessEventActorKind.Manager, new("review-fixture")), new("review-fixture"), Now.AddSeconds(1));
    }

    internal static DateTimeOffset Now { get; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    internal ProcessRuntimeStateSnapshot State { get; set; }
    internal RuntimeCommandContext Context { get; }
    internal ProcessDecisionReviewResult Result { get; set; }
    internal ProcessRuntimeMutation Complete()
        => ProcessDecisionReviewCompletion.Prepare(State, Context, Result);
    internal static Guid Id(int value) => new(value, 0, 0, new byte[8]);
    internal static string Hash(char value) => "sha256:" + new string(value, 64);
}
