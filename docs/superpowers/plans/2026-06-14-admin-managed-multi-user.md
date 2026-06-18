# Admin-Managed Multi-User Upgrade — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement
> this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the open self-registration MVP into an admin-managed multi-user system
with refresh-token sessions, admin-only user/document/FAQ management, a left-sidebar admin
console, a simple user chat, and a working document upload.

**Architecture:** All changes in `api/` (.NET) and `web/` (React); the Python AI service is
untouched. Backend first (data model → auth → controllers → seeding → tests), then frontend
(token client → routing → admin console → user chat). One EF migration adds `User` fields
and the `RefreshTokens` and `Faqs` tables.

**Tech Stack:** .NET 10 / EF Core / MS SQL / JWT, React 18 / TS / Vite / Tailwind.

Spec: `docs/superpowers/specs/2026-06-14-admin-managed-multi-user-design.md`.

---

## File structure

Backend (`api/`):
- `Domain/Entities/RefreshToken.cs`, `Domain/Entities/Faq.cs` — new entities.
- `Domain/Entities/User.cs` — add `IsActive`, `MustChangePassword`.
- `Infrastructure/Persistence/AppDbContext.cs` — `DbSet`s + config.
- `Infrastructure/Persistence/Migrations/*` — one new migration.
- `Infrastructure/Persistence/DbInitializer.cs` — seed root admin + starter FAQs.
- `Application/Auth/*` — request/response contracts for refresh + change-password.
- `Application/Auth/IAuthService.cs`, `Infrastructure/Auth/AuthService.cs` — refresh logic.
- `Infrastructure/Auth/JwtOptions.cs` — `RefreshDays`; `ExpiryMinutes` default 15.
- `Application/Users/*`, `Infrastructure/Users/UserAdminService.cs` — user management.
- `Application/Faqs/*`, `Infrastructure/Faqs/FaqService.cs` — FAQ CRUD.
- `Api/Controllers/AuthController.cs` — login/refresh/logout/change-password (drop register).
- `Api/Controllers/UsersController.cs`, `Api/Controllers/FaqsController.cs` — new.
- `Api/appsettings.json` — `Jwt:ExpiryMinutes=15`, `Jwt:RefreshDays=30`, `Seed:RootAdminPassword`.
- `Tests/*` — auth refresh, user admin, faqs.

Frontend (`web/`):
- `src/api.ts` — dual-token store, silent refresh, users/faqs/auth endpoints.
- `src/auth.tsx` — session w/ `mustChangePassword`; drop register.
- `src/views/Login.tsx` — login only.
- `src/views/ChangePassword.tsx` — forced/optional password change (new).
- `src/admin/AdminApp.tsx` + `src/admin/pages/{Overview,Users,Knowledge,Faqs}.tsx` — console.
- `src/views/Chat.tsx` — user chat = Assistant + FAQ chips (new wrapper).
- `src/App.tsx` — route Admin console vs user Chat vs forced password change.

---

## Task 1: User entity fields + RefreshToken + Faq entities

**Files:**
- Modify: `api/Domain/Entities/User.cs`
- Create: `api/Domain/Entities/RefreshToken.cs`, `api/Domain/Entities/Faq.cs`

- [ ] Add to `User`: `public bool IsActive { get; set; } = true;` and
  `public bool MustChangePassword { get; set; }`.
- [ ] `RefreshToken`: `Id (Guid)`, `UserId (Guid)`, `User? User`, `TokenHash (string)`,
  `ExpiresAt (DateTime)`, `CreatedAt (DateTime)`, `RevokedAt (DateTime?)`.
- [ ] `Faq`: `Id (Guid)`, `OrgId (Guid)`, `Question (string)`, `SortOrder (int)`,
  `CreatedAt (DateTime)`.
- [ ] Build: `dotnet build api/Domain` → succeeds.
- [ ] Commit: `feat(domain): add user flags, RefreshToken and Faq entities`.

## Task 2: DbContext config + migration

**Files:**
- Modify: `api/Infrastructure/Persistence/AppDbContext.cs`

