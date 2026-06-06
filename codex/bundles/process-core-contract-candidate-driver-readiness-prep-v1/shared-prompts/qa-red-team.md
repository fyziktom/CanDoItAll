# QA / Red-Team Prompt

Review the completed bundle as a skeptical architect.

Reject the implementation if:
- Process Core was created,
- production driver API was added,
- UI/media/mobile proof was created for runtime-only changes,
- adapters were hidden under new generic names,
- source payloads remain in route-facing services without a named edge adapter,
- finalizer or subprocess behavior changed,
- route order changed,
- execution report rows are collapsed,
- proof lacks build/unit/focused integration/source scans.
