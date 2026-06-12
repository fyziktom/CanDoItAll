# Original Request

You are senior C# architect.

We did lots of updates in maf-processes-refactor branch in CanDoItAll repo.
After last comit it is possible to run the process of multi-team app delivery process that trully worked and wrote simple TetrisGame based on inputs from project structure. We need to do merge to development branch, but before that lets do some hardening-polishing. Not some drastical changes, we are just preparing it for merge. You must analyse our code around processes deeply. Do not skip or simplify it.  
Main things you should look at are some leaks of bundles namings in the tests. we are removing all bundles. They are just as development helper, but they are not concern to stay in repo. Sometimes codex use some namings from them in tests. We must find and remove those leaks if they exists.

Then analyze again our domain drivers, do they contains all necessary domain related things? Or do we still left some items in our generic dispatcher-runtime of processes? if yes, we should transfer it. 
this refactoring was primarilly about unconnect of reference where MAF was using processes. then isolation of basic processes core, without isolation of dispatcher-runtime part (we will start working on it after merge to development). Now we were working on kind of preparations steps. 

Analyze it deeply. Prepare detailed bundle (see https://github.com/fyziktom/CanDoItAll/tree/development/codex/skills/bundles). It is better to do subbundles for larger parts because we use codex with gpt5.5 extra high, that can do larger tasks, but it must be controled via subbundles because it uses lots of context and could lost track. our whole bundle keeps it still focus on main goals.

Prepare bundle and give it as zip.
