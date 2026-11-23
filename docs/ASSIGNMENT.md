# C#/.NET backend practice project

The technical exercises come from `מבחן בית – מפתח_ת צד שרת (Junior C# _ .NET) – גרסה למועמד.pdf`. These notes give the learner an assignment reference and record the project’s progress. The learner works through the project independently and uses Codex to answer questions as they arise.

## Scenario (page 1)

A bank's Foundation layer sends messages to a queue consumed by its core system. The application also models a session with a stock exchange.

## Implementation exercises (pages 1–3)

1. **Code review.** Review the `ChannelManager` snippet on page 2. Identify at least four issues and explain what can go wrong in practice. The snippet uses public string state, a public static queue selector, constructs a queue client inside `Send`, returns outside `Active`, appends to `audit.log` using local time and message length, and assigns `Active` in `OnLoginAck` when the state is `LoggingIn` or `Suspended`. The full sample is in the PDF.
2. **Queue interface and implementations.** Provide a common interface for sending messages and implement a simulated DATQ client and an in-memory simulator. The DATQ client reads connection details from configuration and can simulate the send by logging it. Keep server names and passwords out of source code. The assignment calls for a simulated connection to the bank's AS400 queue.
3. **Startup configuration and DI.** Register the queue implementations with dependency injection. Select the active implementation from `appsettings.json` at startup, using a setting such as `Queue:Type` with the value `Sim`. Switching the configured queue must require only a configuration change.
4. **Session states.** Enforce the base transition table below. Block every unlisted transition. Only an `Active` session can send a message to the queue. Briefly explain the state design and its rationale in `README.md`.
5. **Audit.** For each message sent, record a UTC timestamp, the message length and an increasing sequence number. Provide an audit interface so tests can substitute an in-memory implementation.
6. **Base tests.** Use xUnit with no external exchange or AS400 dependency. Cover the four behaviors listed below.
7. **Short design question.** In `README.md`, explain when to choose an abstract class and when to choose an interface, with an example from this scenario.
8. **`Reconnecting` extension.** Implement the four added transitions below and test one legal transition and one illegal transition involving the extension. In `README.md`, explain how many places in the code changed to add the state and why those changes were needed.
9. **Sending while `Suspended`.** Choose and implement what happens to a send request in this state. The PDF suggests dropping, buffering until `Active`, or throwing an exception. Add a test and explain the choice and its downside in `README.md`.

### Transition table

| From | Base allowed destinations | Added by item 8 |
| --- | --- | --- |
| `Disconnected` | `Connecting` | |
| `Connecting` | `LoggingIn`, `Disconnected` (connection failure) | |
| `LoggingIn` | `Active`, `Disconnected` (login rejected) | |
| `Active` | `Suspended`, `LoggingOut` | `Reconnecting` |
| `Suspended` | `Active`, `LoggingOut` | `Reconnecting` |
| `LoggingOut` | `Disconnected` | |
| `Reconnecting` | | `LoggingIn`, `Disconnected` |

### Required behavior checks

- `Suspended` → `Active` succeeds.
- `Disconnected` → `Active` is blocked.
- A send request while the session is outside `Active` does not reach the queue in that state.
- A send request while `Active` reaches the queue and creates an audit entry.
- One legal `Reconnecting` transition succeeds and one illegal transition is blocked.
- Sending while `Suspended` follows the chosen behavior.

### Project and README requirements

- Use a **Console app or Worker Service** and put the separate xUnit test project in the same solution.
- `dotnet build` and `dotnet test` must pass on a clean machine.
- Document how to run the project, how to switch queues, and any unfinished work or things you would do differently. Include the code review, the state design rationale, the abstract-class/interface answer, the `Reconnecting` change count and rationale, and the `Suspended` send behavior and trade-off.
- Submit a GitHub repository link with access or a ZIP. Include the code, the test project, `README.md`, `ANSWERS.md` for Part B, `AI_NOTES.md`, and the approximate hours spent.

## Reliability discussion prompts (page 3)

A component receives about 1,000 exchange messages per second with increasing sequence numbers. It writes each message to the bank queue and a database table. The scenario requires avoiding loss and duplicates.

