# File Tools and Workspace Impact

## Conclusion

The confirmed CanDoItAll file path is not MAF Harness file access.

The application registers and composes its own:

- workspace file service;
- workspace path resolver;
- command execution service and process host;
- document-to-Markdown converter;
- image operation service;
- artifact tool service;
- capability/tool metadata;
- workspace scope and external-target policy;
- CanDoItAll.FileTools integration packages.

Therefore the MAF 1.14/1.15 change that makes `HarnessAgentOptions.FileAccessStore` opt-in does not directly remove these tools.

## Why the Distinction Matters

MAF Harness file access is a general-purpose provider backed by `AgentFileStore`. CanDoItAll tools additionally encode:

- sandbox versus project/product workspace scopes;
- path normalization and root containment;
- external target aliases;
- read-only external targets;
- product mutation requirements;
- process step allowed operations;
- script side-effect manifests;
- managed artifact references;
- command receipts and lifecycle facts;
- application telemetry and audit ownership;
- approval wrappers selected by provider capability.

Replacing this layer with Harness file access would be an architecture and security redesign, not a package migration.

## Mandatory Discovery

SB01 must search the full branch for:

```text
HarnessAgent
HarnessAgentOptions
Microsoft.Agents.AI.Harness
FileAccessStore
FileAccessProvider
FileAccessProviderOptions
DisableFileAccess
FileMemoryProvider
FileSystemAgentFileStore
```

Classify every match:

- production path;
- test/sample;
- dead/experimental;
- package-only/transitive;
- documentation/template.

No match may remain unexplained.

## Compatibility Tests

### Tool composition

For representative read-only and mutation agents, record before and after:

- exposed tool names;
- tool descriptions and schemas;
- approval wrappers;
- runtime provider/tool ownership;
- context provider count and state keys;
- duplicate tool-name detection.

### Path and scope safety

Test:

- normal relative path;
- absolute path inside root;
- `..` traversal;
- mixed separators;
- case normalization on Windows;
- symlink/junction/reparse-point escape;
- workspace root itself;
- missing file;
- large file;
- binary file;
- external alias allowed;
- external alias denied;
- external alias read-only mutation;
- process-step target-scope mismatch.

### Mutations and approvals

Test:

- write/create;
- update;
- delete;
- move/rename;
- command execution producing files;
- generated artifact write;
- governed script side effects;
- provider supporting MAF approvals;
- provider not supporting MAF approvals;
- `suppressApprovalRequirements` only in explicitly authorized paths.

### Session and concurrency

Prove:

- tool instances/state do not leak between concurrent runs;
- workspace scope from one run cannot appear in another;
- approval requests remain bound to the original run/session;
- a cached immutable blueprint does not retain live file service or authorization state.

## Optional Harness Use

A future isolated coding-harness feature may deliberately use MAF `HarnessAgent` with:

- an explicitly rooted `FileAccessStore`;
- explicit `FileAccessProviderOptions`;
- no overlapping CanDoItAll file tool names;
- a separate threat model and test suite.

That work is explicitly out of the 1.15 compatibility pass.
