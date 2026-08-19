# Tasks

- [x] Inspect and preserve the three previously uncommitted governed FileTools changes; produce an exact sibling-repository change report.
- [x] Do not push/commit without operator instruction, but ensure the required FileTools source change is represented as a reviewable patch and expected committed contract.
- [x] Make package mode the default; remove automatic source-mode activation based only on sibling directory existence.
- [x] Require explicit source-mode opt-in and a committed FileTools desktop contract/version marker or exact anchor assertion.
- [x] Report desktop implementation `Validated` only after the marker/anchor matches; otherwise fail explicit source build or mark capability unverified/unavailable.
- [x] Keep package-mode desktop disabled for alpha rather than falsely validated.
- [x] Record exact Components/FileTools package/source provenance in the handoff.
