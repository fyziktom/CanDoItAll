# 00 — Original Request

Níže je uložené původní zadání přesně tak, jak bylo dodané.

```text
You are senior C# architect.
You must prepare detailed execution grade bundle with instructions for codex to add our AgentFramework as new module into CanDoItAll.

# Context:
- We have CanDoItAll.AgentFramework cloned in "C:\repositories\CanDoItAll.AgentFramework"
- We have CanDoItAll cloned in "C:\repositories\CanDoItAll"
- Our CanDoItAll.AgentFramework was developed in own solution. We now need to add it as module in CanDoItAll. It means to copy all files properly. We do not want to just connect the project, but it must be full integration. 
- AgentFramework main goal is to provide access to AI and agents for whole CanDoItAll.
- AgentFramework will be the main provider of LLM and other AI connection

# What you must solve during integration:

- We do not have now any system of escalation to human. We need notification center and messaging center. Something like internal MS Teams where agents can send me message or I can see notifications from some running processes. This might be own module. I need you to carefully analyze it and designed proper architecture for it. This must be added before the integration of agents as itselfs, I think.

- There are some duplicities. For example we used to have some Provider Profiles for LLMs in CanDoItAll, but now all those AI related things will do AgentFramework module. 

- we already have some internal messaging system in CanDoItAll. Agents must use it. But it is important to assure that they cannot go around the defined process. We want to do kind of strict rules. It means that agent can communicate between each other, but only if we will allow them that in the process. It means that process must have some "Messaging" line-curve in canvas that will indicate that we are allowing direct communication between some roles. If this is allowed than they can directly communicate, but process will save those messages so we can reconstruct the process run for inspection of that specific run. So, this all means that agents must not communicate directly unless it is allowed in process for specific roles that are later represented by agent or person. 

- We have CRM-HR module. It must allow to manage agents in its UI even they will be internally created-edited-managed via AgentFramework module. CRM-HR module will need HR AI Agent for help with managing human-agent resources. There must be good integration between those two modules. You must aware the creating duplicities. This will be hard. The agents as itself must be in agent module, but the information that some agent like that is available must be in CRM-HR module so it is available (toghether with real people, contractors, etc) as resources for processes. Process can be always solved with combination of resources, not just ai agents. Thats why agent module is just part of the puzzle, but not main resource pool. Main resources pool is CRM-HR module.

- We have Processes module. Integration with process module is one of the main point here. It has also hard logical connection to HR module. When we create some process with defined roles and we want to run that process, the HR AI agent must offer suitable AI agents (or people) that will do the work as that specific role. If there is no suitable agent-person it must offer that it must be created. When AI manager or human will approve the selection it can use those selected agents-person to run the process.
------------------
The flow then looks like this:

1) we created some process with defined roles
2) we are going to starting process so first we will request available resources to do the work
3) HR AI Agent gets info about starting process that needs some resources and based on roles it propose existing or potential new agents
4) AI Manager or person will approve selected resources or creating of new agents based on roles defininition
5) When resources are ready the process can be run. It means it will start process and roles of that run will have selected ai agents that will do whole process up to the end result.
------------------------

This will require at least two default "agents": HR and Main Manager. They can be represented by algorithm implementation in default (to allow start without AI at all) or with AI Agents. In default it will just assign available resource based on some hardcoded rule. But main point will be then that user will create ai agents and assign them. The HR agent will be common across the projects, but Main Manager can be specific per project, that process is related to. It can be also replaced by person that must approve selected resources. 

- CanDoItAll.AgentFramework has own sandbox UI. We will need to recompose the UI because CanDoItAll.Web has own menu and it must stay like that. AgentFramework will be in CanDoItAll.Web as one menu item. But the page will have internal main tabs that will represent original pages in AgentFramework Sandbox. It means one tab will be for creating agents, another for managing providers, another for Chat, etc. 


# Important steps for bundle preparation:
- Start with storing my original input as part of the bundle
- Identify all user-stories, actors, etc and capture them in xlsx
- Analyze actual architecture, identify all helpers and shared resources/tools/code-blocks we must use to keep code maintainable and to avoid duplicities
- Based on those information create architecture how to solve all requests
- Check if proposed architecture solves all our request. If not it must be improved. 
- Analyze weak points of architecture and identify bottlenecks and critical parts. If it is possible to improve it, do it.
- Create detailed plan of how to implement the architecture. Split it into phases. After each phase codex must do validation of implementation and if architecture goes in clean direction. If codex will find that something needs refactoring first it must add new subbundles to solve it first and then it can continue. This must be strict rule. It is large task and we must avoid that codex will continue on some mess. It often does too long files, do not using shared helpers, spliting sources of truth, etc. 
- Create subbundles with detailed instructions (inputs, prompts, unit tests prompts, checklists, validation criteria, etc) for each atomic part of implementation. each phase must contains multiple subbundles. 
- Codex must test everything with playwright mcp and real tests including screenshot analysis with FrontendSkill to assure that it works and looks as UI/UX bestpractices said.
- Codex must do validation with using list of user-stories and analyze if all can be solved in our UI. If not it must be added/improved.
- Codex must do validation on real cases that we have in AgentFramework. There are 5 scenarios. We already have some template agents, but codex must few more to test better cooperation of agents (for example for writing app scenario) on process and add new processes for that and run it as full scenario (means through running the process). Codex must not fake this tests. It must assure it is running as we described here (selection of agents based on roles, etc). If this will not pass it task is not done and it must improve-repair the implementation.
- When you think you are ready to create final zip, you must do detailed validation of prepared bundle as senior QA inspector, senior Development manager and senior C# architect and if any of those views see something that raises concerns it must be improved before final zip. The bundle will be large and detailed. Codex must have precise instructions.
```
