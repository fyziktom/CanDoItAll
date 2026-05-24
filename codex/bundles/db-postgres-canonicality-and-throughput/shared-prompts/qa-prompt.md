# QA prompt

Review the completed subbundle as a skeptical QA lead.

Check:
- Did the implementation actually change production paths, or only tests/docs?
- Are canonical runtime DB and pending activation state separated?
- Are PostgreSQL batch claims safe under concurrency?
- Can stale workers commit after losing claim token?
- Is any dead hot-switching or SQLite-era bottleneck still present?
- Are UI/browser assertions strong enough?
- Are validation failures honestly reported?
