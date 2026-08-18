# WinToWar

Real-time, region-conquest multiplayer strategy game in the style of *state.io*, with real-money entry fees and Litecoin settlement.

---

## Overview

WinToWar lets players compete on a shared, city-based map: each player starts on a randomly assigned region, every owned region produces troops passively over time, and players send troops at other regions to eliminate opponents and take the prize pool. Rooms range from free practice matches to a paid Standard queue and fully customizable VIP tables. Entry fees are charged against a USD-denominated in-app wallet, which is funded and withdrawn in Litecoin (LTC) through BTCPay Server.

The repository is a monorepo. Two of the directories below are deployable applications; the rest are tests, specification, and local tooling:

| Path | What it is |
|---|---|
| `api/` | Server-authoritative ASP.NET Core (`net10.0`) Web API. Validates and computes every game action; broadcasts state over SignalR. Owns auth, payments, and the match audit log. |
| `web/` | Next.js 16 App Router frontend. Renders the landing site, lobby, live match, wallet, and admin panel. |
| `api.Tests/` | xUnit test suite for the backend. |
| `docs/` | Module-by-module product/engineering specification (Turkish; files numbered 01-17 and 21-22). Tracked in git and treated as the source of truth for business rules. |
| `sandbox/btcpay/` | Local, self-hosted BTCPay Server **regtest** sandbox (Docker). Development tooling, not a deployment artifact. |

> [!NOTE]
> `docs/` is version-controlled (it was previously gitignored; that decision was reversed in commit `c3eb01c` so that drift between code and the written business rules shows up in diffs). `CLAUDE.md` and `.claude/` remain untracked local AI-assistant configuration and are **not** part of a fresh clone.

## Features

Verified against the current implementation in `api/` and `web/`.

- **Real-time region conquest** on a 12-region map (`api/Data/map.json` — Luxembourg-themed city names, every region with exactly 3 neighbors), with random starting-region assignment, drag-to-send troop movement, and passive per-region production.
- **Three room types**, tuned in `api/GameConfig.cs` and served through `RoomsController`:
  - **Standard** — 4-player matchmaking queue, fixed $1.00 entry fee, neutral-region defense 10, no fog of war.
  - **VIP** — creator-configurable player count (2–12), entry fee (0 to `Payment:MaxVipEntryFeeUsd`, default $500), fog-of-war toggle, neutral defense strength (1–7), and optional password / invite-token access.
  - **Practice** — free 2-player mode with no payment flow.
- **Bot matchmaking** (`BotMatchService`): if no human opponent joins within a randomized 10–15 s window after the first real player, bots fill the lobby. Bot difficulty is weighted (60 % Normal / 25 % Easy / 15 % Hard) and outcomes are computed by the same combat/economy engine — nothing is predetermined.
- **Time-based dispatch**: an attack does not empty the source region instantly. A server-side `Dispatch` releases troops in batches over real elapsed time (`GameConfig.DispatchBatchInterval*` / `DispatchBatchScale*`), so one region can dispatch to several targets without double-spending troops.
- **Two SignalR hubs**: `/hub/game` (match state and army events) and `/hub/wallet` (push balance updates to the owning user only).
- **Wallet & payments module**: USD-denominated wallet, BTCPay Server (Greenfield API) LTC invoices and on-chain payouts, a live exchange-rate oracle (CoinGecko → CoinCap fallback with a three-tier staleness policy), 10 % commission payouts, refunds for overpayment/failure, webhook-driven invoice reconciliation with signature verification and idempotency, and recently-used withdrawal-address suggestions.
- **Authentication module**: email/password registration and login, Google Sign-In and account linking, JWT access tokens plus rotating refresh tokens in an HttpOnly cookie, password reset, email verification, lockout and rate limiting, and role-based (`Player` / `Admin`) authorization.
- **Admin panel** (`/admin/*`): metrics dashboard, match inspection and audit-log queries, withdrawal approval/rejection, failed-invoice refunds, support tickets, user lookup, and a live in-memory log tail — all behind an `AdminAuthFilter` that requires `Player.Role == Admin`.
- **Match audit log**: match lifecycle events (`MatchStarted`, `RegionAttacked`, `RegionCaptured`, `PlayerEliminated`, `MatchEnded`) are written fire-and-forget to a dedicated `GameEventDbContext` and pruned after `GameConfig.MatchEventLogRetentionDays` (90 days). This applies to **all** matches, including free Practice rooms.
- **SEO scaffolding**: generated `robots.txt` and `sitemap.xml` (`web/app/robots.ts`, `web/app/sitemap.ts`) with per-page `noindex` metadata helpers in `web/lib/metadata.ts`.

## Screenshots

None. The repository has no documentation-image directory; `web/public/` contains only functional UI assets (lobby card art, landing background videos, logos, favicons) consumed by the running application.

## Technology Stack

### Backend (`api/api.csproj`)

| Package | Version | Purpose |
|---|---|---|
| .NET / ASP.NET Core Web API | `net10.0` | Runtime and web framework |
| `Microsoft.AspNetCore.OpenApi` | 10.0.3 | OpenAPI document, mapped in Development only |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | PostgreSQL provider for EF Core |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.3 | `dotnet-ef` tooling (`PrivateAssets=all`, build-time only) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.3 | JWT Bearer authentication |
| `Google.Apis.Auth` | 1.69.0 | Google ID token verification |
| SignalR | built into ASP.NET Core | `GameHub`, `WalletHub` |

