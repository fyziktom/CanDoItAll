# QA / red-team prompt

Review the completed bundle as a senior process-runtime architect.

Reject the bundle if:

- It creates Process Core.
- It creates production driver API.
- It drops route stages or reorders route stages.
- It changes claim lease semantics.
- It changes subprocess projection behavior.
- It changes finalizer/transition/failure semantics.
- It replaces behavior proof with source scans only.
- It leaves broad dispatcher adapters untouched while claiming isolation.
- It collapses execution report rows.
- It adds UI/mobile proof artifacts.
- It adds stubs/TODOs/NotImplemented placeholders.

The final review must state whether a next bundle may begin a minimal Process Core extraction or whether one more application-layer isolation bundle is still required.
