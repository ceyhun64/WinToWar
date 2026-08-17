# WinToWar

Real-time, region-conquest multiplayer strategy game in the style of *state.io*, with real-money entry fees and cryptocurrency payouts.

---

## Overview

WinToWar lets players compete on a shared, city-based map: each player starts on a randomly assigned region, every owned region produces troops passively over time, and players attack neighboring or distant regions to eliminate opponents and win the prize pool. Rooms range from free practice matches to paid Standard queues and fully customizable VIP tables, with deposits/withdrawals settled in Litecoin (LTC) behind a USD-denominated wallet.

The backend (`api/`) is a server-authoritative ASP.NET Core Web API: every game action (attacks, movement, production) is validated and computed server-side over a SignalR hub, and the client never dictates outcomes. The frontend (`web/`) is a Next.js application that renders the live match, lobby, wallet, and an admin panel.

> The project's `docs/` folder (module-by-module product/engineering specification, referenced throughout this README as `docs/0X-*.md`) and `CLAUDE.md` exist in this working copy but are **excluded from version control** (see `.gitignore`, `docs/` and `CLAUDE.md` entries) — `git ls-files` confirms neither is tracked. A fresh clone of this repository will **not** include them; they are local planning/AI-assistant material, not shipped documentation.

## Features

Verified against the current implementation in `api/` and `web/`:

- **Real-time region conquest** on a 12-region map (`api/Data/map.json`, each region with exactly 3 neighbors), with random starting-region assignment per match, drag-to-send army movement, and passive per-region troop production.
- **Three room types**, defined in `GameConfig.cs` and served through `RoomsController`:
  - **Standard** — fixed 4-player matchmaking queue, fixed $1.00 entry fee, fixed neutral-region defense (10).
  - **VIP** — creator-configurable player count (2–12), entry fee, fog-of-war toggle, neutral defense strength (1–7), and optional password-protected/invite-token access.
  - **Practice** — free mode with no payment flow, for learning the mechanics.
- **Bot matchmaking** (`BotMatchService`): if no human opponent joins within a randomized 10–15 second window after the first real player, a clearly-labeled bot fills the lobby so matches start reliably; bot difficulty is weighted (60% Normal / 25% Easy / 15% Hard) and match outcomes are computed by the same combat/economy engine, never predetermined.
- **Real-time engine over SignalR** (`api/Hubs/GameHub.cs`, mapped at `/hub/game`): `JoinMatch`, `LeaveLobby`, `StartVipMatchNow`, and `AttackRegion(fromRegionId, toRegionId)`, all pushed to clients as live `MatchState` (and `ArmyDeparted`/`ArmyClashed`) updates.
- **Wallet & payments module** (independent layer): USD-denominated wallet, BTCPay Server (Greenfield API) integration for LTC deposits/withdrawals, a live exchange-rate oracle (CoinGecko/CoinCap with fallback), commission-based payouts (10%), refunds, and webhook-driven invoice reconciliation.
- **Authentication module** (independent layer): email/password registration and login, Google Sign-In, JWT access/refresh tokens, password reset, email verification, and role-based (Player/Admin) authorization.
- **Admin panel** (`/admin/*`): match inspection and audit-log queries, payment/withdrawal/refund management, support ticket handling, user lookup, and live log viewing — backed by dedicated `Admin*Controller` endpoints and a `Player.Role == Admin` authorization filter (`AdminAuthFilter`).
- **Match audit log**: every real-money match's events (attacks, captures, eliminations, start/end) are recorded to a dedicated `GameEventDbContext` for payment-dispute support.

## Screenshots

Not available. The repository contains no dedicated screenshots/documentation-image directory; `web/public/` only holds functional UI assets (lobby card art, landing background videos, logos, favicons) used by the running application itself, not documentation screenshots. None are referenced here to avoid implying otherwise.

## Technology Stack

**Backend** (`api/`) — from `api/api.csproj` and `api.Tests/api.Tests.csproj`:

