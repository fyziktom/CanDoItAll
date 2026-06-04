# Target Solution

## New Office365 executor

Add:

- `Office365DownloadByAddressWorkflowExecutor`
- `Office365GraphClient.DownloadOneUnprocessedMessageByAddressAsync`
- `Office365PluginConstants.DownloadByAddressExecutorId`
- `Office365MessageAddressFilterSettings`
- Plugin descriptor entry and DI registration.

Executor ID suggestion:

```text
office365.message-by-address-unprocessed
```

## Template workflows

Add `Templates/Workflows/workflows/office365-email-watch-workflows.yaml` with at least:

1. `office365-email-address-summary-to-project`
2. `office365-email-address-tasks-to-project`

Both workflows must:

- Start from scheduler/manual input JSON.
- Download one unprocessed matching email.
- Branch no-message to a successful no-op End.
- Run LLM processing only when a message exists.
- Store result to project structure.
- Mark the message processed only after the project write succeeds.
- End with a compact JSON result suitable for scheduler history.

## Scheduler typed workflow input

Add a durable parameter contract so a workflow/template can expose input fields to Scheduler:

```csharp
public sealed record WorkflowInputParameterDescriptor(
    string Key,
    WorkflowInputParameterKind Kind,
    string Label,
    string HelpText,
    bool IsRequired,
    string DefaultValue,
    string JsonPath,
    IReadOnlyList<WorkflowInputParameterOption> Options);
```

Kinds should include at least:

- Text
- EmailAddress
- CrmContactEmail
- Project
- ProjectNode
- CategoryName
- Number
- Boolean
- Json

The Scheduler should render a typed form when the selected workflow has descriptors and fall back to JSON for advanced editing.

## CRM/project/node option providers

Avoid hard-coupling Scheduler to large modules by introducing narrow provider interfaces:

```csharp
public interface ISchedulerWorkflowInputOptionProvider
{
    string Kind { get; }
    ValueTask<IReadOnlyList<SchedulerWorkflowInputOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionRequest request,
        CancellationToken cancellationToken);
}
```

Implement providers in appropriate modules:

- CRM contact/email provider in CrmHr module or a small bridge.
- Project provider / project node provider through project-structure services.
- Office365 connection/category provider through Plugins module.

## Polling idempotency

Introduce a stable source key for project writes:

```text
office365:{messageId}:summary
office365:{messageId}:tasks
```

Use it in metadata and write paths to skip or update existing outputs instead of creating duplicates on retry.