- [ ] Add `DbSet<RefreshToken> RefreshTokens` and `DbSet<Faq> Faqs`.
- [ ] In `OnModelCreating`: index `RefreshToken.UserId` and `RefreshToken.TokenHash`;
  required strings; `Faq.Question` max length 500.
- [ ] Stop the running API (port 5080) so the build isn't locked.
- [ ] Create migration:
  `dotnet ef migrations add AdminMultiUser --project api/Infrastructure --startup-project api/Api`.
- [ ] Verify the migration file adds the two columns + two tables.
- [ ] Commit: `feat(db): migration for refresh tokens, faqs, user flags`.

## Task 3: JWT options + short access token + refresh contracts

**Files:**
- Modify: `api/Infrastructure/Auth/JwtOptions.cs`, `api/Api/appsettings.json`
- Modify: `api/Application/Auth/AuthContracts.cs` (or create files alongside)

- [ ] `JwtOptions`: `ExpiryMinutes` default `15`; add `int RefreshDays { get; set; } = 30;`.
- [ ] appsettings `Jwt`: `"ExpiryMinutes": 15`, `"RefreshDays": 30`; add
  `"Seed": { "RootAdminPassword": "ChangeMe!123" }`.
- [ ] Contracts: `AuthResponse` gains `RefreshToken (string)` and
  `MustChangePassword (bool)`; add `RefreshRequest(string RefreshToken)`,
  `LogoutRequest(string RefreshToken)`,
  `ChangePasswordRequest(string CurrentPassword, string NewPassword)`.
- [ ] Commit: `feat(auth): refresh/change-password contracts, 15m access token`.

## Task 4: AuthService — issue/rotate/revoke refresh tokens, change password

**Files:**
- Modify: `api/Application/Auth/IAuthService.cs`, `api/Infrastructure/Auth/AuthService.cs`
- Create: `api/Infrastructure/Auth/RefreshTokens.cs` (static helper: generate raw token +
  SHA-256 hash)

- [ ] `RefreshTokens.NewRawToken()` → 32 random bytes base64url; `Hash(raw)` → SHA-256 hex.
- [ ] `IAuthService`: `LoginAsync` returns `AuthResponse?` including a fresh refresh token;
  add `RefreshAsync(string rawToken)`, `LogoutAsync(string rawToken)`,
  `ChangePasswordAsync(Guid userId, string current, string next)`. Remove `RegisterAsync`.
- [ ] `LoginAsync`: reject when `!user.IsActive`; create a `RefreshToken` row
  (hash, `ExpiresAt = now + RefreshDays`); return access+refresh+mustChangePassword.
- [ ] `RefreshAsync`: look up by hash; valid only if not revoked, not expired, user active;
  **rotate** (set `RevokedAt` on old, insert new); return new pair. Else `null`.
- [ ] `LogoutAsync`: set `RevokedAt` on the matching active token.
- [ ] `ChangePasswordAsync`: verify current password; set new hash; clear
  `MustChangePassword`; audit `user.change_password`.
- [ ] Add helper `RevokeAllForUserAsync(userId, except?)` used by change-password and user
  admin.
- [ ] Build api. Commit: `feat(auth): refresh token rotation, change password`.

## Task 5: AuthController — login/refresh/logout/change-password

**Files:**
- Modify: `api/Api/Controllers/AuthController.cs`

- [ ] Remove the register action.
- [ ] `POST login` → 200 with pair or 401.
- [ ] `POST refresh` `{refreshToken}` → 200 new pair or 401.
- [ ] `POST logout` `{refreshToken}` `[Authorize]` → 204.
- [ ] `POST change-password` `{currentPassword,newPassword}` `[Authorize]` → 204 or 400.
- [ ] Validate min new-password length (≥ 8).
- [ ] Build api. Commit: `feat(api): auth endpoints for refresh + change password`.

## Task 6: UserAdminService + UsersController

**Files:**
- Create: `api/Application/Users/UserContracts.cs`, `api/Application/Users/IUserAdminService.cs`
- Create: `api/Infrastructure/Users/UserAdminService.cs`, `api/Infrastructure/Users/PasswordGenerator.cs`
- Create: `api/Api/Controllers/UsersController.cs`
- Modify: `api/Infrastructure/DependencyInjection.cs` (register service)

