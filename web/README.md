# WinToWar — Web (`web/`)

Next.js 16 App Router frontend for WinToWar. This package is one half of a monorepo; it is not useful on its own and talks to the ASP.NET Core backend in [`../api/`](../api/).

**All setup, environment variables, scripts, architecture, and troubleshooting are documented once, in the [root README](../README.md).** This file exists only so the directory is not mistaken for a standalone `create-next-app` project.

Quick reference:

```bash
npm install
npm run dev     # http://localhost:3000, expects the API on http://localhost:5019
```

```bash
NEXT_PUBLIC_SITE_URL=https://your-domain.example npm run build
npm run start
```

`NEXT_PUBLIC_SITE_URL` is mandatory for production builds — `lib/metadata.ts` throws without it. See [Environment Variables](../README.md#environment-variables) in the root README for the full list.
