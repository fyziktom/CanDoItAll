# 04-scheduler-workflow-input-contract-and-template-parameter-schema

## Objective

Introduce a reusable workflow input parameter schema so Scheduler can render a form instead of raw JSON for common workflow templates.

## Problem

`SchedulerPlan.InputJson` is currently a raw JSON string. This is technically enough for automation, but not enough for normal users who need to schedule "check this person's email every two hours and put results under this project node."

## Target Model

Add a model similar to:

```csharp
public enum WorkflowInputParameterKind
{
    Text,
    EmailAddress,
    CrmContactEmail,
    Office365Connection,
    OutlookCategory,
    Project,
    ProjectNode,
    Number,
    Boolean,
    Json
}

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

## Storage Location

Prefer template-pack metadata first:

```yaml
inputParameters:
  - key: emailAddress
    kind: CrmContactEmail
    label: Sender email
    jsonPath: $.emailAddress
    isRequired: true
  - key: projectId
    kind: Project
    label: Target project
    jsonPath: $.projectId
    isRequired: true
  - key: nodeId
    kind: ProjectNode
    label: Parent node
    jsonPath: $.nodeId
    isRequired: false
```

When a workflow definition is saved from a template, preserve this parameter schema in workflow metadata. If the current workflow model has no metadata field, add a narrowly scoped metadata/extension field rather than overloading description text.

## Scheduler Contract

Add service:

```csharp
public interface ISchedulerWorkflowInputSchemaService
{
    ValueTask<SchedulerWorkflowInputSchema> ResolveSchemaAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId,
        CancellationToken cancellationToken);
}
```

## Tests

- Template loader maps `inputParameters`.
- Saved workflow definition preserves parameter descriptors.
- Scheduler resolves schema for selected workflow.
- Invalid required parameter prevents saving schedule.
- Raw JSON fallback still works for workflows without schema.
