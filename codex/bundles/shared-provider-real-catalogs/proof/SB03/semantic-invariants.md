# SB03 semantic invariants

## LOCAL-UI-ACCESS

- Invariant ID: LOCAL-UI-ACCESS
- Source raw note: N005 / R5, ordinary browser cannot create a Simple Chat.
- Expected behavior: a trusted local browser has exactly chat read/manage/execute access
  even on a headless OS and through explicitly configured local Docker ingress.
- Disallowed shallow implementation: injecting a browser JWT, hiding the warning, disabling
  authorization, broad private-network trust or granting the umbrella API scope.
- Failing-first test: four LocalOperatorUiAccessTests cases fail at authentication assertion
  in bundle://proof/SB03/transcripts/regression-red.trx; actual denial is captured in denied-before.md.
- Passing test: same cases plus negative controls in bundle://proof/SB03/transcripts/component-final.trx;
  three real token-free cases in bundle://proof/SB03/transcripts/browser-final.trx.
- Changed source files and hashes: bundle://proof/SB03/changed-files.csv; existing identity
  provider and scoped registration plus LocalOperatorUiOptions, no domain/provider edits.
- Production assertions: bundle://proof/SB03/transcripts/source-audit.txt.
- Red-team negative case: missing/untrusted original or effective address, spoofed headers,
  invalid trust config and authenticated read-only principals cannot gain privileges.
- Downstream dependency check: stable circuit/file access survives request disposal; actual
  shared OpenAI and Ollama chats save, execute, reload and produce complete source usage
  in bundle://proof/SB03/transcripts/runtime-evidence.txt.

## API-BOUNDARY

- Invariant ID: API-BOUNDARY
- Source raw note: R5 repair must preserve the existing canonical API authentication boundary.
- Expected behavior: local circuit identity never authenticates HTTP requests, expands an
  authenticated user's scopes or relaxes development endpoint transport checks.
- Disallowed shallow implementation: setting HttpContext.User, accepting forwarded loopback
  alone, broad API grants or making anonymous LLM/file endpoints public.
- Failing-first test: this is a preservation invariant, not a pre-existing API vulnerability.
  The UI red cases in bundle://proof/SB03/transcripts/regression-red.txt require a scoped fix;
  existing HTTP rejection must remain true after repair.
- Passing test: bundle://proof/SB03/transcripts/integration-final.trx proves HTTP 401 and
  read-only-token create HTTP 403; component-final.trx proves untrusted/forged/invalid cases;
  browser-final.trx checks live HTTP 401 even with a forwarded loopback header.
- Changed source files and hashes: bundle://proof/SB03/changed-files.csv. API policies,
  Program middleware ordering and DevelopmentEndpointAccess are unchanged.
- Production assertions: bundle://proof/SB03/transcripts/source-audit.txt.
- Red-team negative case: an untrusted original peer with a loopback effective IP remains
  anonymous; a trusted gateway with a remote effective IP remains anonymous.
- Downstream dependency check: both deployed hosts remain healthy and reject anonymous
  API/file calls while their local browsers execute genuine chats.
