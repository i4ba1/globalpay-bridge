# Repository and release

GitHub metadata and the exact steps to publish this project. Replace `OWNER` with the GitHub account that
will host the repository (this workspace is authenticated as `i4ba1`).

## Name

| | |
|---|---|
| **Repository** | `globalpay-bridge` |
| Full name | `OWNER/globalpay-bridge` |
| Local folder | `GlobalPayBridge` |

Alternatives if the name is taken: `globalpay-bridge-api`, `globalpay-bridge-dotnet`.

GitHub repository names are conventionally lower-case with hyphens. The .NET solution, namespaces and
Docker project names deliberately keep `GlobalPay`/`GlobalPayBridge` because .NET identifiers cannot
contain hyphens.

## Description

For the repository **About** field (350 character limit):

> Azure-native, multi-tenant payroll integration platform: ASP.NET Core + Azure Functions, Clean
> Architecture and DDD, transactional outbox/inbox, idempotent payments, HMAC-verified webhooks, T-SQL
> procedures, Bicep IaC, and an 18-step Swagger walkthrough captured as GIFs. Portfolio-grade, fully
> tested.

Short alternative:

> Multi-tenant payroll integration platform on .NET 10 — Clean Architecture, outbox/inbox, idempotent
> payments, verified webhooks, Azure-ready.

Set the website field to the walkthrough gallery if you publish GitHub Pages, otherwise leave it empty.

## Topics

GitHub allows 20 topics (lower-case, hyphens, max 50 characters). Recommended set:

```
dotnet
csharp
aspnet-core
dotnet-10
clean-architecture
domain-driven-design
solid-principles
multi-tenant
payroll
entity-framework-core
azure
azure-functions
bicep
keycloak
outbox-pattern
idempotency
webhooks
xunit
openapi
playwright
```

Swap-ins if you want different emphasis: `dapper`, `testcontainers`, `github-actions`, `cqrs`,
`portfolio-project`, `oidc`, `swagger`, `sql-server`.

## License

**MIT** — see [LICENSE](../../LICENSE).

Chosen because this is a portfolio project intended to be read, cloned and learned from; MIT is the most
permissive common choice and adds no friction for reviewers. The copyright line currently reads
`Copyright (c) 2026 GlobalPay Bridge`; change it to your name or handle before publishing if you prefer
personal attribution.

If you later want the code visible but not reusable for commercial training products, Apache-2.0 (patent
grant) or BUSL-1.1 are the usual alternatives.

GitHub detects the license automatically from `LICENSE` and shows it in the About sidebar.

## Release

First release: **`v0.1.0`** — "Portfolio implementation baseline".

`0.x` rather than `1.0.0` is deliberate: the PRD describes this as a portfolio baseline, the R3 export
and replay gaps are still open, and the Bicep templates have never been deployed. Move to `1.0.0` once
those are closed.

Release notes live in [`.github/releases/v0.1.0.md`](../../.github/releases/v0.1.0.md). The changelog is
[`CHANGELOG.md`](../../CHANGELOG.md).

### Publishing commands

```powershell
# 1. first commit (the repository has no history yet)
git add -A
git commit -m "feat: GlobalPay Bridge portfolio baseline (foundation through R3)"

# 2. create the remote and push. Use --private if you are not ready to publish.
gh repo create globalpay-bridge --public --source=. --remote=origin --push

# 3. description and topics
gh repo edit --description "Azure-native, multi-tenant payroll integration platform: ASP.NET Core + Azure Functions, Clean Architecture and DDD, transactional outbox/inbox, idempotent payments, HMAC-verified webhooks, T-SQL procedures, Bicep IaC, and an 18-step Swagger walkthrough captured as GIFs. Portfolio-grade, fully tested."

gh repo edit --add-topic dotnet,csharp,aspnet-core,dotnet-10,clean-architecture,domain-driven-design,solid-principles,multi-tenant,payroll,entity-framework-core,azure,azure-functions,bicep,keycloak,outbox-pattern,idempotency,webhooks,xunit,openapi,playwright

# 4. tag and release
git tag -a v0.1.0 -m "v0.1.0 Portfolio implementation baseline"
git push origin v0.1.0

gh release create v0.1.0 `
  --title "v0.1.0 — Portfolio implementation baseline" `
  --notes-file .github/releases/v0.1.0.md `
  docs/walkthrough/gifs/*.gif
```

Attaching the 18 walkthrough GIFs to the release publishes them as downloadable assets. The same images
are also versioned in the repository under `docs/walkthrough/gifs/`, so the gallery works without the
release.

### Release badge

Add to the top of the README if you want the current release shown:

```markdown
[![release](https://img.shields.io/github/v/release/OWNER/globalpay-bridge)](https://github.com/OWNER/globalpay-bridge/releases)
```
