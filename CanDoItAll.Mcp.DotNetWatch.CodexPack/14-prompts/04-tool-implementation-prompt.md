# Tool implementation prompt

Implement or finalize the public MCP tools according to the contract.

## Required tools
- `candoitall_workspace_info`
- `candoitall_app_start`
- `candoitall_app_stop`
- `candoitall_app_status`
- `candoitall_app_wait`
- `candoitall_app_logs`
- `candoitall_solution_build`
- `candoitall_tests_run`
- `candoitall_operation_status`
- `candoitall_operation_wait`
- `candoitall_operation_logs`
- `candoitall_cleanup_stale_processes`
- `candoitall_diagnose_start_failure`

## Requirements
- Use strongly typed DTOs.
- Use consistent error codes.
- Keep tools thin: validate input, call services, shape response.
- Do not put orchestration logic directly inside tool classes.
- Ensure response payloads are easy for an agent to consume.

## Deliver
- list of tool classes
- request/response DTO summary
- mapping from each tool to its internal service
