# TODO: HOST LINK COMMUNICATION .NET

This file tracks the remaining tasks and issues for the HOST LINK COMMUNICATION (Keyence KV) .NET library.

## 1. Feature Implementation
- [ ] **Core Logic Migration**: Port the communication logic from the Python version.
- [ ] **Async Support**: Implement task-based asynchronous methods for all commands.

## 2. Testing & Validation
- [ ] **Hardware Evidence**: Perform validation with a real KV-8000/7500 and log results in `docs/validation/reports/`.
- [ ] **Unit Tests**: Implement XUnit tests for protocol framing and parsing.

## 3. Documentation & Maintenance
- [ ] **User Manual**: Create `docs/user/USER_GUIDE.md` with connection setup info.
- [ ] **NuGet Configuration**: Exclude non-user docs from the package.
