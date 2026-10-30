# Auto Manual Tester — Product Architecture

## Product contract

This is an autonomous, human-like application testing system. It is not merely a process launcher or UI Automation tree viewer. The application must discover an application, understand its screens, plan meaningful tests, execute user-like interactions, verify observable outcomes, preserve evidence, and produce actionable reports.

## Frozen technology decisions

- Host desktop application: C# / .NET 10 / Windows Forms.
- Windows desktop automation: FlaUI + UIA3.
- Web automation: Playwright adapter.
- Android automation: Appium adapter.
- AI planning and interpretation: provider-independent abstraction; Gemini is the first provider.
- Local persistence: SQLite.
- Structured logging: Serilog.
- Reports: HTML and PDF with screenshots, logs, steps, expected/actual outcomes, and bug classification.
- Delivery: Windows x64 self-contained installer and portable ZIP through GitHub Actions.
- Updates: background manifest check every 3 seconds, SHA-256 verification, silent per-user update and restart after a successful release.

## Required test lifecycle

1. Discover — identify the target, windows, screens, controls, navigation and available interaction methods.
2. Observe — capture UI Automation metadata and screenshots; OCR/visual understanding supplements controls where accessibility metadata is absent.
3. Understand — build a screen and workflow model, preserving observed facts separately from AI inferences.
4. Plan — generate explicit test cases with preconditions, actions, expected outcomes, risk and cleanup.
5. Review — require approval for destructive, financial, irreversible, external or production-impacting actions.
6. Execute — use the platform adapter to interact through keyboard, mouse and semantic controls; support pause, resume, cancellation and emergency stop.
7. Verify — check postconditions using UI state, control values, navigation, notifications and application-visible evidence. A click alone is never a pass.
8. Record — save steps, timestamps, screenshots, logs, expected/actual results and reproducible bug details.
9. Report — generate HTML/PDF reports, classify potential vs confirmed defects, and retain regression history.

## Modular boundaries

- Core: test models, lifecycle, policy, planning contracts, verification contracts and result classification. No UI framework dependencies.
- Automation: platform adapters implementing a shared automation contract. Windows adapter is first; Playwright and Appium remain separate adapters.
- AI: provider-neutral planner/vision interpretation contracts. AI output is untrusted input and must pass policy validation.
- Persistence: SQLite repositories for projects, targets, test plans, sessions, steps, findings and evidence.
- Reporting: HTML/PDF renderers over persisted test results.
- Desktop UI: project/target setup, live observation, plan review, execution controls, evidence viewer, history and reports.

## Safety invariants

- Never treat an AI suggestion as authorization.
- Never execute destructive or externally consequential steps without explicit user approval.
- Enforce per-run step, duration and interaction limits.
- Provide pause, resume, cancellation and emergency stop.
- Scope all automation to the selected target and its owned windows/processes.
- Clearly label inferred, unverified and confirmed findings.
- A test is Passed only when its expected postcondition is verified.

## Implementation milestones

1. Core contracts, test lifecycle and execution safety.
2. Windows adapter: reliable discovery, screenshots, semantic controls, mouse/keyboard and wait/retry.
3. Observe/understand screen model and workflow recorder.
4. AI-assisted test planning with editable/reviewable test cases.
5. Execution and independent postcondition verification.
6. SQLite persistence, evidence capture and HTML/PDF reporting.
7. Playwright and Appium adapters.
8. Regression runs, bug triage and usability hardening.
9. Release validation and end-to-end updater testing.

The existing launcher/inspector is only a temporary diagnostic surface. It is not the product definition and must not be represented as a completed tester.
