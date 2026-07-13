# Runtime Scope Policy Example

This is an illustrative shape for execution design. It is not a required final API.

```json
{
  "capabilityScope": {
    "directives": [
      {
        "effect": "Deny",
        "target": {
          "kind": "CapabilityKey",
          "capabilityKind": "Skill",
          "key": "dotnet-development"
        },
        "reason": "Management-only step must not receive development implementation guidance."
      },
      {
        "effect": "Require",
        "target": {
          "kind": "RuntimeToolName",
          "name": "workspace_write_file"
        },
        "reason": "Step must write the manager summary artifact."
      }
    ],
    "instructions": [
      {
        "key": "manager-summary-contract",
        "attachmentMode": "WhenRequiredCapabilityAvailable",
        "requiredCapability": {
          "kind": "RuntimeToolName",
          "name": "workspace_write_file"
        },
        "text": "Write the manager summary to the managed artifact ref named by this step."
      }
    ]
  }
}
```

Implementation must not store this as unvalidated raw JSON only. Template DTOs and runtime records need typed effects, target kinds, identifiers, and validation.
