# 02-office365-message-by-address-unprocessed-executor

## Objective

Add a new Office365 workflow executor that downloads at most one newest unprocessed email matching a configured email address.

## New Executor

Suggested ID:

```text
office365.message-by-address-unprocessed
```

Suggested classes/files:

- `Office365DownloadByAddressWorkflowExecutor`
- `Office365MessageAddressWorkflowExecutorSettings`
- `Office365GraphClient.DownloadOneUnprocessedMessageByAddressAsync`
- `Office365PluginConstants.DownloadByAddressExecutorId`
- descriptor entry in `Office365BundledPlugin`
- DI registration in `Office365PluginServiceCollectionExtensions`

## Settings

```csharp
public sealed record Office365MessageAddressWorkflowExecutorSettings
{
    public string ConnectionId { get; init; } = string.Empty;
    public string EmailAddress { get; init; } = string.Empty;
    public string EmailAddressJsonPath { get; init; } = "$.emailAddress";
    public string ProcessedCategory { get; init; } = "CanDoItAllProcessed";
    public string MailFolderId { get; init; } = string.Empty;
    public Office365EmailAddressMatchMode MatchMode { get; init; } = Office365EmailAddressMatchMode.FromOrSenderEquals;
    public int MaxCandidateMessages { get; init; } = 25;
    public int LookbackHours { get; init; } = 336;
    public int MaxBodyCharacters { get; init; } = 60000;
    public bool IncludeBody { get; init; } = true;
    public Office365NoMessageBehavior NoMessageBehavior { get; init; } = Office365NoMessageBehavior.SuccessNoMessages;
}
```

## Required Behavior

- Validate email address format with a pragmatic non-regex-hostile check.
- Resolve `EmailAddress` from settings first, then `EmailAddressJsonPath`.
- Use delegated `Mail.Read` for download.
- Prefer server-side Graph filtering:
  - address match;
  - no processed category;
  - optional lookback.
- If Graph rejects a complex filter, use a bounded fallback candidate query and filter client-side.
- Return `count=0`, `noMessages=true`, and `route=no_messages` by default when nothing matches.
- Do not throw on no-message unless explicitly configured to fail.
- Preserve `projectId`, `nodeId`, `project`, and `runContext` from workflow input.
- Include `office365Processing.selectedMessageId`, `messageIds`, `processedCategory`, and `idempotencyKey`.

## Mark Processed Hardening

Current `Office365MarkProcessedWorkflowExecutor` requires `sourceCategory`. Extend it so source category is optional:

- if `sourceCategory` is empty, only add processed category;
- if `sourceCategory` is present, remove it and add processed category;
- preserve unrelated categories.

## Tests

- Fake Graph URL/filter test for processed category exclusion.
- Fake Graph response with matching message.
- Fake Graph response with processed category must be ignored.
- No-message success test.
- Add-only category mutation test.
- Plugin descriptor/manifest validation test.
- Preview simulation test.