1. After a midday restart, how does the component know which message to resume from, and where is that information stored?
2. If the component crashes after the database write but before recording the message as processed, what happens on restart? How would you prevent duplicates in the database table? The prompt points to database keys and indexes.
3. If the database is unavailable for several minutes, should the component keep sending to the bank queue? What happens to messages that have yet to be written to the database?

## Implementation plan

### Current progress

- Step 1 is complete in commit `320680e`. `courseProj.sln` contains the `Backend` console app and the `Backend.Tests` xUnit project. Both target .NET 11. The installed SDK is 11.0.100.
- Step 2 has the six base states and all ten allowed transitions. The [ExchangeSession](../Backend/Sessions/ExchangeSession.cs) constructor sets the initial state to `Disconnected`. `TryTransitionTo` checks a `HashSet` of `(From, To)` pairs created by `CreateAllowedTransitions`. [ExchangeSessionTests.cs](../Backend.Tests/ExchangeSessionTests.cs) covers both the blocked `Disconnected` → `Active` transition and the allowed `Suspended` → `Active` transition.
- Step 4 was worked on ahead of Step 3. [Program.cs](../Backend/Program.cs) registers `InMemoryMessageQueue` and `DatqMessageQueue` with dependency injection. `BuildHost` sets up configuration and the service container. `Queue:Type` in [appsettings.json](../Backend/appsettings.json) selects which implementation is returned as `IMessageQueue`.
- Run the console demo with `dotnet run --project Backend` from the solution folder. Pass `-- --Queue:Type=Sim` or `-- --Queue:Type=Datq` to override the JSON value for one run. Set `Queue__Datq__Server` and `Queue__Datq__Password` in PowerShell before running the DATQ simulator.
- [QueueSelectionTests.cs](../Backend.Tests/QueueSelectionTests.cs) passes `--Queue:Type=Sim` directly to `BuildHost` and checks for an `InMemoryMessageQueue`. `Backend.csproj` gives `Backend.Tests` access to that internal method.
- The learner ran `dotnet test` and confirmed that both projects built and the first three tests passed. The learner then added the `Suspended` → `Active` test and reported that it passed.
- Step 3 implementation and tests are in place. [ExchangeMessageSender](../Backend/Sessions/ExchangeMessageSender.cs) takes an `ExchangeSession`, an `IMessageQueue`, and an `IAuditLog`. `TrySend` returns `false` for an inactive session. For an active session it sends the message, increments its per-sender sequence number, and records an entry with the current UTC time and message length.
- The learner reported that the tests passed after audit recording was wired in. [The disconnected send test](../Backend.Tests/ExchangeMessageSenderTests.cs) checks that `TrySend` returns `false` and leaves both the in-memory queue and audit log empty.
- [AuditEntry](../Backend/Audit/AuditEntry.cs) holds a UTC timestamp, message length, and sequence number. [IAuditLog](../Backend/Audit/IAuditLog.cs) defines `Record(AuditEntry entry)`.
- [InMemoryAuditLog](../Backend/Audit/InMemoryAuditLog.cs) implements `IAuditLog` and exposes a read-only view of the recorded entries for tests.
- The active-send test in [ExchangeMessageSenderTests.cs](../Backend.Tests/ExchangeMessageSenderTests.cs) sends two messages and checks the queue order, UTC timestamps, message lengths, and sequence numbers.
- The console demo in [Program.cs](../Backend/Program.cs) moves a new session to `Active` and sends one message through the selected queue. The learner verified both `Sim` and `Datq` using `dotnet run --project Backend`. Each run printed the expected queue class, `Sent: True`, and `Audit entries: 1`. The DATQ run also printed its simulated-send log.
- Step 5 implementation and tests are in place. `SessionState` has `Reconnecting`, and `ExchangeSession` includes all four new transition pairs. The learner reported passing tests for `Active` → `Reconnecting` and the blocked `Reconnecting` → `Active` transition.
- Step 6 uses immediate rejection while `Suspended`. `TrySend` returns `false`. The queue and audit log stay empty, including after the session resumes. The caller must retry explicitly. The learner reported that `SuspendedSessionRejectsSend` passed, bringing the suite to nine tests.
- [README.md](../README.md) has the run instructions, queue settings, session design, `Reconnecting` change count, suspended-send trade-off, interface versus abstract class explanation, `ChannelManager` review, and reliability answers. The learner verified both console modes and confirmed the final `dotnet build` and `dotnet test` passed locally with nine tests.
- Crash recovery extension is implemented. [SqliteOutbox](../Backend/Persistance/SqliteOutbox.cs) saves the message ID and payload before sending. `SentUtc` is empty until the send is confirmed. [SqliteMessageQueue](../Backend/Queues/SqliteMessageQueue.cs) stores the accepted payload and UTC receipt time in a separate database. A retry with the same ID and payload reuses that receipt.
- Select `DurableSim` to try recovery on startup. Once the session is `Active`, the sender retries unconfirmed messages with their original IDs. New and recovered sends use the same audit path. When recovery confirms an outbox row, it records one in-memory audit entry with the receiver's original receipt time and prints the audit count. The outbox retains durable confirmation data. `Sim` and `Datq` report any unconfirmed IDs for manual review. The SQLite files are local to each computer. `Storage:Directory` can override the default app-data folder.
- [SendRecoveryTests](../Backend.Tests/SendRecoveryTests.cs) verifies both restart scenarios. A pending message with no receipt is delivered, and a pending message with an existing receipt is confirmed with that receipt's timestamp. Each case ends with one receipt and one confirmed outbox row. The recovery audit test checks receipt times, message lengths, sequence order, and that a second recovery pass adds no entries. Tests also verify manual review for `Sim` and `Datq` and the outbox payload and receipt reads.
- [ANSWERS.md](../ANSWERS.md) contains the three Part B answers in Hebrew. The learner's approximate hours and AI use notes still need to be filled in for submission.
- Independent review verified all 49 state pairs, send behavior in all seven states, and configuration-file startup for all three queues. A fresh source copy built and tested with cached NuGet packages. NuGet could not reach its vulnerability feed during that run. After fixing recovery audit, `dotnet build` completed with zero warnings and errors and all 19 tests passed. The next optional practice step is an operator workflow for resolving unconfirmed `Sim` and `Datq` sends.

