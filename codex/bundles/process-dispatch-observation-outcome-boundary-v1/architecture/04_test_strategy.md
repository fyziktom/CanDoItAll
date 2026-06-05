# Test Strategy

## Required focused test groups

- Session observation parser:
  - tool call/result pairing
  - successful result filtering
  - file write/read/stat extraction
  - assistant response/error extraction
  - malformed JSON returns empty observations

- Execution log observation:
  - successful tool names
  - internal tool trust gate
  - browser output filename argument extraction
  - failed log entries ignored

- Declared outcome:
  - status mapping
  - branch key/title/id resolution
  - invalid branch errors
  - missing required tool without receipt

- Completion status/reason:
  - non-terminal run state
  - pending approvals
  - failed outcome
  - declared blocked/completed/refused/waiting
  - critical failures
  - missing blockers
  - explicit disposition recovery

- Regression smoke:
  - artifact contract integration slice
  - retry/no-progress focused slice
  - provider recovery unaffected
  - full solution build

## Required scans

- no Process Core
- no production driver API
- no stubs/TODO/NotImplemented
- no UI files
- no small/medium/mobile proof paths
- line count transcript for ToolValidation/Concurrency/Execution/ArtifactValidation
