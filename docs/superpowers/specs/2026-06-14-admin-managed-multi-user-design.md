# Admin-Managed Multi-User Upgrade — Design

Date: 2026-06-14
Status: Approved (pending spec review)

## Goal

Turn the open, self-registration MVP into an **admin-managed multi-user** system:

- A single root admin (seeded) is the only one who creates users, sets their access
  tier, uploads documents, decides who can see each document, and curates FAQs.
- Regular users (Employee/Manager) get a simple chat assistant, like other AI tools.
- Admins get a dedicated left-sidebar admin console.
- Sessions stay alive for a long time via refresh tokens, and a disabled user is cut off.
- Fix the document upload (currently a frontend no-op).

The Python AI service (RAG + agents) is **not changed**. All work is in `api/` and `web/`,
plus one EF migration.

## Non-goals

- Multi-organization onboarding/tenancy (schema already carries `OrgId`; not exercised).
- Email delivery of credentials (temp passwords are shown once in the admin UI).
- Password-strength policy beyond a minimum length, password history, or 2FA.
- Role-scoped FAQs (FAQs are visible to all signed-in users in this iteration).

## Roles & access model

- Roles stay three tiers: `Employee (0) < Manager (1) < Admin (2)`.
- **Self-registration is removed**: no register UI, and `POST /api/auth/register` is
  deleted. Users exist only because an admin created them.
- A **root admin is seeded on startup** if no admin exists:
  - Email `admin@bpa.local`, display name `Root Admin`, role `Admin`.
  - Password from config key `Seed:RootAdminPassword` (default `ChangeMe!123`), printed
    once to the console on first creation and noted in `CLAUDE.md`.
  - The root admin's `MustChangePassword` is **false** so the live demo works immediately;
    the console message recommends changing it.
- Document `access_role` remains the "who can use this data" mechanism, now set only by
  admins at upload time. Retrieval filtering in the AI service is unchanged.

## Authentication: refresh tokens

### Tokens
- **Access token (JWT)**: lifetime reduced to 15 min (`Jwt:ExpiryMinutes = 15`). Same
  claims as today (`sub`, `email`, `role`, `org`, `jti`).
- **Refresh token**: opaque 256-bit random string (base64url). Lifetime 30 days
  (`Jwt:RefreshDays = 30`). Returned to the client in the response body.

### Storage
New table `RefreshTokens`:

| Column | Type | Notes |
|--------|------|-------|
| `Id` | Guid (PK) | |
| `UserId` | Guid (FK → Users) | indexed |
| `TokenHash` | string | SHA-256 of the raw token; raw token never stored |
| `ExpiresAt` | DateTime (UTC) | |
| `CreatedAt` | DateTime (UTC) | |
| `RevokedAt` | DateTime? (UTC) | null = active |

A token is valid when `RevokedAt is null AND ExpiresAt > now` and the owning user
`IsActive`.

### Endpoints (replace/extend `AuthController`)
- `POST /api/auth/login` `{email, password}` →
  `{accessToken, refreshToken, expiresAt, email, displayName, role, mustChangePassword}`.
  Issues a new refresh token row.
- `POST /api/auth/refresh` `{refreshToken}` → new `{accessToken, refreshToken, expiresAt}`.
  **Rotation**: the presented refresh token is revoked and a new one issued. A presented
  token that is already revoked/expired/invalid → `401`.
- `POST /api/auth/logout` `{refreshToken}` → revokes that token; `204`.
- `POST /api/auth/change-password` `{currentPassword, newPassword}` (authenticated) →
  verifies current password, sets new hash, clears `MustChangePassword`, and **revokes all
  of the user's refresh tokens except the current session** (forces other sessions to
  re-auth). `204`.
- `POST /api/auth/register` is **removed**.

### Revocation
Disabling or deleting a user revokes all that user's refresh tokens. Because access tokens
are short (15 min), the user loses access within at most 15 min and cannot refresh.

### Frontend token handling (`web/src/api.ts`)
- Store `accessToken` + `refreshToken` in `localStorage`.
- `request()` attaches the access token. On a `401`, it calls `/api/auth/refresh` **once**;
  on success it stores the rotated pair and retries the original request; on failure it
  clears tokens and drops to the login screen. Concurrent 401s share a single in-flight
  refresh (a module-level promise) so we don't refresh-storm.

## User management (admin only)

### `User` entity additions
- `IsActive` (bool, default `true`).
- `MustChangePassword` (bool, default `true` for admin-created users).

### Endpoints — `UsersController`, all `[Authorize(Roles = "Admin")]`, scoped to caller org
- `GET /api/users` → list `{id, email, displayName, role, isActive, mustChangePassword, createdAt}`.
- `POST /api/users` `{email, displayName, role}` → creates the user with an
  **auto-generated temp password** (12+ chars, mixed classes), `MustChangePassword = true`.
  Response includes `tempPassword` **once** (only on creation). Duplicate email in org → `409`.
