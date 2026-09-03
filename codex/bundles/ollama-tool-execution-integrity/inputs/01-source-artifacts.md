# Source Artifacts

- User screenshot: [reported-state.png](reported-state.png), copied from the supplied temporary image. The visible Main node has no generated child; chat claims success on one turn and future action on the next.
- Reported run: `894e1404-3019-4221-8be6-7769c0f472ae`.
- Prior mutation attempt: `309132f3-ca46-4039-98a4-cbf5cd75516e`.
- Agent: `952b041a-aba0-385b-8e4e-494c4b21d831`; session: `94623138-38b8-4364-bd19-2b11737bd677`.
- Project: `99d218dc-701a-4fac-9305-2e040f1fb3a7`; parent: `custom:2703c1e17feb42ebb6782bf532387099`.
- [Public run capture](../analysis/public-run-evidence.json), [canonical graph](../analysis/canonical-structure.json), [current tool evidence](../analysis/894e1404-3019-4221-8be6-7769c0f472ae-tool-evidence.json), [prior tool evidence](../analysis/309132f3-ca46-4039-98a4-cbf5cd75516e-tool-evidence.json).
- [Canonical schema](../analysis/canonical-tool-schema.json), [native schema](../analysis/native-tool-schema.json), [OpenAI schema](../analysis/openai-tool-schema.json), [probe output](../analysis/probe-result.log).
- [CodeAnalytics summary](../analysis/codeanalytics-summary.json), snapshot `snap-20260903162319-aa914253`, six projects / 420 documents.
- Source baseline: CanDoItAll `40c55418e8a5acd870c5ddc1175035d6da1153a6`; initial working tree clean.
- Local raw diagnostic files remain ignored under `.artifacts/analysis/ollama-tool-run-894e1404-20260903`. They are not required for portable bundle consumption. Raw session reasoning, protected-root tokens, catalog credentials, and unrelated project content were excluded from bundle evidence.

API capture used unauthenticated access only after `/api/access/status` confirmed authorization was disabled. The graph query was read-only POST with `source: 2` (CanonicalCurrent). The host's OpenAPI declares that enum as an integer; the initial symbolic-string query was rejected with HTTP 400 and was corrected.

- [Project reference inventory](../analysis/project-references.json), [diagnostic probe source](../analysis/probe-source/Program.cs.txt), [probe project](../analysis/probe-source/Probe.csproj.txt), and [probe provenance](../analysis/probe-provenance.json).
- Shared source route verified in source as /api/shared-providers/openai/v1/chat/completions (OpenAiBase plus operation).

- Follow-up intake: [MAF 1.20 request](03-follow-up.md).
- [MAF 1.20 assessment](../analysis/03-maf-1-20-assessment.md) and [sanitized 1.20 evidence](../analysis/maf-1.20/sources.md).
