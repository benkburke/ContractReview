# Frontend rules (Next.js 16 + MUI v9)

## Packages and config
- Runtime: `@mui/material @mui/icons-material @emotion/react @emotion/styled @emotion/cache @mui/material-nextjs openapi-fetch`; add `@microsoft/signalr` only if realtime.
- Dev: `openapi-typescript vitest @vitejs/plugin-react jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event @playwright/test @axe-core/playwright prettier eslint-config-prettier`.
- npm scripts must be cross-platform: no `rm -rf`, `export VAR=`, or `&&`-chained env vars. Use `rimraf`/`cross-env` if needed.
- `vitest.config.ts`: `@vitejs/plugin-react`, `environment: "jsdom"`, `include: ["src/__tests__/**/*.test.{ts,tsx}"]`, `setupFiles: ["src/__tests__/setup.ts"]` (imports `@testing-library/jest-dom/vitest`), the `@/*` alias, and `reporters: ["dot"]`.
- `playwright.config.ts`: `testDir: "e2e"`, `globalSetup: "e2e/global-setup.ts"`, `reporter: "line"`. URLs and ports come from env vars with local defaults, defined once at the top of the file: `E2E_API_URL` (default `http://localhost:5080`) and `E2E_WEB_URL` (default `http://localhost:3000`). The `webServer` array starts (1) the backend with `dotnet run --project ../backend/src/ContractReview.Api --urls <E2E_API_URL> --property:ApplyMigrationsOnBuild=false` and env `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__AppDb=<LocalDB string for ContractReview_E2E>`, waiting on `<E2E_API_URL>/health`; and (2) the frontend with `npm run start` when `VERIFY_BUILT` is set (verify just built it), else `npm run build && npm run start` when `CI` is set, else `npm run dev`. `reuseExistingServer: !process.env.CI`. `global-setup.ts` migrates `ContractReview_E2E` with `dotnet tool run dotnet-ef database update` and `EF_CONNECTION` set (skip until the first migration exists).
- Every `NEXT_PUBLIC_*` variable is in `.env.example` with a safe local default and is read only through `src/config.ts`, which falls back to those defaults so `next build` works without `.env.local`.

## App structure
- Root `src/app/layout.tsx`: `AppRouterCacheProvider` from `@mui/material-nextjs/v16-appRouter` (fall back to the newest `v1X-appRouter` entry in node_modules) → `ThemeProvider` → `CssBaseline`; font via `next/font` class on `<html>`. `src/theme.ts` starts with `'use client'`.
- Server Components by default. `'use client'` only at the top of leaf files that use state, effects, event handlers, theme callbacks, or SignalR. Never pass functions from Server to Client Components; wrap `next/link` in a `'use client'` re-export before using it as an MUI `component` prop.
- Wrap client subtrees that call `useSearchParams` in `<Suspense>` with a size-matched `Skeleton` fallback.
- Request interception uses `src/proxy.ts` (not `middleware.ts`). Caching is opt-in (`"use cache"`).
- Atomic Design: `atoms` (wrapped MUI primitives) → `molecules` → `organisms` → `templates`; pages in `src/app` compose templates. No data fetching in atoms/molecules.
- API calls go through the generated `openapi-fetch` client created once in `src/api/client.ts` (base URL from `src/config.ts`; `credentials: "include"` if auth uses cookies; bearer token from the auth library if OIDC).
- If realtime: one module-level connection in `src/realtime/connection.ts` (`withUrl(hub URL from src/config.ts, { withCredentials: true }).withAutomaticReconnect()`), ref-counted start/stop so StrictMode double-mount does not open two connections; components subscribe via `useHubEvent(name, handler)`, which calls `off` on cleanup. Event names and payloads come only from `src/realtime/contracts.ts`.

## Constants (no magic strings)
- `src/constants/routes.ts` (app paths and route builders), `src/constants/queryKeys.ts`, `src/constants/storageKeys.ts`, `src/constants/testIds.ts` (`data-testid` values shared with tests). Realtime event names stay in `src/realtime/contracts.ts`; env values come only through `src/config.ts`.
- User-facing text lives in `src/strings/<area>.ts` (one exported `as const` object per area), or in i18n message files when there are several languages. Components never contain literal UI text; ESLint's `react/jsx-no-literals` enforces this.
- Export constants `as const` so TypeScript checks every use. Allowed literals: MUI and CSS prop values (`variant="contained"`), import paths, test descriptions.

## Accessibility and languages
- WCAG 2.2 AA unless told otherwise: every input has a visible label or `aria-label`; icon-only buttons have `aria-label`; never convey meaning by color alone; full keyboard use with a visible focus ring; semantic landmarks and one `h1` per page.
- If more than one language: no hardcoded user-facing strings; use message files and the i18n library you were given.

## Tooling
- ESLint CLI with flat `eslint.config.mjs` (`eslint-config-next/core-web-vitals` + `/typescript` + Prettier with `endOfLine: "lf"`); `next lint` no longer exists. Add `"react/jsx-no-literals": ["error", { noStrings: true, ignoreProps: true }]` for `src/app/**` and `src/components/**` (the React plugin ships with `eslint-config-next`).
- Scripts: `lint` = `eslint . --quiet`, `typecheck` = `next typegen && tsc --noEmit` (generate route types before checking a clean checkout), `test` = `vitest run --passWithNoTests`, `e2e` = `playwright test --pass-with-no-tests` (both so a freshly bootstrapped app with no tests yet still verifies), `gen:api` = `openapi-typescript ../backend/openapi/openapi.json -o src/api/generated/schema.d.ts`.
