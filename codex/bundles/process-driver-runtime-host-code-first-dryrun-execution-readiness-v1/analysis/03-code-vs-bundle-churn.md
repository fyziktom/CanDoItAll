# Bundle Churn Analysis And Code-First Rule

## Observed problem

The last bundle looked large, but the actual implementation delta was small relative to the amount of generated bundle/proof/subbundle artifacts.

Examples:
- 60 generated subbundle README files alone create thousands of lines.
- Execution report/proof manifests/transcripts dominate the changed-file list.
- The actual source changes are concentrated in a handful of process-module files plus one integration test file.

## Required next behavior

Codex must do fewer, larger, coherent implementation slices.

The next implementation must record both stats:

```powershell
git diff --stat <base> HEAD -- src tests docs
git diff --stat <base> HEAD -- codex/bundles
```

Critical gates must fail if the implementation is mostly new bundle/proof prose while production/test code did not materially move.

## Code-first acceptance rule

For this bundle, "done" cannot be claimed unless:
- production/test code changes are the primary implementation output,
- every critical gate includes source-level changed-file hashes,
- report/proof files cite command transcripts but do not replace source changes,
- no repeated 79-line boilerplate subbundle READMEs are generated,
- no test depends on concrete transient `codex/bundles/<bundle-name>` paths.

## Proof economy rule

Use proof manifests only at critical gates. Non-critical subbundles must record concise status rows and link to the next critical gate proof, not generate full fake-proof forests.