- `POST /api/users/{id}/reset-password` → generates a new temp password, sets
  `MustChangePassword = true`, revokes the user's refresh tokens, returns `tempPassword` once.
- `PATCH /api/users/{id}` `{role?, isActive?}` → change role and/or enable/disable. Disabling
  revokes the user's refresh tokens.
- `DELETE /api/users/{id}` → delete the user and their refresh tokens.
- Guardrails: an admin cannot disable, demote, or delete **their own** account, and the
  **last active admin** in the org cannot be removed/demoted (prevents lockout).

### Forced password change
If `mustChangePassword` is true after login, the web app routes the user to a "Set a new
password" screen and blocks all other views until `change-password` succeeds.

## FAQs (admin-curated clickable chips)

### `Faq` entity (new table `Faqs`)
| Column | Type | Notes |
|--------|------|-------|
| `Id` | Guid (PK) | |
| `OrgId` | Guid | scope |
| `Question` | string (≤ 500) | |
| `SortOrder` | int | ascending display order |
| `CreatedAt` | DateTime (UTC) | |

### Endpoints — `FaqsController`
- `GET /api/faqs` (any authenticated user) → ordered list `{id, question, sortOrder}`.
- `POST /api/faqs` `{question, sortOrder?}` — admin only.
- `PUT /api/faqs/{id}` `{question, sortOrder}` — admin only.
- `DELETE /api/faqs/{id}` — admin only.

### Behavior
On the user chat screen the FAQs render as clickable suggestion chips. Clicking a chip
submits that question through the existing `POST /api/ai/agent` flow — a live grounded
answer, role-scoped as today. Seed 3–4 starter FAQs on startup if the table is empty.

## Frontend: two distinct UIs

Routing decision after login (and after any forced password change): if `role === Admin`
render the **Admin console**, otherwise render the **User chat**.

### Admin console — left sidebar layout
Sidebar: brand + nav (Overview, Users, Knowledge, FAQs, Audit) + signed-in identity + sign
out. Content area renders the selected section.

- **Overview**: stat cards (user count, document count, FAQ count, audit-entry count) and a
  short recent-activity list (latest audit entries).
- **Users**: table (name, email, role, status, actions). "Create user" opens a modal
  collecting email/name/role; on success it reveals the temp password with a copy button
  and a "shown only once" warning. Row actions: reset password (reveals new temp password),
  change role, enable/disable, delete — respecting the lockout guardrails.
- **Knowledge**: the document uploader (file + access-role select) **fixed** — the upload
  button is disabled until a file is selected, shows an explicit error/progress state, and
  refreshes the list on success — plus the existing document table with status badges.
- **FAQs**: editable list — add/edit/delete questions and reorder via `SortOrder`.
- **Audit**: the existing audit table.

### User chat — simple assistant
The existing assistant conversation (agent route badge, grounded answer, sources, structured
output) **plus**: a row of FAQ suggestion chips near the input, a "Change password" action,
and sign out. No Documents tab, no upload, no audit. This is intentionally minimal, like a
consumer AI chat.

## The upload fix

Root cause: the backend upload endpoint works (verified: `POST /api/documents` returns 201
and indexes). The current `Documents.tsx` silently `return`s when no file is registered, so
the button appears dead. The fix lives in the admin console's Knowledge uploader: disable
the button until a file is chosen, surface validation and request errors, show an uploading
state, and clear/refresh on success.

## Data & migration summary

One EF Core migration:
- `Users`: add `IsActive` (bool, default true), `MustChangePassword` (bool, default true).
- New table `RefreshTokens` (see schema above).
- New table `Faqs` (see schema above).

`DbInitializer` additions: seed the root admin if no admin exists; seed starter FAQs if the
Faqs table is empty. Existing org/process seeding is unchanged.

## Testing

- **API**: refresh rotation (old token rejected after refresh), refresh of a
  revoked/expired token → 401, disabling a user invalidates refresh, change-password clears
  the flag and revokes other sessions, last-admin lockout guard, user-management RBAC
  (Employee/Manager → 403). Mirror the existing controller test style.
- **Web**: typecheck clean (`tsc`); manual/Playwright verification of: forced password
  change on first login, admin creates a user and the temp password is shown, that user
  logs in and is forced to change password, refresh keeps a session alive past access-token
  expiry, upload works from the Knowledge page, FAQ chip runs a query.

## Out of scope / future

Email/SMS credential delivery, password policies & history, 2FA, role-scoped FAQs,
multi-org onboarding, and per-document share lists (beyond the role tier).
