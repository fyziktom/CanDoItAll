# Phase0 — Baseline gate

Status: Pass for implementation entry. This is before evidence only, not final acceptance.

HEAD3d5def561; all twelve real Playwright MCP sends completed. No source edits, application builds, tests or concurrent measured conversations occurred during sampling. Both collectors were attached before the first send and stopped cleanly; each recorded6HTTPstarts and12runspan mappings, with0unmatchedHTTPstarts and0unexpected projected keys.

| Host | Five warm fresh created→dispatch samples, seconds | Minimum | Median | Maximum | Continuation |
|---|---|---:|---:|---:|---:|
| Native5032 |14.716154,12.827347,11.997164,12.152916,11.696706|11.696706|12.152916|14.716154|8.467535|
| Docker5214 |32.703587,31.213456,30.922540,31.868416,31.669973|30.922540|31.669973|32.703587|30.878336|

See baseline-samples.json/CSV for exact run/session IDs and browser totals; sanitized capture streams and persisted-dispatch timing JSON retain the exact HTTP parent-span joins and all persisted phases. Raw data is authoritative.

The first-observed-after-start baseline is unavailable. Existing processes were retained. One controlled first-after-replacement candidate sample must remain separate from the five warm samples. Continuations are likewise separate.

UI observation: both current pages are1920x1080, chat transcript/composer left, floating catalog right. The lists display human-readable models; client file browser styles remain applied. Normal scroll clipping at the transcript viewport is expected. A header badge can be partially occluded by fixed action controls at the existing chat-window width; this is already present before the optimization and no UI redesign is in scope. Screenshots baseline-5032.jpg and baseline-5214.png were visually inspected. No after-change visual pass is implied.

The baseline native assembly records aadd953150e7f659e4060ced6505621c705ea61f. A direct source diff toHEAD shows only the preceding Docker/style/model-label UI changes; startup storage/provider/runtime sources are identical. Client image and native executable hashes, runtime versions, storage roots, container protections, publisher identity, agent/model settings and configuration fingerprints are in host-preflight.json and companion metadata.

playwright-fresh-sample.js and playwright-continuation-sample.js retain the actual browser code used. Replace PORT/SAMPLEID/AGENTBUTTON with each recorded sample's inputs. The first already-open fresh chat used the same observer without the fresh-session transition. The first Docker sample exceeded the40second bounded wait; its still-live observer was read again after completion. No sample was discarded. Continuation UI stage observations are empty because that stream stayed collapsed; persisted server stage evidence is complete.

T_submit is browser timestamp immediately before the Playwright Send click; the browser clock aligns with the native host, and Docker alignment uncertainty is recorded in clock-alignment.json. T_created→T_dispatch uses one server clock. T_first_content records rendered assistant markdown; the current component only provides pending-user transient content, so the completed assistant content may appear simultaneously with terminal UI. HTTPstop records headers, not first content.

Implementation entry: SB01/SB02 prerequisites and source ownership confirmed; separate artifact trees protect live5032 binaries. SB03 waits for SB01's security/downstream closure. Recommendation4 remains excluded. All after-change behavioral, crash/recovery, UI/tool/approval and paired performance gates remain open.