- [ ] `PasswordGenerator.Generate()` → 14 chars, guaranteed upper/lower/digit/symbol.
- [ ] Contracts: `UserDto(Id,Email,DisplayName,Role,IsActive,MustChangePassword,CreatedAt)`,
  `CreateUserRequest(Email,DisplayName,UserRole Role)`,
  `CreateUserResponse(UserDto User, string TempPassword)`,
  `UpdateUserRequest(UserRole? Role, bool? IsActive)`,
  `ResetPasswordResponse(string TempPassword)`.
- [ ] Service methods scoped to an `orgId`: `ListAsync`, `CreateAsync` (409 on dup email,
  `MustChangePassword=true`), `ResetPasswordAsync` (new temp, revoke user's tokens),
  `UpdateAsync` (role/active; disabling revokes tokens), `DeleteAsync`. Guardrails:
  cannot disable/demote/delete self or the last active admin → throw
  `InvalidOperationException`.
- [ ] `UsersController` `[Authorize(Roles="Admin")]`: GET list, POST create,
  POST `{id}/reset-password`, PATCH `{id}`, DELETE `{id}`. Map guard exception → 409/400.
- [ ] Register `IUserAdminService` in DI.
- [ ] Build api. Commit: `feat(api): admin user management`.

## Task 7: FaqService + FaqsController + DI

**Files:**
- Create: `api/Application/Faqs/FaqContracts.cs`, `api/Application/Faqs/IFaqService.cs`
- Create: `api/Infrastructure/Faqs/FaqService.cs`, `api/Api/Controllers/FaqsController.cs`
- Modify: `api/Infrastructure/DependencyInjection.cs`

- [ ] `FaqDto(Id,Question,SortOrder)`, `FaqInput(string Question, int SortOrder)`.
- [ ] Service: `ListAsync(orgId)` ordered by SortOrder; `CreateAsync`, `UpdateAsync`,
  `DeleteAsync`.
- [ ] `FaqsController`: `GET` `[Authorize]`; `POST/PUT/DELETE` `[Authorize(Roles="Admin")]`.
- [ ] Register in DI.
- [ ] Build api. Commit: `feat(api): admin-curated FAQs`.

## Task 8: Seed root admin + starter FAQs

**Files:**
- Modify: `api/Infrastructure/Persistence/DbInitializer.cs`

- [ ] After migrate + org/process seeding: if no `Admin` user exists, create
  `admin@bpa.local` (role Admin, `MustChangePassword=false`, password from
  `Seed:RootAdminPassword`) and write a console line with the credentials.
- [ ] If `Faqs` empty, insert 3 starter questions (e.g. remote-work policy, expense limit,
  PTO) with SortOrder 1..n.
- [ ] Run the API; confirm console prints the seeded admin and startup succeeds.
- [ ] Commit: `feat(db): seed root admin and starter FAQs`.

## Task 9: Backend tests

**Files:**
- Create/modify under `api/Tests/` mirroring existing controller-test style.

- [ ] Auth: refresh rotates (old token → 401 after use); revoked/expired refresh → 401;
  disabling a user invalidates refresh; change-password clears flag.
- [ ] Users: create returns temp password + dup → 409; last-admin guard → 400/409;
  Employee/Manager hitting `/api/users` → 403.
- [ ] Faqs: admin CRUD; non-admin POST → 403; any user GET → 200.
- [ ] Run: `dotnet test`. Commit: `test: auth refresh, user admin, faqs`.

## Task 10: Frontend api client — dual tokens + silent refresh

**Files:**
- Modify: `web/src/api.ts`

- [ ] Store `bpa_access` + `bpa_refresh`. `tokenStore` get/set/clear both.
- [ ] `request()`: attach access token; on 401 once, call `/api/auth/refresh` with a shared
  in-flight promise; on success persist rotated pair and retry; on failure clear + throw.
- [ ] Update types: `AuthResult` += `refreshToken`, `mustChangePassword`.
- [ ] Add endpoints: `refresh`, `logout`, `changePassword`; `users.list/create/resetPassword/
  update/remove`; `faqs.list/create/update/remove`. Remove `register`.
- [ ] `npm run build` typechecks. Commit: `feat(web): dual-token client with silent refresh`.

## Task 11: auth.tsx + Login (no register) + forced ChangePassword

**Files:**
- Modify: `web/src/auth.tsx`, `web/src/views/Login.tsx`
- Create: `web/src/views/ChangePassword.tsx`

- [ ] `auth.tsx`: session holds `mustChangePassword`; expose `login`, `logout`,
  `changePassword`, `refreshSession`. Remove `register`.
- [ ] `Login.tsx`: email+password only; drop the register toggle/role select.
- [ ] `ChangePassword.tsx`: current + new + confirm; calls `changePassword`; on success
  clears the flag (re-login or update session).
- [ ] Typecheck. Commit: `feat(web): login-only auth + change-password screen`.

## Task 12: Admin console shell + Overview + Users page

**Files:**
- Create: `web/src/admin/AdminApp.tsx`, `web/src/admin/pages/Overview.tsx`,
  `web/src/admin/pages/Users.tsx`

- [ ] `AdminApp`: left sidebar (Overview, Users, Knowledge, FAQs, Audit) + content switch +
  identity/sign-out.
- [ ] `Overview`: stat cards (users, docs, faqs, audit count) + recent audit list.
- [ ] `Users`: table; "Create user" modal → on success show temp password with copy + "shown
  once" note; row actions reset-password (show new temp), change role, enable/disable,
  delete, with error surfacing for guard violations.
- [ ] Typecheck. Commit: `feat(web): admin console shell, overview, users`.

## Task 13: Knowledge page (upload fixed) + FAQs admin page

**Files:**
- Create: `web/src/admin/pages/Knowledge.tsx`, `web/src/admin/pages/Faqs.tsx`
- Reuse: existing `Audit.tsx` inside the console.

- [ ] `Knowledge`: file input + access-role select + Upload button **disabled until a file
  is chosen**; show uploading state + explicit errors; refresh list on success; document
  table with status badges.
- [ ] `Faqs`: list with add/edit/delete + SortOrder editing.
- [ ] Typecheck. Commit: `feat(web): knowledge uploader (fixed) and FAQ admin`.

## Task 14: User chat view + FAQ chips + App routing

**Files:**
- Create: `web/src/views/Chat.tsx`
- Modify: `web/src/App.tsx`, reuse `web/src/views/Assistant.tsx`

- [ ] `Chat`: render Assistant; fetch `/api/faqs`; show clickable chips that submit the
  question through the agent; add Change-password + sign-out affordances.
- [ ] `App.tsx`: after login → if `mustChangePassword` render `ChangePassword`; else if role
  Admin render `AdminApp`; else render `Chat`.
- [ ] Typecheck. Commit: `feat(web): user chat with FAQ chips + admin/user routing`.

## Task 15: End-to-end verification

- [ ] Start all services (per `startup-steps` memory).
- [ ] Admin logs in (`admin@bpa.local`), creates a user → temp password shown.
- [ ] New user logs in → forced to change password → lands on chat.
- [ ] Admin uploads a doc on Knowledge → status reaches Indexed.
- [ ] FAQ chip on chat runs a grounded answer.
- [ ] Leave a session idle past 15 min (or shorten token) → silent refresh keeps it alive.
- [ ] Update `CLAUDE.md` (new auth model, seeded admin creds, endpoints). Commit docs.

---

## Self-review notes
- Spec coverage: roles/no-register (T3,T5,T11,T14), refresh tokens (T1–T5,T10), user mgmt
  (T6,T12), FAQs (T7,T8,T13,T14), two UIs (T12–T14), upload fix (T13), seeding (T8),
  migration (T2), tests (T9). All spec sections mapped.
- Names kept consistent: `MustChangePassword`, `IsActive`, `RefreshToken.TokenHash`,
  `tempPassword`, `mustChangePassword` across backend/frontend.
