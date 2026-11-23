# Exchange session and queue demo

## Run the project

Install the .NET 11 SDK, then run these commands from the solution folder. The default queue is `Sim`.

```powershell
dotnet build
dotnet test
dotnet run --project Backend
```

## Choose a queue

`Sim` uses the in-memory queue. To use the DATQ simulator, change `Queue:Type` to `Datq` and run these PowerShell commands.

```powershell
$env:Queue__Datq__Server = "localhost"
$env:Queue__Datq__Password = "demo-password"
dotnet run --project Backend
```

Both modes should print `Sent: True` and `Audit entries: 1`.

## Session design

Each `ExchangeSession` starts in `Disconnected`. Its private setter keeps state changes inside the class. `TryTransitionTo` checks a `HashSet` of allowed `(from, to)` pairs, so the rules live in one place.

`ExchangeMessageSender` sends only while `Active`. Normal sends and recovered sends both record the UTC time, message length, and an increasing sequence number in the in-memory audit log when the send is confirmed. The audit sequence starts again with each new sender. The SQLite outbox keeps the durable confirmation time, message length, and its own sequence number across restarts.

## Reconnecting

Adding `Reconnecting` changed two places in the application code. The `SessionState` enum defines the new state, and `ExchangeSession.CreateAllowedTransitions` lists its four allowed pairs. The existing lookup handles those pairs. Tests cover `Active` → `Reconnecting` and the blocked `Reconnecting` → `Active` transition.

## Sending while suspended

`TrySend` returns `false` immediately while `Suspended`. The test checks that the queue and audit log stay empty even after the session resumes. The caller gets immediate feedback and must keep the message to retry it once the session is `Active`.

## Interface versus abstract class

Use an interface for a shared operation. `IMessageQueue` lets the sender call `Send` with either queue, and `IAuditLog` lets tests supply an in-memory log. Use an abstract class when related implementations need shared code or state. A queue base class could handle common validation and leave the send operation to its subclasses.

## `ChannelManager` code review

- Public string state lets callers assign invalid values or bypass transition rules.
- A public static queue selector shares mutable configuration across sessions and tests.
- Creating the queue client inside `Send` makes it harder to substitute a test queue and can repeat connection setup.
- Returning silently outside `Active` leaves the caller guessing whether the message was sent.
- An audit log with local timestamps and message lengths makes events hard to order across time zones and lacks sequence numbers for tracking sends.

## Reliability

Run `dotnet run --project Backend -- --Queue:Type=DurableSim` to try automatic recovery. The SQLite outbox and receiver database live in the local app-data `courseProj` folder. Use `--Storage:Directory=<path>` to choose another folder. `DurableSim` retries unconfirmed messages with their original IDs, and the receiver keeps one receipt per ID. `Sim` and `Datq` report unconfirmed IDs for manual review.

When recovery confirms a message, it records one audit entry using the receiver's original UTC receipt time. Running recovery again skips the confirmed message.

- **Restart.** Store the last contiguous sequence durably per stream. Commit it with the message and an outbox entry, then resume from the next sequence. The outbox holds messages until they can be published to the bank queue.
- **Crash after a database write.** Replays can deliver the same message again. Enforce a unique `(stream, sequence number)` key and treat a duplicate insert as already processed. Use the same ID to deduplicate queue deliveries.
- **Database outage.** Publish only messages already committed to the outbox. Leave new messages available for replay without advancing the checkpoint. If the source cannot replay, use durable buffering and slow intake when the buffer fills.

## What I would do differently

For a longer-running service, I would add an operator command to inspect and resolve uncertain `Sim` and `Datq` sends. The demo prints the pending IDs and stops. Automatic retries would require the receiver to keep track of message IDs across restarts.

## Continue in a new AI chat

On another computer, install the .NET 11 SDK, pull `main`, and open the folder containing `courseProj.sln`. Start a chat there and paste this prompt.

> Read `AGENTS.md` and the Current progress section of `docs/ASSIGNMENT.md`. Check Git status, then summarize where I left off and pick up from my next question.