| Package | Version | Purpose |
|---|---|---|
| .NET / ASP.NET Core Web API | `net10.0` | Runtime and web framework |
| `Microsoft.AspNetCore.OpenApi` | 10.0.3 | OpenAPI document generation (Development only) |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | PostgreSQL provider for EF Core |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.3 | `dotnet-ef` migration tooling (build-time only) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.3 | JWT Bearer authentication |
| `Google.Apis.Auth` | 1.69.0 | Google ID token verification |
| SignalR (`Microsoft.AspNetCore.SignalR`, built into ASP.NET Core) | — | Real-time game hub |
| `xunit` / `xunit.runner.visualstudio` | 2.9.3 / 3.1.4 | Test framework (`api.Tests/`) |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.3 | End-to-end HTTP pipeline tests |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.0 | In-memory SQLite provider used only by tests |

**Frontend** (`web/`) — from `web/package.json`:

| Package | Version | Purpose |
|---|---|---|
| `next` | 16.2.12 | App Router framework |
| `react` / `react-dom` | 19.2.4 | UI runtime |
| `babel-plugin-react-compiler` | 1.0.0 | React Compiler (enabled via `reactCompiler: true` in `next.config.ts`) |
| `typescript` | ^5 | Static typing |
| `tailwindcss` / `@tailwindcss/postcss` | ^4 | Styling |
| `@microsoft/signalr` | ^10.0.0 | SignalR client for the game hub |
| `shadcn` / `@shadcn/react` (`components.json`) | ^4.16.1 / ^0.2.1 | Component system generator (`components/ui/`) |
| `recharts` | ^3.8.0 | Charts (admin metrics) |
| `framer-motion` | ^13.0.0 | Animation |
| `embla-carousel-react` | ^8.6.0 | Carousels |
| `qrcode.react` | ^4.2.0 | QR codes (wallet deposit address) |
| `lucide-react` | ^1.28.0 | Icon set |
| `date-fns`, `class-variance-authority`, `clsx`, `tailwind-merge`, `cmdk`, `input-otp`, `react-day-picker`, `react-resizable-panels`, `tw-animate-css` | various | Supporting UI utilities |

**Infrastructure**

