namespace CanDoItAll.Processes.Contracts;

public enum ProcessRunStatus
{
    Draft,
    Active,
    Blocked,
    Completed,
    Cancelled,
    Failed
}

public enum ProcessStepRunStatus
{
    Pending,
    Ready,
    InProgress,
    WaitingApproval,
    Blocked,
    Completed,
    Refused,
    Skipped,
    Failed
}

public enum ProcessStepKind
{
    Start = 0,
    Work = 1,
    Decision = 2,
    Approval = 3,
    Review = 4,
    Delivery = 5,
    End = 6,
    Subprocess = 7
}
