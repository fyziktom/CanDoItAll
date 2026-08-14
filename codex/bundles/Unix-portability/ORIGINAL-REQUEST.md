# Original request

The operator requested a senior C# architecture review and an updated Codex bundle for making the current `development` branch of the main `fyziktom/CanDoItAll` repository runnable on Unix-based systems: Linux and macOS.

The supplied 2026-07-31 Linux portability bundle had to be:

- compared with the latest refactored code;
- corrected, expanded, and updated to current implementation names and boundaries;
- reordered so basic slash/path issues come first, then secrets and storage, then tools/runtime nodes/processes/special tools/domain drivers;
- reviewed for prerequisite refactoring before OS-dependent selection;
- evaluated for one large Codex 5.6 Sol xhigh bundle versus a separate runtime/tools/process bundle;
- delivered as a ZIP.

This program preserves that intent while splitting execution into a core bundle and a runtime/tools/process bundle inside one ZIP.