- PostgreSQL — single database, one shared connection string across the three `DbContext`s below.
- [BTCPay Server](https://btcpayserver.org/) (Greenfield REST API) for LTC invoices and payouts. Which provider is used depends on `Payment:Mode` (see `Program.cs`): `Fake` (default) registers `FakePaymentProvider` and never touches the network; `Sandbox` and `Live` both register the real `BtcPayGreenfieldProvider` and require a reachable BTCPay instance. A self-hosted regtest sandbox is versioned in [`sandbox/btcpay/`](sandbox/btcpay/README.md) and starts with a single command.
- No containerization, CI, or deployment configuration (Dockerfile, `docker-compose`, GitHub Actions, `vercel.json`, etc.) is present in the repository — see [Deployment](#deployment).

Client-side state is a lightweight custom React hook (`web/lib/game/store.ts`'s `useGameStore`), explicitly *not* Redux or Zustand (per the file's own header comment) — there is no external state-management library in `package.json`.

## Architecture

- **Layered backend**: `Controller → Service → Model`, with `DbContext`s owned per module. Game state itself (`Player`, `Match`, `Region`, `Army`, `Room`) is kept **in-memory** by singleton services `MatchManager` and `RoomService` (registered via `AddSingleton` in `Program.cs`) — it is not persisted to a database. This is a documented single-instance deployment assumption; horizontal scaling would require moving this state to a shared store (e.g. Redis), which is not implemented.
- **Three independent EF Core `DbContext`s** against the same PostgreSQL database: `AuthDbContext`, `PaymentDbContext`, `GameEventDbContext` (see [Database](#database)).
- **Server-authoritative game engine**: `CombatService` and `MovementService` compute all outcomes; the SignalR hub only relays validated actions.
- **Time-based dispatch**: sending troops does not remove them from the source region instantly — a server-side `Dispatch` (`api/Models/Dispatch.cs`) releases them in batches over real elapsed time, processed by `MovementService`/`EconomyTickService`, so one region can dispatch to multiple targets without double-spending troops.
- **Three largely independent modules** sharing one process and one Postgres instance: the game engine, the payments module (wallet, BTCPay, payouts, refunds), and the authentication module (JWT, Google Sign-In, roles) — each configured through its own `IOptions<T>` class (`GameConfig` is compile-time constants; `PaymentConfig`/`AuthConfig`/`AdminConfig` are bound from `appsettings.json`).
- **CORS**: a single named policy (`WebClientCorsPolicy`) explicitly allows `http://localhost:3000` with credentials — there is no wildcard/production origin configured in the committed `appsettings.json`.

## Folder Structure

```
.
├── api/                          .NET backend
│   ├── Program.cs                DI, middleware, DB migration, startup validation
│   ├── GameConfig.cs             Game-engine tunables (compile-time constants)
│   ├── AuthConfig.cs / PaymentConfig.cs / AdminConfig.cs   IOptions-bound settings
│   ├── Controllers/              REST endpoints (Auth, Matches, Rooms, Wallet, Admin*, ...)
│   ├── Hubs/GameHub.cs           SignalR hub (JoinMatch, AttackRegion, ...)
│   ├── Models/                   Domain entities (+ Auth/, Payments/, Rooms/, Dtos/ subfolders)
│   ├── Services/                 Business logic, grouped by module
│   │   ├── Auth/                 AuthService, JwtTokenService, GoogleIdTokenValidator, ...
│   │   ├── GameEngine/           CombatService, MovementService
│   │   ├── Matchmaking/          BotMatchService
│   │   ├── Payments/             PaymentService, WalletService, PayoutService, RefundService, ...
│   │   └── Rooms/                RoomService
│   ├── Migrations/               EF Core migrations, one folder per DbContext
│   │   ├── Auth/
│   │   ├── GameEvents/
│   │   └── Payments/
│   └── Data/map.json             Static map definition (12 regions + adjacency graph)
├── api.Tests/                    xUnit test suite for the backend
├── web/                          Next.js frontend
│   ├── app/
│   │   ├── (site)/               Public/player-facing routes (lobby, wallet, auth, match history, ...)
│   │   ├── admin/                Admin dashboard routes
│   │   └── game/[matchId]/       Live match screen
│   ├── components/               React components, grouped by module (game, auth, admin, payments, lobby, rules, ui, layout, landing)
│   ├── lib/                      API clients, SignalR client, types, state hooks — grouped by module
│   ├── hooks/                    Shared React hooks
│   └── public/                   Static assets
└── README.md
```

`docs/` and `CLAUDE.md` also exist in this working copy (module-level product/engineering notes) but are gitignored — see the note under [Overview](#overview).

## Installation

**Prerequisites**

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (project targets `net10.0`)
- [Node.js](https://nodejs.org/) compatible with Next.js 16 / React 19
- [PostgreSQL](https://www.postgresql.org/) instance
- (Optional, for real payments) a reachable [BTCPay Server](https://btcpayserver.org/) instance — not required with the default `Payment:Mode=Fake`. For a real-protocol, no-real-money setup, run the self-hosted regtest sandbox in [`sandbox/btcpay/`](sandbox/btcpay/README.md) (needs Docker); it also brings up its own PostgreSQL.

**Clone and install dependencies**

```bash
git clone <repository-url>
cd WinToWar

# Backend
cd api
dotnet restore

# Frontend
cd ../web
npm install
```

## Environment Variables

There is no `.env.example` in the repository. The values below are the configuration keys actually read by the code (`appsettings.json`, `Program.cs`, `web/lib/*`), not production secrets — **do not commit real secrets**.

### Backend (`api/appsettings.json` / `api/appsettings.Development.json`, or environment variables / user-secrets per standard ASP.NET Core configuration precedence)

| Section | Key | Purpose |
|---|---|---|
| `Admin` | `AccessKey` | Dead field — no longer read. Superseded by `AdminAuthFilter`'s `Player.Role == Admin` check (see `api/Services/AdminAuthFilter.cs`), intentionally left in place rather than silently removed. |
| `Admin` | `MaxLogEntries` | In-memory log ring-buffer size for `/api/admin/logs` (default 500, read directly from configuration before DI container build). |
| `Auth` | `JwtSigningKey` | Symmetric key used to sign/validate JWT access tokens. |
| `Auth` | `JwtIssuer` / `JwtAudience` | JWT issuer/audience claims. |
| `Auth` | `GoogleClientId` | OAuth client ID used to validate Google ID tokens. |
| `Auth` | `SeedAdminEmail` / `SeedAdminPassword` | If both are set, a single Admin account is seeded on startup (only if no matching player exists yet). |
| `Auth` | `ConnectionString` | PostgreSQL connection string for `AuthDbContext`. |
| `Auth` | *(code defaults, not in committed appsettings)* | `AccessTokenLifetimeMinutes` (15), `RefreshTokenLifetimeDays` (30), `PasswordResetTokenExpirySeconds` (900), `EmailVerificationTokenExpirySeconds` (86400), `MaxFailedLoginAttempts` (5), `LockoutDurationMinutes` (15), `LoginRateLimitPerMinute` (10), `RegisterRateLimitPerHour` (5), `ForgotPasswordRateLimitPerHour` (5), `RevokeAllOnReuseDetected` (true), `MinPasswordLength` (8) — see `api/AuthConfig.cs`. |
| `Payment` | `CommissionRate` | Payout commission rate (default 0.10). |
| `Payment` | `MinDepositUsd` / `MinWithdrawalUsd` | Minimum deposit/withdrawal amounts (default 1.00 each). |
| `Payment` | `MaxVipEntryFeeUsd` | Upper bound a VIP room creator can set as entry fee (default 500.00). |
| `Payment` | `PriceCacheFreshSeconds` / `PriceCacheStaleMaxSeconds` / `PriceQuoteValiditySeconds` / `PriceOracleTimeoutSeconds` | Exchange-rate oracle cache/staleness tuning. |
| `Payment` | `PaymentToleranceRate` / `RefundOverpaymentThresholdUsd` | Payment matching tolerance and overpayment refund threshold. |
| `Payment` | `RequiredConfirmations` | On-chain confirmations required (default 1, tuned for regtest/testnet). |
| `Payment` | `NetworkFeeResponsibility` | Documentation/audit label only (`"DeductedFromPool"`); network fee is deducted from the payout pool. |
| `Payment` | `Mode` | Payment provider mode: `Fake` (default, no network), `Sandbox` (real Greenfield against regtest BTCPay), `Live` (real Greenfield against a mainnet store). `Sandbox` and `Live` differ **only** in configuration values — there is no code branch between them. In `Sandbox`/`Live`, missing `BtcPay*` configuration makes startup fail fast (never a silent fallback to `Fake`). |
| `Payment` | `BtcPayBaseUrl` / `BtcPayApiKey` / `BtcPayStoreId` | BTCPay Server Greenfield API connection details. |
| `Payment` | `WebhookSecret` / `WebhookSignatureHeader` / `WebhookMaxAgeSeconds` | BTCPay webhook signature verification. |
| `Payment` | `WebhookEventRetentionDays` | Retention period for processed webhook events (default 90). |
| `Payment` | `ConnectionString` | PostgreSQL connection string for `PaymentDbContext` (and `GameEventDbContext`, which reuses it). |

### Frontend (`web/.env.local`, not committed — read via `process.env.NEXT_PUBLIC_*`)

| Variable | Default (when unset) | Purpose |
|---|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | `http://localhost:5019` | Base URL of the backend REST API (`web/lib/identity.ts`, `web/lib/game/api.ts`, `web/lib/auth/api.ts`). |
| `NEXT_PUBLIC_API_HUB_URL` | `http://localhost:5019/hub/game` | SignalR hub URL (`web/lib/game/signalr-client.ts`). |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | *(empty string)* | Google OAuth client ID used by the "Continue with Google" button (`web/components/auth/GoogleContinueButton.tsx`). |
| `NEXT_PUBLIC_SITE_URL` | *unset — used only for metadata/canonical URLs* | Public site URL (`web/lib/metadata.ts`). |

## Available Scripts

From `web/package.json`:

| Script | Command | Description |
|---|---|---|
| `npm run dev` | `next dev` | Starts the Next.js development server. |
| `npm run build` | `next build` | Production build. |
| `npm run start` | `next start` | Serves the production build (run `build` first). |

No script in `web/package.json` is destructive. The backend has no `package.json`-equivalent script list; it is run via the `dotnet` CLI directly (see [Development](#development) / [Build](#build)).

## Development

**Backend** (from `api/`):

```bash
dotnet run
```

Starts on `http://localhost:5019` (and `https://localhost:7230` under the `https` launch profile, both defined in `api/Properties/launchSettings.json`). On startup, `Program.cs` automatically applies pending EF Core migrations for all three `DbContext`s (`Database.Migrate()`) and validates that room-capacity config values (`GameConfig.VipRoomMaxPlayers`, `StandardRoomPlayerCount`, `PracticeRoomDefaultPlayerCount`) do not exceed the map's region count — a mismatch throws on startup rather than silently clamping.

**Frontend** (from `web/`):

```bash
npm run dev
```

Starts the Next.js dev server (default `http://localhost:3000`), which the backend's CORS policy (`WebClientCorsPolicy`) explicitly allows with credentials.

## Build

**Backend:**

```bash
dotnet build      # compile
dotnet publish     # produce a deployable output
```

With `Payment:Mode` set to `Sandbox` or `Live`, `Program.cs` registers the real `BtcPayGreenfieldProvider` instead of the fake payment provider, so a reachable BTCPay Server instance and valid `Payment:BtcPay*` configuration are required; if any of them is missing the application refuses to start rather than falling back to the fake provider. This provider **has** been verified end-to-end against a live BTCPay Server (2.4.2) on a Litecoin regtest network — real invoice creation, real on-chain payment, webhook signature verification, idempotency/out-of-order handling, invoice expiry, and real on-chain withdrawal — using the sandbox in [`sandbox/btcpay/`](sandbox/btcpay/README.md). Going from `Sandbox` to `Live` is a configuration change only; `RequiredConfirmations` should be raised from its regtest-appropriate value of `1` before handling mainnet funds.

**Frontend:**

```bash
npm run build      # production build
npm run start      # serve the production build
```

## Deployment

Not verifiable. No Dockerfile, `docker-compose` file, CI/CD workflow (e.g. `.github/workflows/`), or platform-specific config (`vercel.json`, `railway.json`, `Procfile`, etc.) is present in the repository. Deploying this project means running `dotnet publish` output and `npm run build && npm run start` output on infrastructure of your choosing, with the environment variables above supplied, and does not follow any documented or scripted process in this codebase.

## API

REST endpoints are grouped under `api/Controllers/`; all are prefixed with `api/`.

| Controller | Route prefix | Responsibility |
|---|---|---|
| `AuthController` | `api/auth` | register, login, Google sign-in/link, refresh, logout, password reset, email verification, `me` |
| `MatchesController` | `api/matches` | map data, game config, match state, payout summary |
| `RoomsController` | `api/rooms` | list rooms, join Standard/Practice, create/join VIP (incl. password verification, invite links) |
| `PaymentsController` | `api/matches/{matchId}/payments` | create/inspect match entry payments |
| `PaymentWebhooksController` | `api/webhooks/btcpay` | BTCPay webhook receiver |
| `PaymentsDevController` | `api/dev/payments` | Development-only: simulate a paid invoice |
| `WalletController` | `api/wallet` | balance, invoice/withdrawal history, top-up, withdraw |
| `InvoicesController` | `api/payments` | invoice lookup |
| `SupportController` | `api/support-tickets` | submit a support ticket |
| `AdminMatchesController` | `api/admin/matches` | list matches, inspect match audit-log events |
| `AdminPaymentsController` | `api/admin/payments` | review/approve/reject withdrawals, manage failed invoices/refunds |
| `AdminUsersController` | `api/admin/users` | look up a player |
| `AdminSupportController` | `api/admin/support-tickets` | manage support tickets |
| `AdminLogsController` | `api/admin/logs` | tail in-memory application logs |
| `AdminMetricsController` | `api/admin/metrics` | operational metrics |

**Real-time hub** (`api/Hubs/GameHub.cs`, mapped at `/hub/game`, requires a JWT via an `access_token` query parameter since SignalR handshakes cannot carry headers):

- `JoinMatch(matchId)`
- `LeaveLobby()`
- `StartVipMatchNow()`
- `AttackRegion(fromRegionId, toRegionId)`

`GET /api/health` reports API and database connectivity (checks `PaymentDbContext.Database.CanConnectAsync()`).

## Database

The backend uses three independent EF Core `DbContext`s against the same PostgreSQL database, each with its own migration history under `api/Migrations/`:

- **`AuthDbContext`** (`Migrations/Auth/`) — player accounts, refresh tokens, password reset/email verification tokens.
- **`PaymentDbContext`** (`Migrations/Payments/`) — wallets, payment invoices, payouts/payout recipients, refunds, withdrawal requests.
- **`GameEventDbContext`** (`Migrations/GameEvents/`) — the match audit log (`MatchEventLog`) used for payment-dispute investigation.

Match/room/army state itself (`Player`, `Match`, `Region`, `Army`, `Room`) is kept **in-memory** by `MatchManager`/`RoomService` (single-instance deployment assumption), not persisted to a database.

Migrations apply automatically on backend startup via `Database.Migrate()` (all three contexts, in `Program.cs`); the `Microsoft.EntityFrameworkCore.Design` package is included for use with the `dotnet-ef` CLI tool when authoring new migrations.

## Authentication

- Email/password registration and login (`AuthController`), plus Google Sign-In (`api/auth/google`) and account-linking (`api/auth/google/link`) verified via `Google.Apis.Auth`.
- JWT Bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`), configured in `Program.cs` with issuer/audience/signing-key validation and a 30-second clock skew.
- Access + rotating refresh tokens; on detected refresh-token reuse, all of that player's active tokens are revoked (`AuthConfig.RevokeAllOnReuseDetected`).
- SignalR cannot carry an `Authorization` header on its handshake, so the JWT is passed as an `access_token` query parameter for requests under `/hub` (`Program.cs`, `JwtBearerEvents.OnMessageReceived`).
- Role-based authorization: `Player.Role` (`Player`/`Admin`). Admin endpoints are protected by `AdminAuthFilter`, which checks `User.IsInRole("Admin")` — not the legacy `Admin:AccessKey` config value, which is no longer read.
- An initial Admin account can be seeded on startup from `Auth:SeedAdminEmail` / `Auth:SeedAdminPassword`, only if no matching player already exists — there is no self-service way to become Admin.
- Password reset and email verification tokens are time-limited (`PasswordResetTokenExpirySeconds` / `EmailVerificationTokenExpirySeconds`); email delivery in the current codebase goes through `LoggingEmailSender` (registered as `IEmailSender` in `Program.cs`), i.e. verified to log rather than to send real email — no real email provider (SMTP/SendGrid/etc.) integration was found in the code.

## Configuration

- **`api/appsettings.json`** — base configuration (checked in, contains only development-appropriate default values, e.g. `dev-only-jwt-signing-key...`). **`api/appsettings.Development.json`** currently overrides only `Logging`.
- **`GameConfig.cs`** — all game-engine tunables as compile-time `const` values (room sizes, entry fees, production rates, combat/movement timings, bot behavior). There is a single source of truth; no magic numbers are read from configuration for gameplay values.
- **`PaymentConfig.cs` / `AuthConfig.cs` / `AdminConfig.cs`** — bound from `appsettings.json` via `IOptions<T>` for values that legitimately differ per environment (secrets, connection strings, BTCPay endpoint).
- **`web/next.config.ts`** — enables the React Compiler (`reactCompiler: true`); no other custom Next.js configuration.
- **`web/components.json`** — shadcn/ui generator configuration (style `base-rhea`, base color `mist`, Lucide icons, aliases into `components/`, `lib/`, `hooks/`).
- CORS is hardcoded to `http://localhost:3000` in `Program.cs` (`WebClientCorsPolicy`) — there is no environment-driven allowed-origins list, so a production frontend origin would need this policy to be edited directly.

## Troubleshooting

Issues that can actually occur based on the current code:

- **Backend crashes immediately on startup with an "Oda kapasiteleri ... haritadaki bölge sayısını ... aşıyor" (`InvalidOperationException`)**: a room-capacity constant in `GameConfig.cs` (`VipRoomMaxPlayers`, `StandardRoomPlayerCount`, or `PracticeRoomDefaultPlayerCount`) exceeds the region count in `api/Data/map.json` (currently 12). This is an intentional fail-fast check in `Program.cs`, not a bug — fix the map or the constant.
- **Frontend can't reach the backend / CORS errors in the browser console**: the backend's CORS policy only allows `http://localhost:3000` by default (`Program.cs`). Running the frontend on a different port or host will be rejected.
- **SignalR connects but immediately gets `401 Unauthorized` on `/hub/game`**: the hub requires a JWT passed via `?access_token=...` on the connection URL, not an `Authorization` header — see `web/lib/game/signalr-client.ts` for the expected client pattern.
- **Payments fail or the app refuses to start with a "zorunlu BTCPay ayarları eksik" error**: `Payment:Mode` is `Sandbox`/`Live`, which requires a real, reachable BTCPay Server instance and valid `Payment:BtcPayBaseUrl` / `BtcPayApiKey` / `BtcPayStoreId` / `WebhookSecret`. Either supply them (see [`sandbox/btcpay/`](sandbox/btcpay/README.md)) or leave `Payment:Mode` at its default `Fake`, which needs no BTCPay instance.
- **Exchange-rate lookups fail with 403 from CoinGecko**: CoinGecko blocks requests without a `User-Agent` header; `Program.cs` already sets one (`WinToWar/1.0`) for the named `HttpClient`. If you introduce a new HTTP client for pricing, keep this header.
- **EF Core migrations don't apply / database schema is out of date**: migrations run automatically via `Database.Migrate()` on every backend startup for all three `DbContext`s — ensure the configured `ConnectionString` points at a reachable PostgreSQL instance before running `dotnet run`.
- **`docs/*.md` files referenced in code comments are missing**: `docs/` is intentionally gitignored (see the note under [Overview](#overview)) — this is expected on a fresh clone, not a broken checkout.

## Contributing

There is no `CONTRIBUTING.md` or GitHub issue/PR template in the repository. The backend follows a layered `Controller → Service → Model` structure, with new modules added as subfolders inside the existing top-level `Models/`, `Services/`, `Controllers/` directories rather than new top-level folders; the frontend follows the equivalent pattern under `components/`, `lib/`. If you have access to this working copy's local `docs/` folder and `CLAUDE.md`, they document the module-by-module product/engineering decisions behind these conventions in detail — but neither ships with the git repository itself (see [Overview](#overview)).

## License

No license file is present in this repository (verified: no `LICENSE`, `LICENSE.md`, or similar file at the repository root). All rights reserved unless otherwise stated by the project owner.
