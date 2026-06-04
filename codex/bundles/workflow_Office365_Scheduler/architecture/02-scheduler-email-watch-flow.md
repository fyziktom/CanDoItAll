# Scheduler Email Watch Flow

```mermaid
sequenceDiagram
    participant User
    participant SchedulerUI
    participant Scheduler
    participant WorkflowRuntime
    participant Office365
    participant ProjectStructure

    User->>SchedulerUI: Select workflow template "Office365 email address -> tasks"
    User->>SchedulerUI: Pick CRM contact/email, project, parent node, interval, processed category
    SchedulerUI->>Scheduler: Save plan with typed input JSON + CRON
    Scheduler->>WorkflowRuntime: Start workflow on each trigger
    WorkflowRuntime->>Office365: Download one unprocessed message by address
    alt no matching email
        Office365-->>WorkflowRuntime: count=0
        WorkflowRuntime-->>Scheduler: Completed / NoMessages
    else matching email
        Office365-->>WorkflowRuntime: message
        WorkflowRuntime->>WorkflowRuntime: LLM summary or task extraction
        WorkflowRuntime->>ProjectStructure: Create/update summary asset or task nodes
        WorkflowRuntime->>Office365: Add processed category
        WorkflowRuntime-->>Scheduler: Completed / Processed
    end
```

## Key detail

The category mark must happen after the project write. If project write fails, the email stays unprocessed and can be retried. Idempotency must prevent duplicate project writes on retry.
