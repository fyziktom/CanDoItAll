# Two-instance shared-provider UI acceptance

Executed: 2026-08-26 (America/La_Paz) / 2026-08-27 UTC

## Scope

- Shared instance: `http://127.0.0.1:5210`, Docker image
  `candoitall-shared-providers-ui:spui-final-20260826-9`.
- Desktop/client instance: `http://127.0.0.1:5212`, the same application image, with a
  separate database and data volume.
- Deterministic upstream: `http://127.0.0.1:5213`, Docker image
  `candoitall-shared-providers-upstream-ui:spui-upstream-fix-20260826-9`.
- Existing developer instance: `http://127.0.0.1:5032`, PID 21860, left running and
  untouched by the container lifecycle.

The shared instance was configured through the browser UI with JWT API access, one
Ollama chat provider, one OpenAI chat provider, and one OpenAI image-generation
provider. All three providers were published through the provider Sharing tab. The
client started with no persisted provider definitions other than the seeded runtime
profile, then used the UI to create a shared source, test the connection, discover the
catalog, and import all three providers.

## Browser proof

| Evidence | Result |
| --- | --- |
| `01-shared-api-token-issued.png` | API token created through shared-instance settings; displayed token is redacted in the screenshot. |
| `02-shared-three-providers-published.png` | Ollama chat, OpenAI chat, and OpenAI image providers are published. |
| `03-client-empty-provider-catalog-source-controls.png` | Client source controls remain available with no selected persisted provider. |
| `04-client-three-shared-providers-imported.png` | Three source-managed providers imported through the client UI. |
| `05-client-agents-created-from-shared-providers.png` | Ollama and multimedia agents bound to imported providers. |
| `06-client-ollama-shared-chat.png` | Agent chat completed through the shared Ollama provider. |
| `07a-client-shared-image-generation-approval.png` | Image tool approval surfaced in the client UI. |
| `07-client-shared-image-generation.png` | Shared OpenAI image generation completed and returned `shared-provider-ui/generated.png`. |
| `08-client-shared-image-analysis.png` | The supplied screenshot was relayed as vision input and analyzed successfully. |

The external Playwright acceptance passed twice without deleting or recreating the
source/import/agent records between runs. This proves the UI setup and reconciliation
path are repeatable rather than only working on a fresh database.

## Independent transport and usage proof

After resetting the fixture capture buffer, the repeat run produced eight HTTP 200
upstream requests:

- four Ollama/OpenAI-compatible chat requests for relay preflight and the Ollama agent;
- three OpenAI chat requests for image tool selection, post-tool completion, and vision;
- one OpenAI image-generation request;
- the final vision request contained both `image_url` and a `data:image` URL; its capture
  was intentionally truncated at the fixture's 64 KiB evidence boundary after those
  markers were recorded.

For the same run, the shared-instance `Workspace_SharedProviderInvocations` ledger
contained only successful outcomes:

| Operation | Upstream model | Count | Input tokens | Output tokens | Images |
| --- | --- | ---: | ---: | ---: | ---: |
| ChatCompletions | `e2e-ollama` | 4 | 20 | 12 | 0 |
| ChatCompletions | `e2e-duplicate-model` | 3 | 15 | 9 | 0 |
| ImageGenerations | `e2e-openai-image` | 1 | 0 | 0 | 1 |

The generated client artifact exists at
`/data/workspace/shared-provider-ui/generated.png` and is 68 bytes.

## Defects found and repaired

1. The production OpenAI chat support descriptor advertised `SupportsVisionInput=false`.
   Catalog publication therefore omitted `vision-input`, and the client materialized the
   imported chat profile without vision support. The descriptor now advertises vision and
   a focused unit regression locks the contract.
2. The deterministic upstream initially selected image intent from the wrong message
   scope. It now examines user-role messages only, which accepts the human request while
   ignoring system instructions and the later application-context message.
3. The fixture's 1 MiB request limit was below the production shared-provider contract.
   It now accepts the production-aligned 16 MiB maximum while retaining a 64 KiB capture
   limit, allowing realistic vision payloads without unbounded evidence retention.

## Validation

- Two-instance Playwright UI acceptance: 1/1 passed, then 1/1 passed again.
- Focused unit tests: 62/62 passed.
- Focused source-sync and OpenAI compatibility integration tests: 38/38 passed.
- Existing 5032 agents/chat non-mutating Playwright smoke: 2/2 passed.
- Provider-boundary architecture tests: 11/11 passed.
- Final provider-boundary script: passed with zero violations.
- `git diff --check`: passed (only existing CRLF conversion warnings were reported).
- Final health checks: ports 5210, 5212, 5213, and 5032 all returned HTTP 200.
- Final-window terminal-error scan: zero matching lines in shared, client, and upstream logs.

The bundle preparation validator was also invoked, but it is intentionally a prepared-state
gate: it requires the original `SB00=READY`/downstream-locked status and immutable preparation
manifest. It correctly rejects the evolved execution status and new post-preparation evidence,
so it is not represented as a closure pass. The executable boundary guard and focused behavior
tests above are the applicable post-change gates.

After validation, 18 stopped rollback containers were removed. The live containers, images,
databases, and data volumes were retained. The temporary host directory
`.artifacts/spui-runtime-secrets` was deleted exactly and is not recoverable; the running
fixture continues to use its isolated Docker secret volume.

This evidence satisfies the operator-requested two-application acceptance lane. It does
not replace SB07's separately frozen three-application proof contract, so SB07 remains
blocked until that contract is executed or explicitly amended.
