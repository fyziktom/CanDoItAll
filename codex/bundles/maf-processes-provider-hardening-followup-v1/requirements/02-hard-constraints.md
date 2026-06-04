# Hard Constraints

- Do not extract `CanDoItAll.Processes.Core` in this bundle.
- Do not introduce full `IProcessDriverPack` domain drivers in this bundle.
- Do not change process tool names, signatures, or approval behavior unless a subbundle explicitly owns and proves the change.
- Do not weaken process access checks.
- Do not remove MAF project references to product modules until source scans prove they are unused or a replacement provider is registered and tested.
- Do not hide build/test failures behind proof text.
- Do not mark browser validation as required when no rendered UI route is touched; mark it N/A with reason.
- Keep all code comments in English if implementation code is touched.
