# Runtime state compatibility v2

## Current semantic problem

The v1 envelope has one `ContextPolicyFingerprint`, but the value is the model-context digest. Toolset fingerprint includes only tool names. The envelope stores history mode and adapter package version without comparing them. Provider conversation detection reads the wrapper rather than the inner payload.

## Target v2 envelope

```text
adapterId
schemaVersion = 2
adapterPackageVersion
adapterStateFormatVersion
providerProfileId
providerTransport
model
providerConversationStrategy
configuredHistoryMode
effectiveHistoryMode
authorityPolicyFingerprint
modelContextFingerprint
capabilityPolicyFingerprint
toolContractFingerprint
createdAtUtc
payloadJson
```

## Compatibility order

1. Is stored text absent, envelope, or strictly recognized legacy MAF payload?
2. Is adapter/schema readable?
3. Is a registered migration required?
4. Does provider identity/transport/model/conversation strategy match?
5. Does effective history mode allow native restore?
6. Does authority policy match?
7. Does capability/tool contract match?
8. Unwrap through owning adapter.
9. Inspect native MAF payload only after compatibility judgment.
10. Restore, named canonical replay, or fail closed.

## Tool contract fingerprint

Hash stable, normalized data:

- tool name/stable ID
- normalized input schema
- operation classification
- approval requirement/mode
- owning runtime provider key and contract version
- resource-scope semantics relevant to continuation

Do not hash display descriptions, order-insensitive lists without normalization, timestamps, or secrets.
