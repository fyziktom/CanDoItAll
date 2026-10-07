namespace CanDoItAll.Modules.Collaboration;

public enum CollaborationInboxItemKind {
    Notification = 0,
    Escalation = 1
}

public enum CollaborationContextKind {
    Manual = 0,
    ProcessRun = 1,
    ProcessLaunch = 2,
    AutomationSignal = 3
}

public enum CollaborationMessageAuthorKind {
    User = 0,
    Agent = 1,
    Role = 2,
    System = 3
}

public enum CollaborationMessageKind {
    Standard = 0,
    Escalation = 1,
    System = 2
}

public enum CollaborationParticipantKind {
    User = 0,
    Agent = 1,
    Role = 2,
    System = 3
}

public enum CollaborationThreadState {
    Open = 0,
    Closed = 1
}
