# ADR-013: Boundary validation with endpoint filters, business rules as results

- Status: Accepted
- Date: 2026-09-22
- Source: Delivery decision (implementation)

## Context

The original plan described validation, logging, transactions, and idempotency as ordered decorators
around request handlers. During implementation two facts changed the shape of that plan:

1. Handlers are registered in the container as `ICommandHandler<,>`/`IQueryHandler<,>` and are invoked
   directly by minimal API endpoints. Minimal APIs bind method arguments **before** filters run, so a
   handler's dependencies are constructed even when the request is invalid — a decorator that short
   circuits must sit in front of binding to avoid that cost.
2. Field-level validation errors must reach the client with field names (RFC 9457 `errors`), which is a
   transport concern, not a use-case concern.

## Decision

- **Field validation** lives in `IRequestValidator<TRequest>` implementations in the Application layer,
  validating the bound request contract. It runs in a minimal API `IEndpointFilter`
  (`ValidationEndpointFilter<TRequest>`) which returns a `ValidationProblem` with per-field messages and
  the stable code `request.validation_failed`.
- **Business rules** live in the domain and surface as `Result` failures carrying a stable `Error.Code`.
  Domain rule violations (`DomainException`) are translated to `Result` failures at the handler boundary
  via `DomainExceptionMapping.ToError`, so the API returns one consistent problem-details shape.
- **Cross-cutting concerns** are applied where the framework owns the boundary: authentication and
  authorization as ASP.NET policies, correlation and tenant resolution as middleware, atomicity through
  `IUnitOfWork.ExecuteInTransactionAsync`, and idempotency through `IIdempotencyStore` inside the
  payment submission use case.

## Consequences

- Invalid requests are rejected before any handler or database work happens.
- One error contract for validation, business conflicts, and faults: problem details with `code` and
  `correlationId`, plus `errors` when field-level detail exists.
- Handlers remain free of transport concerns and are directly testable, which is what the 84 unit tests
  exercise.
- Two mechanisms (endpoint filter + handler result) must be understood together; this is a deliberate
  trade for correct binding order and a single error contract.

## Related correction

`InvariantGlobalization` was initially enabled project-wide. `Microsoft.Data.SqlClient` refuses to open
connections in invariant mode, which would have made every SQL-backed endpoint fail at runtime. It was
disabled during integration testing; all culture-sensitive formatting in application code already uses
`CultureInfo.InvariantCulture` explicitly, so behaviour is unchanged.
