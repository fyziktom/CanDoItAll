# 05-scheduler-crm-email-project-node-picker-ux

## Objective

Make the Scheduler UI practical for Office365 email-watch workflows.

## UX Requirements

When selected target workflow has input parameter descriptors:

- Render typed fields above the raw JSON editor.
- Keep raw JSON as an advanced view or synchronized preview.
- Allow manual email entry.
- Allow selecting an email from CRM/contact directory.
- Allow selecting Office365 connection when multiple connections exist.
- Allow typing/selecting processed category.
- Allow selecting target project.
- Allow selecting parent project-structure node after project selection.
- Allow a quick interval selector:
  - every 15 minutes;
  - every 30 minutes;
  - every 1 hour;
  - every 2 hours;
  - custom Quartz CRON.
- Show generated CRON expression and human description.

## Provider Architecture

Introduce narrow option providers to avoid heavy compile-time coupling:

```csharp
public interface ISchedulerWorkflowInputOptionProvider
{
    WorkflowInputParameterKind Kind { get; }
    ValueTask<IReadOnlyList<SchedulerWorkflowInputOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionRequest request,
        CancellationToken cancellationToken);
}
```

Provider examples:

- CRM contact email provider.
- Office365 connection provider.
- Office365 category provider.
- Project provider.
- Project node provider scoped by selected project.

## Accessibility / Mobile

- Must be usable on narrow/mobile layout.
- Project node picker must support search.
- CRM picker must show name + email.
- Required fields must show validation errors before save.

## Tests

- Component test: selecting Office365 template renders typed form.
- Component test: choosing CRM contact updates `$.emailAddress`.
- Component test: choosing project loads/selects node options.
- Component test: every-two-hours quick preset creates valid Quartz expression.
- Browser proof on `/scheduler` desktop and narrow viewport.