### Tests (`api.Tests/api.Tests.csproj`)

| Package | Version | Purpose |
|---|---|---|
| `xunit` / `xunit.runner.visualstudio` | 2.9.3 / 3.1.4 | Test framework |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test host |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.3 | `WebApplicationFactory<Program>` end-to-end HTTP tests |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.0 | In-memory SQLite, used **only** by tests (production is PostgreSQL) |
| `coverlet.collector` | 6.0.4 | Coverage collection |

### Frontend (`web/package.json`)

| Package | Version | Purpose |
|---|---|---|
| `next` | 16.2.12 | App Router framework (Turbopack) |
| `react` / `react-dom` | 19.2.4 | UI runtime |
| `babel-plugin-react-compiler` | 1.0.0 | React Compiler, enabled via `reactCompiler: true` |
| `typescript` | ^5 | Static typing (`strict: true`) |
| `tailwindcss` / `@tailwindcss/postcss` | ^4 | Styling |
| `@microsoft/signalr` | ^10.0.0 | SignalR client for both hubs |
| `@base-ui/react` | ^1.6.0 | Headless primitives underlying `components/ui/` |
| `shadcn` / `@shadcn/react` | ^4.16.1 / ^0.2.1 | Component generator (`components.json`) |
| `recharts` | ^3.8.0 | Charts |
| `framer-motion` | ^13.0.0 | Animation |
| `embla-carousel-react` | ^8.6.0 | Carousels |
| `qrcode.react` | ^4.2.0 | Deposit-address QR codes |
| `lucide-react` | ^1.28.0 | Icons |
| `date-fns`, `class-variance-authority`, `clsx`, `tailwind-merge`, `cmdk`, `input-otp`, `react-day-picker`, `react-resizable-panels`, `tw-animate-css` | various | Supporting UI utilities |

There is **no** external client state-management library. `web/lib/game/store.ts` is a plain React hook (`useGameStore`) that owns the SignalR connection and match state; `web/lib/payments/WalletProvider.tsx` does the same for the wallet.

### Infrastructure

