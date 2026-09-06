# H00 asset closure

Provider prerequisite closed before catalog edits. SDK 10.0.303, Node 24.19.0, locked Tailwind 4.2.1. Exact baseline bytes are in asset-baseline.json; sibling revisions remain the entry inventory.

Fast utility scan roots: AgentFramework.UI (real catalog/card and pure presentation), UiSandbox (specimen controls) and Conversations.Components (real card child and its UI source). The explicit Tailwind input uses source(none). No additional application CSS imports are required: catalog layout is isolated component CSS; typography, theme variables, buttons, tree, tooltip and avatar styles come from BaseLib's compiled static assets. Admin, app navigation and reconnect styles are outside the specimen.

Read-only sibling audit: Components/Tailwind/input-base.css explicitly scans BaseLib and compatibility consumers CanvasLib/OverlayLib/QRCode and imports fonts/theme/typography/cards/tooltips. Its wwwroot/css/output.css and material-symbols.css are served through real static web assets. Rescanning sibling repositories would duplicate their build responsibility. FileTools contributes no rendered catalog asset and receives no new reference or scan root. Both siblings remain live and unedited.

Mode is a build property with compiled typed identity, distinct asset URLs and sandbox output/intermediate directories. Runtime mismatch fails explicitly; missing assets fail at build. Fast does not include the production CSS item. Parity retains its original physical ContentRoot and real production theme bytes. Direct watch now permits browser refresh; this is an explicit change from the old managed-run profile suppression and is frozen uniformly for the new comparison.

Representative CSS timing uses a real catalog isolated CSS spacing edit. Each mode runs its appropriate Tailwind companion; this experiment does not isolate Tailwind-only compilation speed. CSS byte reduction is reported separately, without deriving performance claims from size.
