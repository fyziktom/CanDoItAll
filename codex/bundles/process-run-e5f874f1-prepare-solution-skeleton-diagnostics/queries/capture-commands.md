# Capture Commands

These are the important capture patterns used for this folder.

## API

```powershell
Invoke-RestMethod http://localhost:5032/api/access/status
Invoke-RestMethod http://localhost:5032/api/processes/runs/e5f874f1-02b9-43c8-9c2d-ee932972e992
Invoke-RestMethod http://localhost:5032/api/processes/runs/ab4a1ed8-8b1b-4974-973d-93983bf41f09
Invoke-RestMethod "http://localhost:5032/api/agents/execution-runs?processRunId=e5f874f1-02b9-43c8-9c2d-ee932972e992&processStepId=db3e7295-b523-4343-8be6-85598427385b&take=100"
Invoke-RestMethod "http://localhost:5032/api/agents/execution-runs?processRunId=ab4a1ed8-8b1b-4974-973d-93983bf41f09&processStepId=53d370f4-04c6-4f9c-8ce0-9cd89efda764&take=100"
Invoke-RestMethod http://localhost:5032/api/agents/execution-runs/48c3753c-d0bb-4679-9eae-2f295d2b8181/tool-receipts
```

## Database

```powershell
docker exec candoitall-postgres psql -U candoitall -d candoitall_development -x -c 'select * from process_strategy_result_receipts where "RunId" = ''e5f874f1-02b9-43c8-9c2d-ee932972e992'' and "StepInstanceId" = ''db3e7295-b523-4343-8be6-85598427385b'';'
docker exec candoitall-postgres psql -U candoitall -d candoitall_development -x -c 'select * from process_strategy_result_receipts where "RunId" = ''ab4a1ed8-8b1b-4974-973d-93983bf41f09'' and "StepInstanceId" = ''53d370f4-04c6-4f9c-8ce0-9cd89efda764'';'
```

The complete DB exports are in `db/`.

## Product Readback

```powershell
Get-Content C:\programovani\dotnet\calculator-output\Calculator.slnx -Raw
dotnet sln C:\programovani\dotnet\calculator-output\Calculator.slnx list
```
