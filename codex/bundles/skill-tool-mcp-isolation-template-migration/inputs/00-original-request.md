# Original Request

## User Request

Use `candoitall-bundle-workflow` to prepare bundle to solve this:

Main goal:
Better isolation and templating of skills and tools and MCPs.

Reason:
templating of those three things are difficult now. Lots of things are hardcoded and part as projects in level of code. Code parts around those things are mostly "hidden" in MAF common parts. It is hard to maintain that part of the code.

Architect notes:
- we have tools and skills and mcps too much integrated in MAF wrapper. you must create for them own projects with abstraction and then implementation. Common MAF core/wrapper then use them as reference.
- the way how we have it now is hard to understand if we need to add another skills, or change the default skills.
- we must use our Template folder where we have agents and processes and workflow templates to store also informations about skills.
- Tools and MCPs are little different because they can be as integrated or external. Internal is for example some function/class/service as tool. In case of MCP we can run it internally too together with app if it is our MCP. This brings better ways how to balance load and do not overload system with forgotten running not used MCPs. Anyway for those we need some system where we can setup in Templates connection to some external MCP and same for tool. It means that both must have proper interfaces to allow also kind of generic toolcall as external function, for example some python script, some another exe, etc. This must be editable as templates and use in case of seeding instead of our hardcoded versions. Some tools must be internal, so we must have their implementations. This will require own project with all implementations of tools.
- In new projects we always must keep structured folders that groups related parts together. For example in case of tools we can have folder/namespace for tools related to file system, or .net specific tools, or documents tools, etc. Same for the skills.
- We must have proper tests for loading mechanism and call mechanism. With good isolation it must be possible to have easier testing, including possibility to mockup the tools, etc.
- External Tools and MCPs must have way how user can test them during setup. We already have way in UI how to add new MCP or skill, not tools yet, those must be added. But MCP need to test start of that server and get for example list of tools.

Mandatory steps:
- You are preparing bundle only now. Do not do implementation yet.
- This will affect lots of parts around MAF workflows and processes. You must do deep analysis of current architecture and find where we have places where we will must reconnect new implementation.
- You must first create new implementations as own projects and assure they are correctly implemented and tested. It is necessary to do proper hardening and refactoring before connection to rest of the app. After this it is possible to start reconnecting MAF and others to new implementation.
- You must use xlsx to design detailed checklists and plan steps flows and phases. This will be long run and it include lots of testing. You must split tests to unit integration and e2e tests to assure that all functions work as before. We must preserve all functionalities.
- If there are some standards about naming in case of tools skills and MCPs in general AI world, we must use proper naming conventions to assure compatibility with other AI agents world and simplify understanding of our framework to other developers.

## Preparation Boundary

- This bundle contains analysis, architecture, execution contracts, traceability, and workbook planning only.
- Implementation agents must not treat these files as production changes.