- **PostgreSQL** — one database, one connection string shared by all three `DbContext`s.
- **[BTCPay Server](https://btcpayserver.org/)** (Greenfield REST API) for LTC invoices and payouts. Which provider is registered depends on `Payment:Mode`: `Fake` (default) uses `FakePaymentProvider` and never touches the network; `Sandbox` and `Live` both use the real `BtcPayGreenfieldProvider`.
- **Docker** is used only by the local regtest sandbox in [`sandbox/btcpay/`](sandbox/btcpay/README.md). There is no production container image, CI workflow, or hosting configuration in the repository — see [Deployment](#deployment).

## Architecture

**Layering.** `Controller → Service → Model` on the backend, with each module owning its own `DbContext`, config class, and service subfolder. New modules are added as subfolders inside the existing `Models/`, `Services/`, `Controllers/` (backend) and `components/`, `lib/` (frontend) trees rather than as new top-level directories.

**Three loosely coupled modules** share one process and one PostgreSQL instance:

| Module | Persistence | Config |
|---|---|---|
| Game engine | **In-memory only** (`MatchManager`, `RoomService`) + audit log in `GameEventDbContext` | `GameConfig` (compile-time `const`) |
| Payments | `PaymentDbContext` | `PaymentConfig` (`IOptions`, section `Payment`) |
| Authentication | `AuthDbContext` | `AuthConfig` (`IOptions`, section `Auth`) |

**Server-authoritative engine.** `CombatService` and `MovementService` compute all outcomes; `GameHub` only relays validated actions and broadcasts the resulting `MatchState`. `EconomyTickService` is a hosted service driving production, neutral regen, dispatch batching, army arrivals, lobby timeouts, match end, and payout triggering on a `GameConfig.GameTickMs` (250 ms) tick.

**In-memory game state is a single-instance assumption.** `MatchManager` and `RoomService` are registered with `AddSingleton` and hold `Player`/`Match`/`Region`/`Army`/`Room` in process memory. Horizontal scaling would require moving that state to a shared store (e.g. Redis); this is not implemented, and match/room state does not survive a process restart.

**Wallet ↔ match integration.** `RoomEntryService` is the single point where a room join touches money: it debits `Wallet.BalanceUsd` first (durable, reversible), then reserves the match slot, and re-credits immediately if the reservation fails. There is no distributed transaction between the SQL wallet and the in-memory match, so ordering is the safety mechanism. If the balance is short, the same request returns a top-up invoice instead of an error.

**Payment provider selection is config-driven, and fails fast.** `Program.cs` reads `Payment:Mode`; for `Sandbox`/`Live` it verifies that `BtcPayBaseUrl`, `BtcPayApiKey`, `BtcPayStoreId`, and `WebhookSecret` are all non-empty and **throws on startup** if any is missing. Silently falling back to the fake provider is explicitly forbidden. `Sandbox` and `Live` share the same code path — the only difference is configuration values.

**CORS.** A single named policy (`WebClientCorsPolicy`) allows `http://localhost:3000` with credentials. There is no environment-driven origin list; a production frontend origin requires editing `Program.cs`.

## Folder Structure

```
.
├── api/                          .NET backend
│   ├── Program.cs                DI, middleware, migrations, admin seed, startup validation
│   ├── GameConfig.cs             Game-engine tunables (compile-time constants)
│   ├── AuthConfig.cs             IOptions-bound "Auth" settings
│   ├── PaymentConfig.cs          IOptions-bound "Payment" settings + PaymentProviderMode
│   ├── AdminConfig.cs            IOptions-bound "Admin" settings (largely dead, see below)
│   ├── Controllers/              REST endpoints (Auth, Matches, Rooms, Wallet, Admin*, ...)
│   ├── Hubs/
│   │   ├── GameHub.cs            SignalR: JoinMatch, LeaveLobby, StartVipMatchNow, AttackRegion
│   │   └── WalletHub.cs          SignalR: per-user wallet:{userId} group, no business logic
│   ├── Models/                   Domain entities (+ Auth/, Payments/, Rooms/, Dtos/)
│   ├── Services/
│   │   ├── Auth/                 AuthService, JwtTokenService, GoogleIdTokenValidator, AuthDbContext
│   │   ├── GameEngine/           CombatService, MovementService
│   │   ├── Matchmaking/          BotMatchService
│   │   ├── Payments/             PaymentService, WalletService, PayoutService, RefundService,
│   │   │                         RoomEntryService, BtcPayGreenfieldProvider, price oracles
│   │   ├── Rooms/                RoomService, RoomDisplayNameFormatter
│   │   ├── MatchManager.cs       In-memory match/player state
│   │   ├── EconomyTickService.cs Hosted service, 250 ms game tick
│   │   └── MatchEventLog*.cs     Audit-log writer / buffered flush service / reader
│   ├── Migrations/               One folder per DbContext: Auth, Payments, GameEvents
│   └── Data/map.json             Static map: 12 regions, 3 neighbors each, polygon geometry
├── api.Tests/                    xUnit suite (+ TestSupport/ fakes and factories)
├── docs/                         Product/engineering spec (Turkish; files 01-17, 21-22)
├── sandbox/btcpay/               Local BTCPay regtest sandbox (docker-compose, up.ps1, down.ps1)
└── web/                          Next.js frontend
    ├── app/
    │   ├── (site)/               Player-facing routes (lobi, cuzdan, giris, gecmis, legal pages)
    │   ├── admin/                Admin dashboard routes
    │   ├── game/[matchId]/       Live match screen
    │   ├── robots.ts, sitemap.ts SEO route handlers
    │   └── layout.tsx, error.tsx, not-found.tsx, globals.css
    ├── components/               admin/, auth/, game/, landing/, layout/, lobby/, payments/, rules/, ui/
    ├── lib/                      admin/, auth/, game/, payments/ clients; identity.ts; metadata.ts
    ├── hooks/                    use-mobile.ts
    └── public/                   landing/, lobby/, logo/ assets, og-image.png
```

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — verified with `10.0.103`
- [Node.js](https://nodejs.org/) compatible with Next.js 16 / React 19 — verified with `v22.15.0` / npm `11.17.0`
- A reachable [PostgreSQL](https://www.postgresql.org/) instance
- *(Optional)* [Docker](https://www.docker.com/) plus Windows PowerShell, only if you want the BTCPay regtest sandbox. Not needed with the default `Payment:Mode=Fake`.

### Clone and restore

```bash
git clone <repository-url>
cd WinToWar

# Backend (no solution file — target the project directly)
dotnet restore api/api.csproj
dotnet restore api.Tests/api.Tests.csproj

# Frontend
cd web
npm install
```

> [!NOTE]
> There is no `.sln` file in the repository. `dotnet` commands must be given a project path (`api/api.csproj`, `api.Tests/api.Tests.csproj`) or be run from inside `api/` or `api.Tests/`.

## Environment Variables

There is no `.env.example`. `web/.env` exists but is empty and gitignored. The tables below list only keys the code actually reads.

### Backend

Values live in `api/appsettings.json` (checked in, development defaults only) and can be overridden by `appsettings.{Environment}.json`, by environment variables using the ASP.NET Core double-underscore form (`Payment__Mode`, `Auth__ConnectionString`, and so on), or by `dotnet user-secrets` (a `UserSecretsId` is configured in `api.csproj`). **Never commit real secrets.**

#### `Auth` section

| Key | Default | Purpose |
|---|---|---|
| `JwtSigningKey` | empty in code, dev placeholder in `appsettings.json` | Symmetric key for signing and validating JWT access tokens |
| `JwtIssuer` / `JwtAudience` | `WinToWar` / `WinToWar` | JWT issuer and audience claims |
| `GoogleClientId` | empty | Expected audience when validating Google ID tokens |
| `SeedAdminEmail` / `SeedAdminPassword` | dev values in `appsettings.json` | If both are non-empty, one Admin account is seeded at startup, and only if no player with that email exists |
| `ConnectionString` | `Host=localhost;Port=5432;Database=wintowar;Username=postgres;Password=postgres` | PostgreSQL connection for `AuthDbContext` |
| `AccessTokenLifetimeMinutes` | 15 | Access-token lifetime |
| `RefreshTokenLifetimeDays` | 30 | Refresh-token and cookie lifetime |
| `PasswordResetTokenExpirySeconds` | 900 | Password-reset token TTL |
| `EmailVerificationTokenExpirySeconds` | 86400 | Email-verification token TTL |
| `MaxFailedLoginAttempts` / `LockoutDurationMinutes` | 5 / 15 | Account lockout policy |
| `LoginRateLimitPerMinute` | 10 | Login rate limit |
| `RegisterRateLimitPerHour` | 5 | Registration rate limit, per IP |
| `ForgotPasswordRateLimitPerHour` | 5 | Forgot-password rate limit |
| `RevokeAllOnReuseDetected` | `true` | Revoke all of a player's tokens when refresh-token reuse is detected |
| `MinPasswordLength` | 8 | Minimum password length |

Only the first five keys appear in the committed `appsettings.json`; the rest are `AuthConfig.cs` defaults and can be overridden the same way.

#### `Payment` section

| Key | Default | Purpose |
|---|---|---|
| `Mode` | `Fake` | One of `Fake`, `Sandbox`, `Live`. Selects `FakePaymentProvider` versus `BtcPayGreenfieldProvider`. Absent from `appsettings.json`; set to `Fake` in `appsettings.Development.json` |
| `BtcPayBaseUrl` / `BtcPayApiKey` / `BtcPayStoreId` | empty | BTCPay Greenfield connection. **Required** (non-empty) in `Sandbox` and `Live`, or startup throws |
| `WebhookSecret` | empty | BTCPay webhook HMAC secret. **Required** in `Sandbox` and `Live` |
| `WebhookSignatureHeader` | `BTCPay-Sig` | Header carrying the webhook signature |
| `WebhookMaxAgeSeconds` | 300 | Maximum accepted webhook age |
| `CommissionRate` | 0.10 | Payout commission taken from the pool |
| `MinDepositUsd` / `MinWithdrawalUsd` | 1.00 / 1.00 | Minimum top-up and withdrawal amounts |
| `MaxVipEntryFeeUsd` | 500.00 | Upper bound on a VIP room entry fee |
| `PriceCacheFreshSeconds` | 30 | Exchange-rate cache freshness window |
| `PriceCacheStaleMaxSeconds` | 300 | Maximum tolerated staleness before the oracle counts as unavailable |
| `PriceQuoteValiditySeconds` | 900 | How long a locked LTC/USD invoice quote stays valid |
| `PriceOracleTimeoutSeconds` | 5 | Per-provider HTTP timeout |
| `PaymentToleranceRate` | 0.01 | Underpayment tolerance when matching an on-chain payment |
| `RefundOverpaymentThresholdUsd` | 1.00 | Overpayment above this triggers an automatic refund |
| `RequiredConfirmations` | 1 | On-chain confirmations required. Tuned for regtest — **raise before mainnet** |
| `ConnectionString` | same default as `Auth` | PostgreSQL connection for `PaymentDbContext` **and** `GameEventDbContext` |
| `NetworkFeeResponsibility` | `DeductedFromPool` | Documentation and audit label only; never read as behavior |
| `WebhookEventRetentionDays` | 90 | Declared but **not enforced anywhere in code**. No pruning job exists for `ProcessedWebhookEvents` |

#### `Admin` section

| Key | Default | Purpose |
|---|---|---|
| `MaxLogEntries` | 500 | Size of the in-memory log ring buffer behind `GET /api/admin/logs`. Read straight from `builder.Configuration` before the DI container is built |
| `AccessKey` | `dev-admin-key` | **Dead field.** Superseded by the `Player.Role == Admin` check in `AdminAuthFilter`; nothing reads it. Left in place deliberately rather than silently removed |

### Frontend (`web/.env.local`)

| Variable | Required | Default when unset | Purpose |
|---|---|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | No | `http://localhost:5019` | Backend REST base URL (`lib/identity.ts`, `lib/game/api.ts`, `lib/auth/api.ts`, `lib/payments/*`) |
| `NEXT_PUBLIC_API_HUB_URL` | No | `http://localhost:5019/hub/game` | Game hub URL (`lib/game/signalr-client.ts`). The wallet hub URL is always derived from `NEXT_PUBLIC_API_BASE_URL` plus `/hub/wallet` and is not separately configurable |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | No | empty string; the button renders but Google sign-in cannot complete | Google OAuth client ID (`components/auth/GoogleContinueButton.tsx`) |
| `NEXT_PUBLIC_SITE_URL` | **Yes, for production builds** | `http://localhost:3000` in development | Base URL for canonical, Open Graph, `robots.txt`, and `sitemap.xml` URLs (`lib/metadata.ts`) |

> [!WARNING]
> `NEXT_PUBLIC_SITE_URL` is not optional at build time. `web/lib/metadata.ts` throws when `NODE_ENV` is `production` and the variable is unset, which fails `next build` while collecting page data for `/robots.txt`. Verified by running the build both without and with the variable.

## Available Scripts

`web/package.json` defines exactly three scripts. None is destructive.

| Script | Command | What it does |
|---|---|---|
| `npm run dev` | `next dev` | Starts the Next.js dev server on `http://localhost:3000` |
| `npm run build` | `next build` | Production build. Requires `NEXT_PUBLIC_SITE_URL` |
| `npm run start` | `next start` | Serves an existing production build; run `build` first |

The backend has no script manifest; it is driven with the `dotnet` CLI directly.

### Scripts outside `package.json`

| Script | What it does |
|---|---|
| `web/_gen-icons.js` | One-off Node script that regenerates `app/icon.png`, `app/apple-icon.png`, and `app/favicon.ico` from `public/logo/`. It requires `sharp`, which is **not** a declared dependency in `package.json` (only present transitively in the lockfile), so it may need `npm i -D sharp` before it will run |
| `sandbox/btcpay/up.ps1` | Windows PowerShell. Brings up the whole BTCPay regtest sandbox (Postgres, bitcoind, litecoind, NBXplorer, BTCPay 2.4.2, and a containerized API) and writes the `Payment:*` values into `dotnet user-secrets`. Accepts `-SkipApiContainer` |
| `sandbox/btcpay/down.ps1` | Tears the sandbox down |

> [!CAUTION]
> `sandbox/btcpay/down.ps1` is **destructive**. It runs `docker compose down -v`, deleting every sandbox volume (BTCPay store, LTC wallet, API key, webhook, and the sandbox `wintowar` database), removes the sandbox `dotnet user-secrets` entries from `api/`, and deletes `sandbox/btcpay/.env`. Nothing in the sandbox survives it.

## Development

### Backend

```bash
cd api
dotnet run
```

Listens on `http://localhost:5019`; the `https` profile adds `https://localhost:7230`. Both are defined in `api/Properties/launchSettings.json`. On startup `Program.cs`:

1. applies pending EF Core migrations for all three `DbContext`s via `Database.Migrate()`;
2. seeds an Admin account if `Auth:SeedAdminEmail` and `Auth:SeedAdminPassword` are set and no such player exists;
3. validates that `GameConfig.VipRoomMaxPlayers`, `StandardRoomPlayerCount`, and `PracticeRoomDefaultPlayerCount` do not exceed the map region count, throwing rather than silently clamping;
4. maps the OpenAPI document **in Development only**.

### Frontend

```bash
cd web
npm run dev
```

Runs on `http://localhost:3000`, the only origin the backend CORS policy allows.

### Tests

```bash
dotnet test api.Tests/api.Tests.csproj
```

189 tests. Verified locally: **188 passed, 1 failed** — see [Troubleshooting](#troubleshooting) for why that single failure is environmental rather than a code defect.

### BTCPay regtest sandbox

```powershell
.\sandbox\btcpay\up.ps1     # bring everything up and wire user-secrets
.\sandbox\btcpay\down.ps1   # destructive teardown, see the warning above
```

Service list, image versions, the minimum Greenfield API-key permission set, how webhooks reach the API without a tunnel, and how to make regtest payments and mine blocks are documented in [`sandbox/btcpay/README.md`](sandbox/btcpay/README.md).

## Build

### Backend

```bash
dotnet build api/api.csproj
dotnet publish api/api.csproj -c Release -o <output>
```

Verified: `dotnet build` succeeds. It emits two `NU1903` warnings for a known high-severity advisory in the transitive `Microsoft.OpenApi` 2.0.0 package; the test project adds a third for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11. These are warnings only and do not fail the build.

### Frontend

```bash
cd web
NEXT_PUBLIC_SITE_URL=https://your-domain.example npm run build
npm run start
```

Verified: the build fails without `NEXT_PUBLIC_SITE_URL` and succeeds with it.

### Moving from Sandbox to Live

Configuration only: `Payment:Mode`, `Payment:BtcPayBaseUrl`, `Payment:BtcPayApiKey`, `Payment:BtcPayStoreId`, `Payment:WebhookSecret`. There is no code branch between `Sandbox` and `Live`. Per `docs/21-payment-sandbox-e2e.md`, the Greenfield provider has been exercised end to end against BTCPay Server 2.4.2 on a Litecoin regtest network — invoice creation, on-chain payment, webhook signature verification, idempotency and out-of-order handling, invoice expiry, and on-chain withdrawal. Raise `Payment:RequiredConfirmations` from its regtest-appropriate `1` before handling mainnet funds.

## Deployment

**Not verifiable from the repository.** There is no production Dockerfile, CI/CD workflow (`.github/workflows/`), or platform configuration (`vercel.json`, `railway.json`, `Procfile`, and the like). The only Docker assets — `sandbox/btcpay/docker-compose.yml` and `sandbox/btcpay/api.Dockerfile` — are explicitly local regtest development tooling, and the Dockerfile says so in its own header.

Deploying therefore means running `dotnet publish` output and `next build` / `next start` output on infrastructure of your choosing, supplying the configuration above. Two things in the committed code assume localhost and would have to change first:

- the CORS policy in `Program.cs` is hardcoded to `http://localhost:3000`;
- `appsettings.json` ships development-only defaults, including a placeholder JWT signing key and a seeded admin password.

## API

All REST routes are prefixed with `api/`. "Auth" below means a valid JWT Bearer token; "Admin" means the `AdminAuthFilter`, which requires `Player.Role == Admin`.

### Authentication — `AuthController`

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `api/auth/register` | — | Email/password registration |
| POST | `api/auth/login` | — | Email/password login |
| POST | `api/auth/google` | — | Sign in or sign up with a Google ID token |
| POST | `api/auth/google/link` | Auth | Link a Google account to the current player |
| POST | `api/auth/refresh` | Refresh cookie | Rotate the refresh token, issue a new access token |
| POST | `api/auth/logout` | Auth | Revoke the refresh token and clear the cookie |
| POST | `api/auth/forgot-password` | — | Start password reset |
| POST | `api/auth/reset-password` | — | Complete password reset with a token |
| POST | `api/auth/verify-email` | — | Confirm an email-verification token |
| POST | `api/auth/change-password` | Auth | Change password |
| GET | `api/auth/me` | Auth | Current player profile |

### Game — `MatchesController`, `RoomsController`

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `api/matches/map` | — | Static map definition |
| GET | `api/matches/config` | — | Public game-config values, including the commission rate |
| GET | `api/matches/{matchId}` | — | Current match state |
| GET | `api/matches/{matchId}/payout` | — | Payout summary for a finished match |
| GET | `api/rooms?type=` | Auth | List open rooms of a type |
| POST | `api/rooms/standard/join` | Auth | Join the Standard queue |
| POST | `api/rooms/practice/join` | Auth | Join a free Practice room |
| POST | `api/rooms/vip` | Auth | Create a VIP room |
| GET | `api/rooms/invite/{inviteToken}` | Auth | Resolve a VIP invite token |
| POST | `api/rooms/{matchId}/verify-password` | Auth | Verify a VIP room password |
| POST | `api/rooms/{matchId}/join` | Auth | Join a specific room |

Join endpoints return a `JoinRoomResult` whose `Outcome` is `Joined`, `RoomFull`, or `InsufficientBalance`. In the last case the response carries the shortfall and a ready-made top-up invoice — the client is never asked for an LTC address in order to join.

### Wallet and payments

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `api/wallet` | Auth | Balance |
| GET | `api/wallet/invoices` | Auth | Invoice history |
| GET | `api/wallet/withdrawals` | Auth | Pending withdrawal requests |
| GET | `api/wallet/withdrawal-addresses` | Auth | Recently used LTC destination addresses |
| POST | `api/wallet/topup` | Auth | Create a deposit invoice |
| POST | `api/wallet/withdraw` | Auth | Request a withdrawal |
| GET | `api/payments/{invoiceId}` | Auth | Look up an invoice |
| POST | `api/matches/{matchId}/payments` | Auth | Create a match entry payment |
| GET | `api/matches/{matchId}/payments/{invoiceId}` | Auth | Inspect a match entry payment |
| POST | `api/webhooks/btcpay` | Signature | BTCPay webhook receiver, HMAC-verified and idempotent |
| POST | `api/dev/payments/{invoiceId}/simulate-paid` | — | **Development only.** Returns `404` unless the host environment is Development *and* the registered provider is `FakePaymentProvider` |

### Support and admin

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `api/support-tickets` | — | Submit a support ticket |
| GET | `api/admin/metrics` | Admin | Pending withdrawals, active matches, today's confirmed volume |
| GET | `api/admin/matches` | Admin | List matches currently held in memory |
| GET | `api/admin/matches/{matchId}/events` | Admin | Persisted match audit-log events |
| GET | `api/admin/payments/withdrawals` | Admin | Withdrawal queue |
| POST | `api/admin/payments/withdrawals/{id}/approve` | Admin | Approve a withdrawal |
| POST | `api/admin/payments/withdrawals/{id}/reject` | Admin | Reject a withdrawal |
| GET | `api/admin/payments/invoices/failed` | Admin | Failed invoices |
| POST | `api/admin/payments/invoices/{invoiceId}/refund` | Admin | Refund a failed invoice |
| GET | `api/admin/users/{playerId}` | Admin | Look up a player |
| GET | `api/admin/support-tickets` | Admin | List support tickets |
| POST | `api/admin/support-tickets/{id}/status` | Admin | Change ticket status |
| GET | `api/admin/logs` | Admin | Tail the in-memory log ring buffer |

### Health

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `api/health` | — | Returns `api` and `database` flags; `database` reflects `PaymentDbContext.Database.CanConnectAsync()` |

### SignalR hubs

Both hubs are `[Authorize]`. A SignalR handshake cannot carry an `Authorization` header, so the JWT is passed as an `access_token` query parameter; `Program.cs` picks it up for any path under `/hub`.

**`/hub/game`** — client-callable methods:

| Method | Purpose |
|---|---|
| `JoinMatch(matchId)` | Attach/reconnect the caller to the match, add the connection to the match SignalR group, and trigger an immediate `MatchState` broadcast |
| `LeaveLobby()` | Leave a lobby that has not started |
| `StartVipMatchNow()` | VIP creator starts the match early |
| `AttackRegion(fromRegionId, toRegionId)` | Send troops |

Server-to-client events on `/hub/game`: `MatchState`, `ActionError`, `ArmyDeparted`, `ArmyClashed`, `ArmyArrived`, `LobbyTimeoutReached`, `PaymentConfirmed`, `PayoutCompleted`.

**`/hub/wallet`** — no client-callable methods. On connect the hub adds the caller to a `wallet:{userId}` group derived from the JWT subject claim, never from client input, and emits `WalletBalanceUpdated` with the full absolute balance whenever it changes. Because the payload is absolute rather than a delta, duplicate deliveries are harmless.

## Database

Three independent EF Core `DbContext`s target the **same** PostgreSQL database, each with its own migration history under `api/Migrations/`.

| DbContext | Migrations folder | Tables |
|---|---|---|
| `AuthDbContext` | `Migrations/Auth/` | `PlayerAccounts`, `RefreshTokens`, `PasswordResetTokens`, `EmailVerificationTokens`, `AccountDeletionRequests` |
| `PaymentDbContext` | `Migrations/Payments/` | `Wallets`, `PaymentInvoices`, `Payouts`, `PayoutRecipients`, `Refunds`, `WithdrawalRequests`, `ProcessedWebhookEvents` |
| `GameEventDbContext` | `Migrations/GameEvents/` | `MatchEventLogs` |

Notes:

- Migrations are applied automatically at startup via `Database.Migrate()` for all three contexts. `EnsureCreated()` is deliberately not used.
- `Microsoft.EntityFrameworkCore.Design` is referenced so the `dotnet-ef` CLI can author new migrations; run it against `api/api.csproj` with an explicit `--context`.
- Match, room, player, region, and army state is **not** persisted; it lives only in `MatchManager` and `RoomService` memory. `GET /api/admin/matches` therefore lists only matches created since the last process start, while `api/admin/matches/{id}/events` reads the durable audit log.
- `AccountDeletionRequests` has a table and an entity but **no endpoint writes to it**. The `/hesap-ayarlari` delete action only checks that the wallet balance is zero; it performs no server-side deletion.
- A `ReconciliationLocks` table appears in the early migrations and was dropped again in `20260808065254_PayoutRefundWalletCredit`. There is no reconciliation service in the current code.

## Authentication

- **Credentials.** Email/password through `AuthController`, plus Google Sign-In (`api/auth/google`) and linking (`api/auth/google/link`) verified with `Google.Apis.Auth` against `Auth:GoogleClientId`. Passwords are hashed with the ASP.NET Core `PasswordHasher` for `PlayerAccount`.
- **Tokens.** Short-lived JWT access tokens, 15 minutes by default, validated in `Program.cs` with issuer, audience, signing-key, and lifetime checks and a 30-second clock skew. Refresh tokens rotate on every use and are delivered in an `HttpOnly`, `SameSite=Strict` cookie named `wintowar_refresh` scoped to path `/api/auth`. `Secure` is enabled outside Development, because a fixed `Secure=true` broke plain-HTTP local development.
- **Reuse detection.** When an already-used refresh token is presented, all of that player's active tokens are revoked (`Auth:RevokeAllOnReuseDetected`).
- **Client-side session.** The access token is held in module memory only, never `localStorage`. `web/lib/identity.ts` exposes `ensureSessionLoaded()`, which silently refreshes via the cookie on page load; `AuthGuard` waits for it before deciding whether to redirect to `/giris`.
- **SignalR.** Both hubs read the JWT from an `access_token` query parameter for paths under `/hub`.
- **Roles.** `Player.Role` is `Player` or `Admin`. Admin controllers carry `[AdminAuth]`, which requires an authenticated principal in the `Admin` role. The frontend `AdminGate` mirrors this by logging in through `/api/auth/login` and rejecting non-Admin roles; it is a UX gate, not the security boundary.
- **Admin bootstrap.** The only way to create the first Admin is `Auth:SeedAdminEmail` plus `Auth:SeedAdminPassword` at startup. There is no self-service path to the Admin role.
- **Email delivery.** `IEmailSender` is bound to `LoggingEmailSender`. Verification and reset emails are written to the log, not sent. No SMTP, SendGrid, or other provider integration exists in the codebase.

## Configuration

| File | Role |
|---|---|
| `api/appsettings.json` | Base configuration, checked in. Development-only values, including a placeholder JWT signing key and a seeded admin password |
| `api/appsettings.Development.json` | Overrides `Logging` and sets `Payment:Mode` to `Fake` |
| `api/GameConfig.cs` | Every gameplay tunable as a compile-time constant: room sizes, entry fee, production and regen intervals, tick rate, dispatch batching, bot weights and decision intervals, rate limits, retention. Not runtime-configurable by design |
| `api/PaymentConfig.cs`, `api/AuthConfig.cs`, `api/AdminConfig.cs` | `IOptions`-bound settings for values that legitimately vary per environment |
| `api/Properties/launchSettings.json` | `http` and `https` local run profiles |
| `web/next.config.ts` | Enables the React Compiler (`reactCompiler: true`). Nothing else is customized |
| `web/tsconfig.json` | `strict: true`, with the `@/*` path alias pointing at the `web/` root |
| `web/components.json` | shadcn generator config: style `base-rhea`, base color `mist`, Lucide icons, RSC on, aliases into `components/`, `lib/`, `hooks/` |
| `web/postcss.config.mjs` | Tailwind v4 through `@tailwindcss/postcss` |
| `sandbox/btcpay/docker-compose.yml` | The regtest sandbox stack. Also demonstrates the double-underscore environment-variable form the API accepts (`Payment__Mode`, `Auth__ConnectionString`) |

> [!NOTE]
> `api/api.http` is leftover `dotnet new webapi` scaffolding. It still requests `/weatherforecast/`, an endpoint that does not exist in this project.

## Troubleshooting

**Backend throws `InvalidOperationException: Oda kapasiteleri ... haritadaki bölge sayısını ... aşıyor` at startup.**
A room-capacity constant in `GameConfig.cs` (`VipRoomMaxPlayers`, `StandardRoomPlayerCount`, `PracticeRoomDefaultPlayerCount`) exceeds the region count in `api/Data/map.json`, which is 12. This is a deliberate fail-fast check, not a bug; fix the map or the constant.

**Backend refuses to start with `Payment:Mode=... için zorunlu BTCPay ayarları eksik`.**
`Payment:Mode` is `Sandbox` or `Live` and at least one of `BtcPayBaseUrl`, `BtcPayApiKey`, `BtcPayStoreId`, `WebhookSecret` is empty. The message names the missing keys. Supply them, or set `Payment:Mode=Fake`. There is no silent fallback: shipping a real-money system with a fake provider is treated as the worst possible outcome.

**`dotnet test` fails only on `AuthEndpointsSecurityTests.WalletBalance_ReturnsOnlyTheAuthenticatedPlayersOwnWallet`, with `SocketException (10061)` against `localhost:49392`.**
This is environmental, not a code defect. `sandbox/btcpay/up.ps1` writes `Payment:Mode=Sandbox` plus BTCPay endpoints into `dotnet user-secrets` for `api/`, and the ASP.NET Core test host loads user-secrets, so the test boots the real Greenfield provider and tries to reach a BTCPay instance that is no longer running. Check with `dotnet user-secrets list --project api`, and clear it by running `sandbox/btcpay/down.ps1` or `dotnet user-secrets remove "Payment:Mode" --project api`. The other 188 tests pass.

**`next build` fails with `Failed to collect page data for /robots.txt`.**
`NEXT_PUBLIC_SITE_URL` is unset. It is mandatory for production builds; see [Environment Variables](#environment-variables).

**CORS errors in the browser console.**
`Program.cs` allows only `http://localhost:3000`. Any other frontend origin or port is rejected, and the policy is hardcoded, so it must be edited in source.

**SignalR connects and then fails with `401 Unauthorized` on `/hub/game` or `/hub/wallet`.**
The JWT must be supplied as `?access_token=...` on the connection URL, not as an `Authorization` header. See `web/lib/game/signalr-client.ts` and `web/lib/payments/wallet-signalr-client.ts` for the `accessTokenFactory` pattern.

**Sessions reset on every page refresh.**
The refresh cookie is `Secure` outside Development and scoped to path `/api/auth`. Running the API over plain HTTP in a non-Development environment makes the browser silently drop it.

**Exchange-rate lookups return 403 from CoinGecko.**
CoinGecko blocks requests without a `User-Agent`. `Program.cs` already sets `WinToWar/1.0` on the named `CoinGecko` and `CoinCap` clients; keep that header if you add another pricing client.

**Database schema looks out of date.**
Migrations run on every backend start via `Database.Migrate()` for all three contexts. If nothing changed, the configured connection string is probably pointing at a different database than you expect: `Auth:ConnectionString` and `Payment:ConnectionString` are separate keys and must agree.

**`NU1903` warnings during build.**
Known high-severity advisories in transitive `Microsoft.OpenApi` 2.0.0 (both projects) and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (test project). Warnings only; the build succeeds.

## Contributing

There is no `CONTRIBUTING.md` and no issue or PR template in the repository. Conventions the code actually follows:

- **Backend:** `Controller → Service → Model`. New modules become subfolders inside the existing `Models/`, `Services/`, `Controllers/` directories, never new top-level folders.
- **Frontend:** the same rule, under a per-module subfolder of `components/` and `lib/`.
- **Gameplay numbers** go in `GameConfig.cs` as constants; **environment-varying values** go in an `IOptions`-bound config class. Magic numbers in service code are avoided.
- **User-facing strings are Turkish**; identifiers are English.
- **Schema changes are migrations**, never `EnsureCreated()`.
- `docs/` carries the authoritative business rules, marked 🔒 (fixed customer decision), 🛠️ (engineering assumption with rationale), and ⚙️ (process rule). `CLAUDE.md`, which is untracked, describes the reading order and precedence among those documents.

## License

No license file is present: no `LICENSE`, `LICENSE.md`, or equivalent at the repository root, and no `license` field in `web/package.json`. All rights reserved unless the project owner states otherwise.
