# Original user request

You are senior C# architect with deep understanding of CRM and HR software.

I need you to create detailed execution-grade bundle to add CRM/HR module into CanDoItAll App.
We will think about those modules together, because both handle some Person/Company/AIAgent. In HR the company can be delivery unit instead of some employee and then I need it also in CRM and vice versa. That is why we will need to kind of merge those modules to one.

Recommended steps:
- Start with definition of all user-stories that serious CRM and HR system needs.
- Map our application functions and identify user-stories in our map that are related to some CRM/HR functions (example: we have some projects => user must be able to assign partners, customers, person, aiagents to project; or another example: we have delivery node in Project Structure => assign by who; or another example: we have meetings nodes in Project Structure => assign with who, etc)
- check with view of senior Business Director if this new CRM/HR module will cover all that normal enterprise company needs. if not it must be added/improved.
- Map existing shared parts, models, components, etc  for Modules in CanDoItAll and identify those that might be usefull. Do not use canvas related components, just BaseLib for UI in this module.
- Based on analysis and all inputs/discoveries create architecture, design of models, etc.
- validate that architecture covers all user-stories and will lead to successfull implementation
- split architecture to phases and each phase to subbundles, create their checklists and folders for each subbundle, validate dependencies between subbundles and create map of implementation steps (ideal as mermaid gantt)
- create detailed instruction for each subbundle (including prompts, ascii layouts, validations, test, etc)
- validate whole plan and all instructions if they cover all functionalities

When you think you are ready to create final zip. do one more validation as senior QA inspector.
You must confirm that bundle convers all important and CanDoItAll related user-stories for CRM/HR module. Assure that codex will be able to automatically and fully implement and validate all (using playwright mcp, screenshots with real analysis of them, etc). If something is missing, can be improved, is not detailed enough, etc it must be improved before final zip.
Then create final zip.
