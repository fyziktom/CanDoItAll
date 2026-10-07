# Large desktop and useful isolated development

Primary viewport is 1920x1080, scale 1. A second large viewport (for example 1600x1000) is optional
only for a concrete functional geometry defect. No mobile/tablet/small/medium tuning campaign.
Do not remove existing framework responsiveness tests; do not spend this run redesigning them.

Verify real tab/step content, long typed configuration, scroll ownership, visible/hit-tested footer,
Enter/Space/Escape behavior, focus return, nested picker/dialog stacking and live binding before
blur. An input in the DOM is not proof it is visible or clickable. Keep production and sandbox
on identical renderers and relevant styles/assets. Source and Production-published hosts are
separate proof. Never provide a mock menu instead of the real child.

Sandbox scenarios include every capability mode and team surface plus loading/empty, malformed
config, parse-invalid numeric text, saved missing references, read failure/retry, held setup/save,
known refusal, conflict, unknown result, accepted-save/readback failure, old result versus new
editor, large agent selection and independent simultaneous editors. Fixture stored state is
separate from mutable draft so refresh and identity assertions prove something.

Measure evaluated graph, watch-file set, build/start/navigation/hydration and at least three
visible Razor/C#/scoped-CSS edits with exact source pair/SDK/configuration. Record server PID,
restart count, edit/event times, browser confirmation, failed probes and restored hashes. Test JS
when the moved family owns it. No browser action before compilation is proof of a failed hot
reload; distinguish observation errors. Record restart fallback separately, never call it hot reload.

PP3's successful same-PID probes are historical context, not a replacement for this new sandbox.
Compare like conditions only; no universal speedup based solely on project/file counts.
