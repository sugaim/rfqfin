# 07 — Error Taxonomy, Error Boundaries, and Incident Reporting

Baseline commit:

```text
b0fc1943d7f61270a11651d6f4d2b0f0b9fcb813
```

This refactoring establishes a single error-classification model across Domain, Application, API, Bulk operations, and background workers.

Do not add unrelated features or refactor unrelated code.

---

## 1. Goals

This refactoring must achieve all of the following:

1. Expected operational failures are explicitly classified by the code that throws them.
2. Unexpected/invariant failures are never accidentally converted into normal 4xx responses.
3. API and Bulk use the same semantic error classification.
4. HTTP status, logging policy, Bulk continuation policy, and incident delivery remain caller/Host concerns.
5. Unexpected errors can be reported through an infrastructure-independent Host abstraction.
6. API responses and incident records share a correlation/trace id.
7. BCL exception types are not used as the public expected-error contract.

---

## 2. Core classification model

Add a shared RFQ error hierarchy in `Rfq.Domain/Errors`.

Use Domain because it is already the innermost assembly referenced by Application, Infrastructure, and API.
Do not create a new shared/core project only for exceptions.

### `RfqErrorKind`

```csharp
public enum RfqErrorKind
{
    Validation,
    InvalidState,
    VersionConflict,
    NotFound,
    Forbidden,
    CalculationFailure,
}
```

### Base exception hierarchy

```csharp
public abstract class RfqException : Exception
{
    protected RfqException(string message) : base(message) { }

    protected RfqException(string message, Exception innerException)
        : base(message, innerException) { }
}

public abstract class ExpectedRfqException : RfqException
{
    protected ExpectedRfqException(RfqErrorKind kind, string message)
        : base(message) => Kind = kind;

    protected ExpectedRfqException(
        RfqErrorKind kind,
        string message,
        Exception innerException)
        : base(message, innerException) => Kind = kind;

    public RfqErrorKind Kind { get; }
}

public class RfqInvariantException : RfqException
{
    public RfqInvariantException(string message) : base(message) { }

    public RfqInvariantException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

The exception hierarchy must describe semantic failure meaning only.

Do **not** add any of these to exception types:

```text
HttpStatusCode
ShouldNotifyDeveloper
LogLevel
Retryable
ContinueBulk
```

Those are caller / transport / operational policies.

---

## 3. Existing Domain exceptions

Update the existing Domain exceptions as follows.

```text
DomainValidationException
    : ExpectedRfqException
    Kind = Validation

DomainRuleViolationException
    : ExpectedRfqException
    Kind = InvalidState

StateVersionMismatchException
    : ExpectedRfqException
    Kind = VersionConflict

DomainInvariantException
    : RfqInvariantException
```

`DomainInvariantException` remains the specific type for broken Domain-model invariants.

`RfqInvariantException` is used for impossible/inconsistent RFQ-system state outside the Domain model, including Application and Infrastructure invariants.

The existing `DomainException` abstraction is not currently used as a catch/policy contract. Remove it if it is no longer needed after this hierarchy is introduced.

---

## 4. Application expected exception types

Add explicit expected Application-level exception types.

```text
RfqRequestValidationException
    : ExpectedRfqException
    Kind = Validation

RfqNotFoundException
    : ExpectedRfqException
    Kind = NotFound

RfqForbiddenException
    : ExpectedRfqException
    Kind = Forbidden
```

Update:

```text
CalculationFailureException
    : ExpectedRfqException
    Kind = CalculationFailure
