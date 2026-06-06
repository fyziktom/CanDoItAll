# Phase Plan

## Subbundle Dependency Map

```mermaid
gantt
    title Route Service / Model Decoupling Boundary
    dateFormat  X
    section P1 Baseline and guardrails
    SB001 Entry branch audit and previous proof review : 1, 1
    SB002 Route service/model inventory : 2, 1
    SB003 Route alias and all-facet service source scan : 3, 1
    SB004 Gate A: no-Core/no-driver/no-UI guardrails : 4, 1
    SB005 Route behavior preservation matrix : 5, 1
    SB006 Route side-effect matrix refresh : 6, 1
    SB007 Focused route test selection : 7, 1
    SB008 Gate B: baseline proof and reopen triggers : 8, 1
    section P2 Route model and adapter foundation
    SB009 Introduce route-owned candidate/run/step snapshots : 9, 1
    SB010 Introduce route-owned dispatch claim model : 10, 1
    SB011 Introduce route-owned execution context model : 11, 1
    SB012 Gate C: route snapshot adapter proof : 12, 1
    SB013 Introduce direct-agent route outcome snapshot : 13, 1
    SB014 Introduce route mutable state : 14, 1
    SB015 Add adapter from dispatcher nested models to route models : 15, 1
    SB016 Gate D: adapter parity proof : 16, 1
    SB017 Migrate route context to route-owned models : 17, 1
    SB018 Migrate handler tests to route-owned context : 18, 1
    SB019 Source scan for forbidden aliases in handlers : 19, 1
    SB020 Gate E: route model foundation closure : 20, 1
    section P3 Route context and result decoupling
    SB021 Extract ProcessDispatchRouteHandlerResult to route-owned file : 21, 1
    SB022 Extract ProcessDispatchRouteContext to route-owned file : 22, 1
    SB023 Remove direct dependency on ProcessClaimedDispatchExecution from context : 23, 1
    SB024 Gate F: context/result decoupling : 24, 1
    SB025 Route stage facts model : 25, 1
    SB026 Route handler side-effect declaration model : 26, 1
    SB027 Route order assertion over route-owned stages : 27, 1
    SB028 Gate G: route order proof : 28, 1
    SB029 Route handler fixture builder : 29, 1
    SB030 Route context mutation audit : 30, 1
    SB031 Route source assertion hardening : 31, 1
    SB032 Gate H: context/test closure : 32, 1
    section P4 Claim and trigger model decoupling
    SB033 Route claim adapter boundary : 33, 1
    SB034 Claim coordinator return model migration feasibility : 34, 1
    SB035 Trigger normalization route model : 35, 1
    SB036 Gate I: claim/trigger model proof : 36, 1
    SB037 Renew-lease callback wrapper model : 37, 1
    SB038 Heartbeat route lifecycle facts : 38, 1
    SB039 Claim lost closure adapter review : 39, 1
    SB040 Gate J: claim lifecycle readiness : 40, 1
    section P5 Pre-execution service split
    SB041 Split database requirement route service : 41, 1
    SB042 Database requirement transition negative proof : 42, 1
    SB043 Split upstream materialization route service : 43, 1
    SB044 Upstream materialization request/journal proof : 44, 1
    SB045 Remove database/upstream from all-facet service : 45, 1
    SB046 Pre-execution service source scan : 46, 1
    SB047 Pre-execution route handler constructor narrowing : 47, 1
    SB048 Gate K: pre-execution service proof : 48, 1
    SB049 Database route model adapter cleanup : 49, 1
    SB050 Materialization route model adapter cleanup : 50, 1
    SB051 Pre-execution side-effect classification update : 51, 1
    SB052 Pre-execution integration smoke : 52, 1
    SB053 Pre-execution red-team review : 53, 1
    SB054 Pre-execution doc and driver-readiness update : 54, 1
    SB055 Pre-execution line-count review : 55, 1
    SB056 Gate L: pre-execution closure : 56, 1
    section P6 Recovery and subprocess service split
    SB057 Split stranded artifact recovery route service : 57, 1
    SB058 Recovery finalizer handoff proof : 58, 1
    SB059 Split subprocess route service shell : 59, 1
    SB060 Subprocess lifecycle transition proof : 60, 1
    SB061 Subprocess artifact projection adapter review : 61, 1
    SB062 Subprocess projection persistence side-effect proof : 62, 1
    SB063 Remove recovery/subprocess from all-facet service : 63, 1
    SB064 Gate M: recovery/subprocess proof : 64, 1
    SB065 Subprocess route read model feasibility : 65, 1
    SB066 Subprocess capability gap boundary scan : 66, 1
    SB067 Subprocess projection matrix tests : 67, 1
    SB068 Gate N: subprocess model proof : 68, 1
    SB069 Recovery route no-op/handled tests : 69, 1
    SB070 Recovery route source hardening : 70, 1
    SB071 Recovery/subprocess red-team : 71, 1
    SB072 Gate O: recovery/subprocess closure : 72, 1
    section P7 Start/workflow/direct-agent service split
    SB073 Split start transition route service : 73, 1
    SB074 Start transition reload parity tests : 74, 1
    SB075 Split workflow route service : 75, 1
    SB076 Workflow finalizer handoff proof : 76, 1
    SB077 Split direct-agent route service : 77, 1
    SB078 Direct-agent execution outcome model adapter : 78, 1
    SB079 Remove start/workflow/direct from all-facet service : 79, 1
    SB080 Gate P: start/workflow/direct proof : 80, 1
    SB081 Direct-agent route model source scan : 81, 1
    SB082 Workflow route handler constructor narrowing : 82, 1
    SB083 Start-transition negative/reload tests : 83, 1
    SB084 Gate Q: mid-route closure : 84, 1
    SB085 Direct-agent route red-team : 85, 1
    SB086 Workflow/direct driver-readiness docs : 86, 1
    SB087 Route factory composition update : 87, 1
    SB088 Gate R: start/workflow/direct final : 88, 1
    section P8 Guard/finalizer/failure closure service split
    SB089 Split competing execution guard route service : 89, 1
    SB090 Split run-closed guard route service : 90, 1
    SB091 Guard route query proof : 91, 1
    SB092 Gate S: guard service proof : 92, 1
    SB093 Split finalizer route service : 93, 1
    SB094 Finalizer transition handoff proof : 94, 1
    SB095 Split failure closure route service : 95, 1
    SB096 Gate T: finalizer/failure proof : 96, 1
    SB097 Exception closure context model decoupling : 97, 1
    SB098 Claim-lost/heartbeat-lost closure tests : 98, 1
    SB099 Generic failure transition tests : 99, 1
    SB100 Failure closure side-effect scan : 100, 1
    SB101 Remove guard/finalizer/failure from all-facet service : 101, 1
    SB102 All-facet service deletion/shim plan : 102, 1
    SB103 No broad service source scan : 103, 1
    SB104 Gate U: guard/finalizer/failure closure : 104, 1
    section P9 Factory, source hardening and line-count pass
    SB105 Replace route factory all-service input with route facet set : 105, 1
    SB106 Route facet set construction update : 106, 1
    SB107 Remove ProcessDispatchRouteServices or reduce to explicit adapter-only shim : 107, 1
    SB108 Gate V: factory and service split proof : 108, 1
    SB109 Remove forbidden dispatcher alias usage from route-facing files : 109, 1
    SB110 Route handler constructor source scan : 110, 1
    SB111 Route service implementation file-size review : 111, 1
    SB112 Gate W: source hardening proof : 112, 1
    SB113 Dispatch/RouteExecution line-count pass : 113, 1
    SB114 Known unrelated failure notes refresh : 114, 1
    SB115 Broad focused route smoke matrix : 115, 1
    SB116 Gate X: line-count and broad smoke closure : 116, 1
    section P10 Driver-readiness, broad smoke and final closure
    SB117 Documentation-only driver-readiness map update : 117, 1
    SB118 Core readiness checkpoint without Core creation : 118, 1
    SB119 Architecture guard: no collapsed execution rows : 119, 1
    SB120 Gate Y: no-core/no-driver checkpoint : 120, 1
    SB121 Full solution build proof : 121, 1
    SB122 Focused unit route boundary tests : 122, 1
    SB123 Focused integration route tests : 123, 1
    SB124 Gate Z: build and tests : 124, 1
    SB125 Final source scan no UI/mobile/Core/driver : 125, 1
    SB126 Final anti-stub audit : 126, 1
    SB127 Final red-team and manager review : 127, 1
    SB128 Gate FINAL: completed validator and closure : 128, 1
```

