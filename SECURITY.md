# Security Policy

## Supported Versions

Only the latest release of Six Console receives security updates.

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

---

## Security Principles of Six Console

Six Console is designed with strict transparency and safety principles:

1. **No Administrative Privileges Required**: The application runs entirely in user-mode space.
2. **Standard Documented Windows APIs**: Input observation is performed via documented Win32 hooks (`WH_MOUSE_LL`) and emission via standard `SendInput`.
3. **No Process Injection**: Six Console does not open, inspect, inject, or tamper with the memory or execution of any other running application or game.
4. **Distinguishable Synthetic Input**: Synthetic mouse movements and keystrokes carry the standard Windows `LLMHF_INJECTED` flag and an explicit extra-info marker (`0x53495843`). This guarantees that the application's own input engine cannot enter an infinite loop.
5. **No Telemetry / No Network Access**: Six Console contains no analytics, network trackers, telemetry services, or remote connections. All settings and diagnostic logs remain exclusively on the user's local disk under `%LOCALAPPDATA%\SixConsole`.
6. **Failsafe Controls**: The application includes an unconditional Emergency Stop (`STOP` title bar action) that halts all worker loops, releases any held virtual keys, and disables the profile immediately.

---

## Reporting a Vulnerability

If you believe you have discovered a security vulnerability or unexpected input behavior:

1. **Do not** report security vulnerabilities via public GitHub issues.
2. Contact the maintainer privately via GitHub private vulnerability reporting or direct message.
3. Please provide a detailed description of the vulnerability, including steps to reproduce, affected Windows version, and potential impact.
4. We will acknowledge receipt within 48 hours and coordinate remediation before public disclosure.
