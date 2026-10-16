# Contributing to Six Console

Thank you for your interest in contributing to **Six Console**! We welcome bug reports, documentation improvements, and pull requests that adhere to the project's design principles and technical constraints.

---

## Development Prerequisites

- **Operating System:** Windows 10 (version 19041 or higher) or Windows 11 x64.
- **SDK:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.300 or newer).
- **Runtime:** Windows App Runtime 1.8+ (included automatically or installed via Windows App SDK).
- **IDE:** Visual Studio 2022/2026, Visual Studio Code (with C# Dev Kit), or JetBrains Rider.

---

## Architecture and Guidelines

Six Console is built as a native Windows desktop utility:
- **`src/SixConsole.Profile`**: Pure domain library containing the operator/weapon catalogue, mathematical pull calculation, navigation models, and profile settings. Has zero UI or platform dependencies.
- **`src/SixConsole.App`**: Native WinUI 3 application with MVVM architecture (`CommunityToolkit.Mvvm`), Windows App SDK controls, Fluent Design tokens, and Win32 low-level input hooks.
- **`src/SixConsole.Tests`**: Comprehensive automated xUnit test suite ensuring data integrity, calculation fidelity, settings persistence, and loop behavior.

### Hard Rules & Invariants

1. **Canonical Lua Source Integrity**:
   - `message (2).txt` is the canonical reference fixture.
   - `src/SixConsole.Tests/SourceFixtures/lua-source.txt` must remain byte-identical to `message (2).txt`.
   - Never "rebalance" or manually modify operator/weapon pull values, rapid-fire flags, or timings.
2. **Safety & Anticheat**:
   - The application does not and will never attempt to bypass game anticheat, inject code into third-party processes, or disguise synthetic input.
   - Synthetic inputs must remain distinguishable (`LLMHF_INJECTED` and extra-info flags) to prevent infinite hook recursion.
   - Emergency cancellation must be preserved on all paths (`STOP` button, disable toggle, window exit).
3. **Design System**:
   - Use Windows Fluent Native tokens (`windows-fluent-native` / `pro-utility-monochrome`).
   - Support System, Light, and Dark themes with verified contrast.
   - Do not add arbitrary styling or third-party web wrappers.

---

## Building and Testing

Run the automated test suite before opening any PR:

```powershell
# Run all automated tests
dotnet test src/SixConsole.Tests

# Build the application in Debug mode
dotnet build src/SixConsole.App/SixConsole.App.csproj -c Debug

# Launch the built application
& src/SixConsole.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/SixConsole.App.exe
```

All 59 unit and regression tests must pass.

---

## Submitting Pull Requests

1. Fork the repository and create a feature branch (`git checkout -b feature/my-feature`).
2. Implement your changes following existing code conventions and formatting.
3. Add unit tests for any new or modified behavior.
4. Ensure all tests pass (`dotnet test`).
5. Commit your changes with concise, descriptive commit messages.
6. Push to your branch and open a Pull Request against `main`.