### Milestones

This table records the original milestone sequence and suggested commit messages for reference. The learner decides when to work on each milestone and record the results.

| Step | Work and verification | Commit message |
| --- | --- | --- |
| 1. Set up the solution | Create the Console app, xUnit project, solution, and `.gitignore`. Run the app and confirm `dotnet test` discovers and runs a test. | `Set up solution and tests` |
| 2. Model the session | Implement the base transition table. Test that `Suspended` → `Active` succeeds and `Disconnected` → `Active` is blocked. | `Enforce session state transitions` |
| 3. Send and audit | Add queue and audit interfaces. Use in-memory implementations in tests. Verify that an inactive send leaves both empty and that active sends queue the payload and record UTC time, message length, and increasing sequence numbers. | `Send and audit messages in active sessions` |
| 4. Wire up queue selection | Add simulated DATQ and select the implementation through configuration and dependency injection. Run the same demo with `Sim` and `Datq` by changing only the configuration. | `Select queue implementation from configuration` |
| 5. Add reconnection | Implement the `Reconnecting` transitions. Test one allowed and one blocked transition involving the new state. Record which parts of the code changed. | `Add reconnecting session state` |
| 6. Decide how suspended sends behave | Pick a policy, test what the caller sees and what reaches the queue, and document the trade-off. | `Define behavior for sends while suspended` |

The README deliverables and reliability questions are listed above.

### GitHub workflow

`main` tracks `origin/main`. The milestone workflow uses checks from the repository root, a staged review with `git status --short` and `git diff --cached`, and a commit and push when the learner is ready.

On another computer, install the .NET 11 SDK, then clone the repository or pull `main` into an existing checkout. Open the folder containing `courseProj.sln` in Codex and ask it to read `AGENTS.md` and the current progress in this file. Run `dotnet build`, `dotnet test`, and `dotnet run --project Backend` from that folder. The default `Sim` mode should send one demo message and record one audit entry. Set the DATQ environment variables on that machine when you want to run the DATQ simulator.

## Starting state

- The original `Backend` project was a .NET 11 ASP.NET Core Web API scaffold with `/weatherforecast`, development OpenAPI setup, and an example HTTP request file. Step 1 converted it to a console app.
- The workspace initially had no solution, test project, or Git repository. The installed SDK list showed `11.0.100` only when this handoff was written.
- Visual Studio Community's bundled Git worked when invoked by its full path. Check `git --version` in the terminal you plan to use before following the GitHub workflow.
