# Agent notes

- Run commands from repo root. Restore with `dotnet restore Calendar.slnx --configfile NuGet.Config` and `npm --prefix frontend ci`. `NuGet.Config` clears inherited feeds: use only public nuget.org; do not alter global/user NuGet configuration.
- Start separately: `dotnet run --project src/Calendar.Api --launch-profile http` (port 5005) and `npm --prefix frontend run dev`. Vite proxies `/api` to the backend **only in development**; production hosting must route `/api` separately.
- Mirror `.github/workflows/ci.yml`: `dotnet format Calendar.slnx --verify-no-changes --no-restore`; `dotnet build Calendar.slnx -c Release -warnaserror --no-restore`; `dotnet test Calendar.slnx -c Release --no-build --no-restore`; `npm --prefix frontend run lint`; `npm --prefix frontend run build`. Restore first; test with `--no-build` only after a Release build. Frontend lint is Oxlint (not ESLint); build also checks TypeScript.
- Focused backend test: `dotnet test tests/Calendar.Api.Tests/Calendar.Api.Tests.csproj --filter FullyQualifiedName~HealthTests`. Tests use `WebApplicationFactory<Program>`; do not start a separate API server for them.
- Current API only has `GET /api/health`; UI only reports API availability. Booking is not implemented.
- Keep `.github/workflows/hexlet-check.yml` and the repository name unchanged. This separate Hexlet check currently expects a root Dockerfile; its failure does not mean our `.github/workflows/ci.yml` failed.
- Agent-written commits, including squash-merge subjects, must follow Conventional Commits (`feat:`, `fix:`, `test:`, `ci:`, `docs:`, `chore:`); release-please runs on pushes to `main`. Release PRs generated with `GITHUB_TOKEN` may have no automatic CI: verify their exact head before merging. Do not merge a release PR just to test the release workflow.

## Agent skills

### Issue tracker

Track specs and tickets in this repository's GitHub Issues. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the default five triage labels. See `docs/agents/triage-labels.md`.

### Domain docs

Use a single-context layout: root `GLOSSARY.md` and `docs/adr/`. See `docs/agents/domain.md`.
