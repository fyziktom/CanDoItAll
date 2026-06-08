# QA / Red-Team Prompt

Review every gate for fake proof:
- Did production alpha code accidentally become runtime infrastructure?
- Did a test inspect only strings but miss source-level forbidden tokens?
- Does any response path omit audit/redaction/no-mutation proof?
- Does any verifier read files or run commands instead of consuming supplied transcript content?
- Does any Office/business lane permit mutation?
- Did Core begin referencing driver abstractions?
- Are report rows separate and validators complete?
