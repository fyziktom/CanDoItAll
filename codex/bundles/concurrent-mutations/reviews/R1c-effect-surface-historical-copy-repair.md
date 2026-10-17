# Historical recovery copy assertion repair

Source-only successor to the retained historical Components50 result (49 pass, one failed). The failed assertion expected original recovery context is not retained; the reviewed historical renderer intentionally uses original execution context is not retained and explains that receipt review does not authorize a new recovery attempt. This is a stale test expectation, not a dispatch or admission change.

Only that expectation is corrected, with a second assertion retaining the read-versus-recovery distinction. Existing disabled/absent action, explicit intent, SnapshotRead, approval and success assertions remain. No production or renderer metadata changed. The class and selected lane counts remain unchanged (four effect surface Facts; historical Components50).

Predecessor: EEF051EF99C58A0E96AD3521055696BE6B597BE7FF1F4E1B631604D6F7D0F571. Byte-exact predecessor is retained in R1c-effect-surface-before-historical-copy-repair.cs.txt. Successor: 54F280ABF728F9F501121250E776FBF0B90A45312EDF95713EC911C1F2F70D16.

No build, test, host, database or portability scan was run. Parent owns affected compilation, discovery/execution and final static closure; source syntax and whitespace checks are the only local checks.