# API Contract Overview

Source of truth: `src/Services/MGF.Api/Controllers`, `src/Services/MGF.Api/Program.cs`
Change control: Update when endpoints, auth headers, or request/response models change.
Last verified: 2025-12-30

## Scope
- Internal API for MGF apps and tools.
- Authenticated endpoints use `/api/*`.
- Webhooks use `/webhooks/*`.

## Authentication
- Requests to `/api/*` require the `X-MGF-API-KEY` header.

## References
- HTTP examples: [http-examples.http](http-examples.http)
