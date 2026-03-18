# TODO: Host Link Communication .NET

This file tracks the remaining tasks and issues for the Host Link Communication (Keyence KV) .NET library.

## 1. Project Scaffold
- [ ] **Create the Actual Library Project**: Add the `src/` .NET project structure that `docfx.json` and the workspace policy expect.
- [ ] **Fix the Canonical API Shape**: Start the implementation with `KvHostLinkClient` and `KvHostLinkDeviceClient`, plus async naming reserved from the beginning.

## 2. Protocol and Feature Work
- [ ] **Frame / Parser Core**: Implement the Keyence KV Host Link frame builder, parser, and transport handling.
- [ ] **Async Surface**: Add task-based async methods only after the sync surface is defined and tested.

## 3. Testing and Validation
- [ ] **Unit Test Base**: Add .NET tests for framing, parsing, and error handling before live PLC integration.
- [ ] **Hardware Evidence**: Validate against a real KV-8000 / KV-7500 class target and write reports in `docs/validation/reports/`.

## 4. Documentation and Packaging
- [ ] **User Guide**: Create `docs/user/USER_GUIDE.md` after the actual public API exists.
- [ ] **NuGet Packaging Rules**: Apply distribution exclusions once the `.csproj` is in place.
