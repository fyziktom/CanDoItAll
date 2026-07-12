namespace CanDoItAll.Modules.Processes;

internal enum ProcessCompletionGateContributionStage
{
    BeforeToolReceiptEvidence,
    AfterToolReceiptEvidence
}

internal interface IProcessCompletionGateContribution
{
    string ContributionKey { get; }

    int Order { get; }

    ProcessCompletionGateContributionStage Stage => ProcessCompletionGateContributionStage.AfterToolReceiptEvidence;

    ProcessCompletionIssue? Validate(ProcessCompletionGateContext context);
}
