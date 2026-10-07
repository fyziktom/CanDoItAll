namespace CanDoItAll.Workbench.Execution.UiSandbox;

public enum ExecutionScenario {
    Normal,
    Empty,
    Unavailable,
    Loading,
    Error,
    InvalidInput,
    Pending,
    Accepted,
    Conflict,
    ObservationRequired
}

public static class ExecutionScenarioDescriptions {
    public static string Describe(ExecutionScenario scenario) => scenario switch {
        ExecutionScenario.Normal => "Ready for an explicit scenario action.",
        ExecutionScenario.Empty => "The native owner supplied no choices.",
        ExecutionScenario.Unavailable => "The saved choice or candidate is unavailable.",
        ExecutionScenario.Loading => "The original opening is waiting for its read.",
        ExecutionScenario.Error => "The original request failed before dispatch; a deliberate retry is available.",
        ExecutionScenario.InvalidInput => "Raw invalid input stays visible for correction.",
        ExecutionScenario.Pending => "The original operation is pending; duplicate submission is blocked.",
        ExecutionScenario.Accepted => "The accepted original identity is retained for observation.",
        ExecutionScenario.Conflict => "The original source changed; a successor must not receive its completion.",
        ExecutionScenario.ObservationRequired => "Observe the original accepted effect before taking further action.",
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };
}
