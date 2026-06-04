# Original Request

User requested use of `candoitall-bundle-workflow` to solve:

```text
Main goal:
Improve our Multi-Team software delivery process.

Architect notes:
We did lots of improvements of processes. But we tested it mainly on Blazor app delivery process.
The multi-team software delivery process and related subprocesses are probably not up to date. It need to harden the permissions for specific process steps (so architect will not start coding, etc).
Analyze whole proces. You can fit it to multi-team .net software delivery. We used to have it for .net and js, but it will be better to have for each own process and subprocesses.
We will first tune the .net only. It must recognize what type of app it is (just backend, blazor ssr, blazor wasm, etc). Based on that, it can continue via proper subprocesses. Actual blazor app delivery process was not bad, but what we need to add is that for apps with UI it must take screenshots and add them to project structure (under new parent node Screenshots under process run node). It should be as one of the subprocesses. Then it must also add runtime dotnet nodes in project structure (again under parent Run command under process run node). There will be at least node to run app, and run tests.
It will be better to have also subprocess for architecture design and review. If it is in one step without review it is usually not so good when we use smaller LLMs. But if we will split it to multiple process steps it will get better. Usually review should ask things like "do we have properly splited logic from components, are models well defined and cover all info, will contain all services functions we need to cover userstories, are functions testable...etc.?" You know based on coding best practice how to review architecture desing, you have those rules also in our candoitall workflow skills.
when you will implement all, assure that you did not missed anything and analyze if you would be based on that instructions build some app.
Do not run the process. After you will implement all, keep app running and I will load some test projects and run the process. Then you will do analysis of that run.
```