```

Keep the existing calculation-specific `Code` property.
It is separate from `RfqErrorKind`.

---

## 5. Expected vs Unexpected

### Expected

An expected error is one that can occur during normal operation because of:

- invalid client input
- business state
- authorization
- missing requested resource
- optimistic concurrency/version conflict
- calculation failure explicitly returned by the calculation engine

Expected errors may be exposed as 4xx HTTP responses.

### Unexpected

Unexpected failures include:

```text
DomainInvariantException
RfqInvariantException
InvalidOperationException
ArgumentOutOfRangeException
NullReferenceException
database/network/provider failures
unclassified exceptions
```

Unexpected failures must not be converted to normal expected errors simply because of their BCL type.

---

# 6. Application throw-site changes

Audit and implement the following changes.

## `Rfq.Application/Authorization/RfqAuthorization.cs`

Change the following remaining business-state `InvalidOperationException` cases to `DomainRuleViolationException`:

```text
Unowned RFQs do not require Take Over.
The RFQ is already owned by this Trader.
Quotes can only be edited for an Open RFQ.
WorkingQuote editing requires QuoteStatus Requested.
Only a Closed RFQ outcome can be corrected.
```

Change request-validation `ArgumentException` cases to `RfqRequestValidationException`:

```text
Confirmation is required to pick up an RFQ assigned to another Trader.
Strong confirmation is required to take over an owned RFQ.
```

Change intentional authorization failures from `UnauthorizedAccessException` to `RfqForbiddenException`, including:

```text
Only the owning Trader can release the RFQ.
Only the owning Trader can edit the WorkingQuote.
Only the owning Trader can Confirm the WorkingQuote.
Only the current Contact Owner can Present or Unpresent the RFQ.
Only the owning Trader can withdraw a quote.
Only the current Contact Owner can change the Revision.
Only the current Contact Owner can ... the RFQ.
The Sales role is required.
The Trader role is required.
The Sales or Trader role is required.
```

Keep already-correct `DomainRuleViolationException` cases.

---

## `WorkingQuoteModels.cs`

Change concurrency failures:

```text
The RFQ was changed by another user.
The WorkingQuote was changed by another user.
```

from `InvalidOperationException` to `StateVersionMismatchException`.

Change:

```text
WorkingQuote was not found.
```

to `RfqInvariantException`.

Reason: this path already refers to an existing RFQ/revision and the missing WorkingQuote is an internal consistency failure, not a user-requested missing resource.

---

## `CalculateWorkingQuote.cs`

Change these concurrency failures to `StateVersionMismatchException`:

```text
The RFQ state changed while calculation was in progress.
The RFQ was changed by another user.
The WorkingQuote was changed by another user.
```

Change:

```text
RFQ Case '{caseId}' was not found.
```

to `RfqNotFoundException`.

Change reload-time:

```text
WorkingQuote was not found.
```

to `RfqInvariantException`.

Keep unknown enum/programmer errors such as:

```csharp
ArgumentOutOfRangeException(nameof(driver))
```

unexpected.

Update `CalculationFailureException` to participate in the new expected hierarchy.

---

## `ConfirmQuote.cs`

Keep version mismatch as `StateVersionMismatchException`.

Change:

```text
WorkingQuote was not found.
```

to `RfqInvariantException`.

This is an internal inconsistency for an existing RFQ/revision.

---

## `WithdrawQuote.cs`

Change:

```text
Current quote was not found.
```

from `InvalidOperationException` to `RfqInvariantException`.

---

## `PresentQuote.cs`
## `UnpresentQuote.cs`

Change:

```text
Current ConfirmedQuote was not found.
```

from `InvalidOperationException` to `RfqInvariantException`.

Keep existing `DomainInvariantException` checks.

---

## `ScratchPricer.cs`

Change:

```text
Unknown calculation response.
```

from `InvalidOperationException` to `RfqInvariantException`.

Keep `ArgumentOutOfRangeException` for impossible enum/programmer input.

---

## `InitialRfqFactory.cs`

Change requested Client missing:

```text
Client ... was not found.
```

to `RfqNotFoundException`.

Change authoritative settlement mismatch from `ArgumentException` to `RfqRequestValidationException`.

Keep `ArgumentNullException.ThrowIfNull(command)` as a programmer guard.

---

## `UpdateInitialDraft.cs`

Change RFQ Case missing to `RfqNotFoundException`.

Change settlement mismatch from `ArgumentException` to `RfqRequestValidationException`.

---

## `AssignedTraderValidator.cs`

Change target trader missing to `RfqNotFoundException`.

Change invalid trader role/desk from `ArgumentException` to `RfqRequestValidationException`.

Keep null guards as programmer guards.

---

## `ConfirmInitialDraft.cs`

Change settlement mismatch from `ArgumentException` to `RfqRequestValidationException`.

---

## `ResolveRfqCreationContext.cs`

Change requested Security missing to `RfqNotFoundException`.

These two conditions indicate broken required routing/master configuration and must be unexpected:

```text
Assigned Trader ... was not found.
The configured Assigned Trader must be a Trader on the current user's desk.
```

Change them to `RfqInvariantException`.

---

## `ChangeContactOwner.cs`

Change:

```text
Contact Owner handoff requires confirmation.
Contact Owner must be a Sales or Trader user on the same desk.
```

to `RfqRequestValidationException`.

Change missing target user to `RfqNotFoundException`.

---

## `ClosedRfqUseCase.cs`
## `OwnershipUseCase.cs`

Change route/requested RFQ Case missing from `KeyNotFoundException` to `RfqNotFoundException`.

---

## `UpdateSalesMemo.cs`
## `UpdateTraderMemo.cs`

Change missing memo row for an existing RFQ to `RfqInvariantException`.

Change intentional desk-scope authorization failure to `RfqForbiddenException`.

---

# 7. Domain changes

Existing Domain semantics are mostly correct.

Keep:

```text
DomainValidationException   -> Validation
DomainRuleViolationException -> InvalidState
StateVersionMismatchException -> VersionConflict
DomainInvariantException    -> unexpected invariant
```

### `StateVersion.cs`

The overflow path in `StateVersion.Next()` is not user validation.

Change:

```text
OverflowException -> DomainValidationException
```

to:

```text
OverflowException -> DomainInvariantException
```

or equivalent `RfqInvariantException`.

Prefer `DomainInvariantException` because this is a Domain value invariant/exhaustion condition.

---

# 8. Infrastructure throw-site changes

## `EfCoreCategoryRouting.cs`

Change admin-requested missing resources:

```text
Category ... was not found.
Trader ... was not found.
```

to `RfqNotFoundException`.

Change invalid requested trader role/desk from `ArgumentException` to `RfqRequestValidationException`.

Change required configuration absence:

```text
No default Assigned Trader is configured...
No routing row exists...
```

to `RfqInvariantException`.

These are system configuration failures, not 404s.

---

## `EfCoreQuoteExpirySettings.cs`

Missing current-user master row is a system inconsistency:

```text
User ... was not found.
```

Change to `RfqInvariantException`.

Change invalid quote-expiry persistence request:

```text
Quote expiry must be a whole number of minutes.
```

from `ArgumentException` to `RfqRequestValidationException`.

Keep unknown QuoteExpiry policy as `DomainInvariantException`.

---

## `EfCoreDeskLocalDateResolver.cs`
## `EfCoreRfqSearchQueries.cs`
## `EfCoreEodQueries.cs`

Missing current-user Desk master row is not a user 404.

Change `KeyNotFoundException` to `RfqInvariantException`.

---

## `WorkingQuoteRepository.cs`

Change:

```text
WorkingQuote was not found.
Confirmed Revision is missing SettlementDate.
```

to `RfqInvariantException`.

Keep repository contract failure:

```text
The WorkingQuote must be loaded before it can be updated.
```

as `InvalidOperationException`.

That is a programmer/repository usage failure and must remain unexpected.

---

## `RfqCaseRepository.cs`

Change persisted-state:

```text
Unsupported RFQ lifecycle.
```

from `InvalidOperationException` to `RfqInvariantException` or `DomainInvariantException`.

Prefer `RfqInvariantException` because this is persistence/system materialization rather than Domain transition logic.

Keep:

```text
The RFQ Case must be loaded before it can be updated.
The RFQ Revision must be loaded before it can be updated.
```

as `InvalidOperationException`.

These are repository usage/programmer contract failures.

Keep existing persisted-state `DomainInvariantException` checks.

---

## `RfqMemoRepository.cs`

Keep:

```text
must be loaded before update
```

as `InvalidOperationException`.

These are repository usage failures.

---

## `EfCoreBusinessDateProvider.cs`

Missing business-date configuration is unexpected.

It may remain `InvalidOperationException` because the new API policy will map it to 500.

Changing it to `RfqInvariantException` is acceptable if it improves consistency, but do not treat it as an expected error.

---

## Infrastructure startup/tool failures

Do not convert infrastructure/process configuration errors merely to fit the taxonomy.

Examples:

```text
DependencyInjection.cs
DatabaseOperations.cs
PostgreSqlResetDevSafetyGuard.cs
DeskDateBoundary.cs
```

Their `InvalidOperationException` / argument guards may remain.

They are not part of the HTTP expected-error contract.

---

## Persistence/event serialization

Keep the current invariant-oriented behavior in:

```text
PersistedEventSink
EventPersistenceContract
QuotePayloadPersistence
PersistenceJsonSerializer
EfCoreTraderRfqQueries
PostgreSqlUnitOfWork
```

Persisted invalid state should remain unexpected.

---

# 9. API contract mapping

## `ApiErrorMiddleware.cs`

Remove the current direct BCL/type switch:

```text
CalculationFailureException
UnauthorizedAccessException
KeyNotFoundException
StateVersionMismatchException
DomainRuleViolationException
DomainValidationException
InvalidOperationException
ArgumentException
```

Replace it conceptually with:

```csharp
exception switch
{
    ExpectedRfqException expected => Map(expected.Kind),
    _ => InternalServerError,
};
```

HTTP mapping:

```text
Validation         -> 400 / Validation
InvalidState       -> 409 / InvalidState
VersionConflict    -> 409 / VersionConflict
NotFound           -> 404 / NotFound
Forbidden          -> 403 / Forbidden
CalculationFailure -> 422 / CalculationFailure
```

For expected errors:

- return the expected exception message as ProblemDetails detail
- do not `LogError`
- use the semantic code above in ProblemDetails

For unexpected errors:

```text
HTTP status = 500
code = InternalServerError
detail = "An unexpected error occurred."
```

Do not expose the exception message.

Unexpected errors must be sent to `IIncidentReporter`.

---

# 10. Incident reporting abstraction

Add a Host/API-level abstraction.

Do **not** put it in Domain or Application.

Initial location may be under `Rfq.Api`, for example:

```text
Rfq.Api/Incidents/
```

### Contract

```csharp
public interface IIncidentReporter
{
    Task ReportAsync(
        Incident incident,
        CancellationToken cancellationToken = default);
}

