# QA / Red-Team Prompt

Review this bundle as a hostile senior architect. Look specifically for:

- route-order drift;
- claim lease or heartbeat drift;
- failure transition drift;
- hidden side effects inside pure helpers;
- accidental Process Core or driver API introduction;
- missing proof rows;
- UI/prohibited viewport artifacts;
- functionality deleted instead of moved.

Reject the implementation if any critical gate is collapsed or missing.
