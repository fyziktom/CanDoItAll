# QA / Verification Prompt

Verify the bundle as a behavior-preserving refactor.

Reject completion if:
- Process Core or production driver API appears;
- any UI file is touched;
- any helper has TODO, NotImplementedException, return default, or fake placeholder behavior;
- exact summary/status/retry behavior drifts;
- source scans are used without focused tests for behavior-critical gates;
- proof artifacts contain mobile/small/medium/tablet/phone/responsive screenshots or paths.
