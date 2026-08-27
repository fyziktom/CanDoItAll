# SB03 governed proof manifest

Status: Completed. Owned input: N005 / R5. Invariants: LOCAL-UI-ACCESS and API-BOUNDARY.

- Changed-file manifest: bundle://proof/SB03/changed-files.csv.
- Exact initial captures: bundle://proof/SB03/before-hashes.csv. Late-added files use
  explicitly labelled baseline Git blob provenance, not invented pre-edit capture.
- Failing-first transcript: bundle://proof/SB03/transcripts/regression-red.txt.
- Passing transcript: bundle://proof/SB03/transcripts/component-final.txt.
- Passing HTTP security transcript: bundle://proof/SB03/transcripts/integration-final.txt.
- Passing browser transcript: bundle://proof/SB03/transcripts/browser-final.txt.
- Anti-stub audit transcript: bundle://proof/SB03/transcripts/source-audit.txt.
- Semantic contract: bundle://proof/SB03/semantic-invariants.md.
- Architecture evidence: bundle://proof/SB03/codeanalytics-summary.json.
- Deployment: bundle://proof/SB03/transcripts/docker-build.txt and bundle://proof/SB03/transcripts/deploy.txt.
- Real usage and health: bundle://proof/SB03/transcripts/runtime-evidence.txt.
- Artifact hashes: bundle://proof/SB03/proof-artifacts.csv.

Final scope: 38 component, 9 HTTP/API and 3 real browser cases pass. The four red cases
failed on Assert.True(authentication), not build failure. The initial missing test import,
incorrect discovery count and source-local versus imported-provider helper assumption
remain in earlier failed transcripts. They are not counted as passing proof.

The browser contexts start with no cookies and never issue/inject a browser JWT. Each
case asserts zero browser Authorization headers, definition save/reopen/activation,
actual response, full navigation reload and anonymous API/file HTTP 401. Source requests
still use the configured shared-provider JWT held server-side by the client.

Visual proof: bundle://proof/SB03/denied-before.png, definitions-after.png,
new-definition-dialog.png, openai-chat-viewport.png, ollama-chat-viewport.png and
source-chat-viewport.png. Normal and dialog screenshots were inspected at 1920x1080.
The three browser result JSON files and answer text files are in bundle://proof/SB03/browser.
Full-page browser captures include existing offscreen blank document space; viewport
captures are the visual-review authority. This phase makes no layout redesign claim.

Both containers run local-ui-access-20260827-1, image
sha256:b13994b6a0b08bae40302f5cc36bfc0f352c17c1941e8fc7bfb2ed6587702ed2.
Their loopback bindings and explicit gateway 172.31.0.1 are verified. Volumes and rollback
containers remain intact; 5032 is untouched. Historical SB01/SB02 proof remains historical;
SB03 owns the new source/configuration state and normal-browser acceptance.
