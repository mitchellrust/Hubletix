**Agents**
- Repo: .NET 10, ASP.NET Core (Razor Pages), EF Core, PostgreSQL; layered as `src/Hubletix.Core`, `src/Hubletix.Infrastructure`, `src/Hubletix.Api`
- Purpose: guidance for automated or agentic contributors (formatting, build/test, conventions)

Build / Lint / Test Commands
- Build entire solution: `dotnet build` (runs across `Hubletix.sln`). Use from repo root.
- Run API locally: `dotnet run --project src/Hubletix.Api` (applies migrations on startup).
- Run EF migrations (apply):
  - `dotnet ef database update -p src/Hubletix.Infrastructure -s src/Hubletix.Api`
  - List migrations: `dotnet ef migrations list -p src/Hubletix.Infrastructure -s src/Hubletix.Api`

Repo-specific developer/agent rules
- Never commit secrets. If you need to run with secrets locally, use environment variables or a secrets store and never add them to git.

Code style & conventions (follow existing codebase)
- Language / files: C# (.cs), Razor pages (.cshtml), JSON, shell scripts. An .editorconfig is present and authoritative for formatting rules.
- Namespaces: use file-scoped namespaces (existing code uses `namespace Foo.Bar;`). Keep namespace matching folder structure under `src/`.
- Usings / imports:
  - Prefer file-scoped `using` when appropriate and remove unused `using` statements.
- Formatting:
  - Follow `.editorconfig` for all formatting.
  - Run `dotnet format` before creating PRs.
- Naming conventions:
  - Types, enums, properties, methods: `PascalCase`.
  - Method suffix for asynchronous methods: append `Async` (e.g., `StartSignupSessionAsync`).
  - Parameters and local variables: `camelCase`.
  - Private fields injected via constructor: `_camelCase` prefix (e.g., `_dbContext`).
  - Constants in this codebase often use `UPPER_SNAKE_CASE` (e.g., `SIGNUP_SESSION_ID_KEY`). Follow existing usage for consistency.
- Types & signatures:
  - Public surface area (interfaces, public classes and methods) should use explicit return types and XML doc comments (triple-slash) for important members.
  - Methods that perform I/O should be `async` and accept a `CancellationToken` as the last optional parameter with a default value.
  - Prefer returning domain models or DTOs over loosely-typed collections when part of the public API.
- Error handling:
  - Throw domain-appropriate exceptions for invalid states (the repo commonly uses `InvalidOperationException` for domain validation). When an error represents a specific domain case consider introducing a custom exception type.
  - Log useful context with `ILogger<T>` before swallowing or converting exceptions. Avoid empty `catch { }` blocks; if a catch is intentionally broad, add a comment explaining why and rethrow or log.
  - Preserve original exception when wrapping: `throw new InvalidOperationException(msg, ex);` so stack traces are preserved.
- Asynchronous & DB patterns:
  - Use EF Core async methods (`FirstOrDefaultAsync`, `SaveChangesAsync`, etc.).
  - Use explicit transactions where multiple DB writes must be atomic (`BeginTransactionAsync` + `CommitAsync`).
  - Keep repository/service methods idempotent where practical (webhooks and billing flows in this repo rely on idempotence).
- Logging:
  - Inject `ILogger<T>` into services and controllers. Use structured logging: `_logger.LogInformation("Message {Key}", value);`
  - Use appropriate log levels: Debug/Trace for verbose internal steps, Information for lifecycle events, Warning for recoverable issues, Error for failures.
- Tests:
  - Tests should be kept small, deterministic, and avoid reliance on external services. Use in-memory or testcontainers Postgres for integration tests.
  - Prefer xUnit (common for .NET) style filters when running single tests with `dotnet test`.

Copilot rules
- There is no `.github/copilot-instructions.md` file present. If such a file appears, treat it as authoritative guidance for AI-assisted edits and include a summary in AGENTS.md.

Agent workflow & commit guidance
- Agents may propose edits but should not create commits unless explicitly requested by a human. When asked to commit, create small focused commits with a concise message describing the "why" (1–2 sentences).
- If making changes that touch many files (formatting), prefer running `dotnet format` and listing the affected files in the PR rather than manual edits.
- Avoid amending historic commits or force-pushing main branches. Follow the repo's branching strategy (create feature branch + PR).

Quick references
- Run API locally: `dotnet run --project src/Hubletix.Api`
- Build: `dotnet build`
- Run single test: `dotnet test --filter "FullyQualifiedName=Namespace.Class.Method"`
- Format: `dotnet format`

If you are blocked
- Ask one precise question and include your recommended default action. Eg: "Which exception type should be used instead of InvalidOperationException? (Recommend: create DomainValidationException)".

End of AGENTS.md