public sealed record Incident(
    Exception Exception,
    string Source,
    string? TraceId = null,
    string? Operation = null);
```

The reporter must **not** decide whether an exception is expected or unexpected.

Classification belongs outside the interface.

The caller decides that an incident must be reported, then calls `IIncidentReporter`.

Do not add `ShouldNotifyDeveloper` to exceptions or `Incident`.

---

## Initial implementation

Add a logging-backed implementation:

```csharp
public sealed class LoggingIncidentReporter(
    ILogger<LoggingIncidentReporter> logger)
    : IIncidentReporter
{
    public Task ReportAsync(
        Incident incident,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(
            incident.Exception,
            "Unexpected failure. Source={Source} TraceId={TraceId} Operation={Operation}",
            incident.Source,
            incident.TraceId,
            incident.Operation);

        return Task.CompletedTask;
    }
}
```

Register it in DI.

The API middleware itself should not also `LogError`, otherwise the same incident is logged twice.

Future integrations such as Sentry, Application Insights, OpenTelemetry exporters, or internal alerting are outside this task.

---

# 11. Incident reporter failure semantics

Incident reporting is best-effort.

A failure inside an incident reporter must not:

- prevent the API from returning the original generic 500 response
- terminate the background worker loop solely because alert delivery failed
- replace the original exception context with the reporter exception

Implement this in a simple way appropriate for the current logging-only reporter.

Do not build a retry/deduplication/alerting subsystem.

---

# 12. Correlation / trace id

Expose the same trace id in both the HTTP response and the incident.

For API unexpected and expected error responses, add:

```text
ProblemDetails.Extensions["traceId"]
```

using:

```csharp
context.TraceIdentifier
```

For unexpected errors, pass the same value to:

```csharp
Incident.TraceId
```

Also include a concise operation string:

```text
"{HTTP_METHOD} {PATH}"
```

Do not include full request bodies or sensitive RFQ data in Incident by default.

---

# 13. `QuoteExpiryWorker`

Current code catches and ignores `InvalidOperationException` per candidate:

```csharp
catch (InvalidOperationException)
{
    /* raced with a normal command */
}
```

Remove this catch.

`ExpireQuote.ExecuteAsync` already treats normal race/business-state changes as:

```text
DomainRuleViolationException -> false
```

Therefore an `InvalidOperationException` reaching the worker is unexpected and must not be silently swallowed.

For the worker outer boundary:

```text
OperationCanceledException when stoppingToken is cancelled
    -> normal shutdown, do not report

