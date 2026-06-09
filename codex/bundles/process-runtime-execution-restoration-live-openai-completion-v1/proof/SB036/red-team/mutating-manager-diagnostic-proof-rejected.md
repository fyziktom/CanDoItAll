# SB036 Red-Team: Mutating Manager Diagnostic Proof Rejected

## Rejected Shallow Pass
A shallow Gate L pass could claim manager diagnostics exist by showing report text, UI-only screenshots, or a broad driver package import without proving the diagnostic path is read-only and mutation-free.

## Why It Is Rejected
- Manager diagnostics must project from supplied evidence only.
- Attached manager diagnostics must require a manager identity.
- Diagnostic projection must not attach an evidence envelope unless requested.
- Evidence-envelope projection must not expose process, transition, or finalizer mutation flags.
- Transcript and runtime evidence adapters must deny mutation operations and untrusted evidence without invoking verifier mutation paths.
- Runtime evidence snapshots must redact sensitive payloads and mark restricted hash policy where required.
- Process module driver consumers must stay on the explicit allowlist and must not introduce runtime driver host registration.

## Positive Proof Required Instead
- `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- `bundle://proof/SB036/transcripts/source-assertions.txt`
- `bundle://proof/SB036/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB036/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