## Critical Subbundles

SB004, SB008, SB012, SB016, SB020, SB024, SB028, SB032, SB040, SB048, SB056, SB064, SB072, SB080, SB088, SB096, SB104, SB112, SB120, SB128

## Phase Gates

- **P1 Baseline and guardrails**: may proceed only after SB004, SB008 passes.
- **P2 Route model and adapter foundation**: may proceed only after SB012, SB016, SB020 passes.
- **P3 Route context and result decoupling**: may proceed only after SB024, SB028, SB032 passes.
- **P4 Claim and trigger model decoupling**: may proceed only after SB040 passes.
- **P5 Pre-execution service split**: may proceed only after SB048, SB056 passes.
- **P6 Recovery and subprocess service split**: may proceed only after SB064, SB072 passes.
- **P7 Start/workflow/direct-agent service split**: may proceed only after SB080, SB088 passes.
- **P8 Guard/finalizer/failure closure service split**: may proceed only after SB096, SB104 passes.
- **P9 Factory, source hardening and line-count pass**: may proceed only after SB112 passes.
- **P10 Driver-readiness, broad smoke and final closure**: may proceed only after SB120, SB128 passes.

## No-collapse rule

The execution report must contain individual rows for SB001 through SB128. A collapsed row such as `SB001-SB128` is not acceptable.