other Exception
    -> report through IIncidentReporter
```

Use an incident source such as:

```text
QuoteExpiryWorker
```

Do not classify inside `IIncidentReporter`.

Avoid double logging.

---

# 14. Bulk

Refactor Bulk to use the new semantic error kind rather than enumerating exception classes.

Conceptually:

```csharp
catch (ExpectedRfqException exception)
    when (TryMap(exception.Kind, out var code))
{
    unitOfWork.DiscardChanges();
    ...
}
```

Bulk may continue only for explicitly supported kinds.

Map:

```text
Validation      -> BulkFailureCode.Validation
InvalidState    -> BulkFailureCode.InvalidState
VersionConflict -> BulkFailureCode.VersionConflict
NotFound        -> BulkFailureCode.NotFound
Forbidden       -> BulkFailureCode.Forbidden
```

Do not add `CalculationFailure` to Bulk mapping now.
Current calculation use cases are not Bulk operations.

An expected exception kind not explicitly supported by Bulk must propagate/abort rather than being silently treated as recoverable.

BCL exceptions must not be caught as recoverable Bulk failures.

---

# 15. API layer throw-site changes

## `QuoteApiContract.cs`

For request-to-domain conversion, change client-input failures to `RfqRequestValidationException`:

```text
none expiry must not include minutes
after expiry requires positive integer minutes
unknown request Quote Expiry type
unknown request Calculation Driver
unknown request Quote Mode
```

For Domain-to-API response mapping, unknown enum/policy remains unexpected:

```text
Unknown Quote Expiry policy
Unknown Calculation Driver
Unknown Quote Mode
```

These may remain `InvalidOperationException` or use `RfqInvariantException`.

Do not classify them as expected client errors.

---

## `DevelopmentCurrentUser.cs`

Unknown development identity may be changed from `UnauthorizedAccessException` to `RfqForbiddenException`.

This is development-only and lower priority, but classification should be consistent if touched.

---

## `MeController.cs`
## `EventsController.cs`
## `RfqDraftsController.cs`

Unknown enum or impossible response state is unexpected.

Do not convert these to expected validation errors.

Existing `InvalidOperationException` is acceptable, or use `RfqInvariantException` if clearer.

---

# 16. DbTool

`Rfq.DbTool/Program.cs` is a CLI process boundary.

Its top-level:

```csharp
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
```

is outside the API incident/error-taxonomy behavior.

Do not redesign DbTool in this task.

---

# 17. Persistence materialization caveat

Be aware of this subtle issue:

Domain value constructors such as:

```text
UserId.Create(...)
new CaseId(...)
new StateVersion(...)
```

can throw `DomainValidationException`.

When those values come from a persisted DB row, a validation failure means persisted-data corruption/inconsistency, not bad client input.

Some persistence/event code already correctly wraps such failures in `DomainInvariantException`.

Do not perform a broad persistence rewrite in this task.

However:

- if a touched materialization path clearly turns persisted bad data into `DomainValidationException`, wrap it as `RfqInvariantException` / `DomainInvariantException`
- do not blindly expose persisted-data validation as HTTP 400

A broader persistence hardening pass may remain deferred.

---

# 18. Tests

Add or update tests for all policy boundaries.

## API middleware

Verify:

```text
Validation         -> 400
InvalidState       -> 409
VersionConflict    -> 409
NotFound           -> 404
Forbidden          -> 403
CalculationFailure -> 422
```

Verify expected errors include `traceId`.

Verify:

```text
DomainInvariantException  -> 500 + generic detail
RfqInvariantException     -> 500 + generic detail
InvalidOperationException -> 500 + generic detail
ArgumentException         -> 500 + generic detail
KeyNotFoundException      -> 500 + generic detail
UnauthorizedAccessException -> 500 + generic detail
```

These BCL cases are important:
they prove an unclassified exception does not accidentally become a normal 4xx response.

Verify unexpected errors are reported exactly once through `IIncidentReporter`.

Verify the incident trace id matches `ProblemDetails.traceId`.

---

## Bulk

Verify all supported `RfqErrorKind` values map correctly:

```text
Validation
InvalidState
VersionConflict
NotFound
Forbidden
```

Verify they:

```text
-> Failed
-> DiscardChanges
-> continue to next item
```

Verify:

```text
CalculationFailure
InvalidOperationException
DomainInvariantException
RfqInvariantException
unknown Exception
```

are not silently converted into recoverable item failures.

They must abort unless explicitly supported.

---

## QuoteExpiryWorker

Verify:

- cancellation during shutdown is not reported as an incident
- unexpected `InvalidOperationException` is no longer swallowed
- unexpected scan/use-case failure is reported through `IIncidentReporter`
- worker error handling does not duplicate logging

---

## Representative use-case tests

Have representative tests proving each semantic category is thrown by the intended layer:

```text
request validation -> RfqRequestValidationException
business state      -> DomainRuleViolationException
version conflict    -> StateVersionMismatchException
requested missing resource -> RfqNotFoundException
authorization       -> RfqForbiddenException
impossible internal state -> RfqInvariantException / DomainInvariantException
calculation error   -> CalculationFailureException
```

Do not add redundant tests for every identical throw site if representative coverage plus existing tests already prove the behavior.

Run the full backend test suite.

If frontend types or generated contracts are affected, run the existing frontend checks as well.

---

# 19. Non-goals

Do not implement in this refactoring:

```text
Sentry
Application Insights
PagerDuty
OpenTelemetry alert routing
retry policy
incident severity taxonomy
incident deduplication
rate limiting
alert escalation
remote notification delivery
generic Result<T> migration
global exception hierarchy beyond what is needed here
new Bulk operations
EOD redesign
```

Do not add a generic notification policy engine.

Do not move business logic into middleware.

Do not make exception types carry HTTP or operational policy.

---

# 20. Completion criteria

The refactoring is complete when:

1. Expected operational failures are represented by `ExpectedRfqException + RfqErrorKind`.
2. Unexpected failures no longer rely on BCL type-to-4xx mapping.
3. `InvalidOperationException` returns 500 from API and aborts Bulk.
4. `KeyNotFoundException`, `ArgumentException`, and `UnauthorizedAccessException` are no longer globally interpreted as expected API errors.
5. API mapping depends on `RfqErrorKind`.
6. Bulk mapping depends on `RfqErrorKind`.
7. `IIncidentReporter` exists at the Host/API boundary.
8. API middleware reports unexpected failures through the reporter.
9. QuoteExpiryWorker reports unexpected failures through the reporter.
10. Incident reporting does not classify exceptions.
11. Incident reporting failure cannot replace the original HTTP/worker error behavior.
12. API ProblemDetails contains `traceId`.
13. unexpected incident records carry the same trace id.
14. no duplicate `LogError` occurs between middleware/worker and `LoggingIncidentReporter`.
15. full tests pass.
