# SB14 Line Count And Hotspot Source Assertions

- Invariant ID: `SB14-INV-001`.
- Main finalizer line count is 1433, reduced from the SB01 baseline of 2091.
- Extracted helper files are bounded: readers 230 lines, types 139 lines, validation orchestration 129 lines, runtime invariant audit 149 lines, transition request builder 39 lines.
- No extracted helper became a new dispatcher monolith.
- Transcript: `bundle://proof/SB14/transcripts/line-count-hotspot-scan.txt`.
