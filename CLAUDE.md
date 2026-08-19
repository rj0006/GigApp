# GigApp

Local services marketplace (India). Customers post tasks; verified partners accept and complete them.
Three-sided: customer, partner ("provider"), admin.

**Stack:** ASP.NET Core 8 · PostgreSQL · vanilla JS + CSS · Flutter (mobile)

## Working agreement

How the user wants help delivered on this project. These override default response style.

1. **Be concise.** After code, write a **one-line** summary in simple Indian English that anyone can
   follow. Do not pad it. Longer explanation is fine when the change genuinely needs it — clarity
   beats an arbitrary limit.
2. **Mobile (Flutter) = clean architecture.** Layered `data / domain / presentation`, a proper state
   management solution, and a typed API client with a single auth interceptor. No logic in widgets.
3. **Language — depends on where it goes:**
   - **Chat replies:** simple Hinglish / Indian English. Short sentences, no heavy vocabulary.
   - **Everything written to a file — web page text, code comments, docs, commit messages, log
     messages, validation messages:** **Indian English only. No Hinglish, ever.** Those are read by
     users and other developers, so they stay in plain professional English.
4. **Breaking changes → top 3 bullets first**, before any code or explanation.
5. **Max one clarifying question.** If more than one thing is unclear, assume best practice, list the
   assumptions at the top, and keep going. Never block waiting for an answer.
6. **Repeated code → reusable helper.** If the same code appears in more than two places, extract it
   into a reusable function, partial or utility instead of copy-pasting. Record every new helper in
   [docs/REUSABLE.md](docs/REUSABLE.md) with its signature and a usage snippet, so next time it can be
   called directly without reading the implementation again.

## Structure

| Path | What | State |
|---|---|---|
| `GigApp.Api/` | ASP.NET Core 8 API + Razor portals + PostgreSQL | **Active — all real code lives here** |
| `customer_app/` | Flutter | Untouched template |
| `provider_app/` | Flutter | Untouched template |
| `admin_panel/` | Flutter | Untouched template — **slated for deletion**, replaced by the Razor `/admin` portal |

Not a git repository yet.

## Architecture decisions

Read these before changing auth, identity, or the data model. They were deliberate.

**One `User` table for everyone.** Identity and credentials (name, phone, email, password hash, role)
live on `User` for customers, partners and admins alike. `Partner` is a *profile* row linked by a
required `UserId` FK — it holds only skill category, verification and KYC. Never add name/phone/email
to `Partner`; that duplication was removed on purpose.

**Phone is the primary identifier, email is optional.** `User.Email` is nullable and uniquely indexed
(Postgres allows many NULLs). Login accepts either — `LoginRequest.Identifier` is routed by whether it
contains `@`. This shape exists because **phone/OTP verification is the planned direction**; `User` already
carries `IsPhoneVerified` / `PhoneVerifiedAt`, currently set by the dev seeder and enforced nowhere.
When OTP lands, gate login and task acceptance on `IsPhoneVerified` and make password optional.

**JWT is the single source of truth for auth.** One `AuthService` issues tokens for every client.
Mobile apps send `Authorization: Bearer`; the Razor portals park the same JWT in an HttpOnly cookie
(`gigapp_token`) that `JwtBearerEvents.OnMessageReceived` reads. There is deliberately **no separate
cookie/session auth scheme** — do not add ASP.NET Identity or `AddCookie` alongside this.

**No public admin registration.** `AuthController` exposes `register/customer` and `register/partner`
only. Admins are seeded or promoted by an existing admin. `/admin` is login-only by design.

**Roles are lowercase strings**: `customer`, `partner`, `admin` — see `UserRoles`. Enforced by a DB check
constraint. The seed data originally used capitalised roles; a migration normalised them. Never compare
role strings case-insensitively as a workaround — fix the data.

**Task lifecycle is a state machine.** `GigTaskStatus.CanTransition` is the single authority:
`pending → accepted → in_progress → completed`, with `cancelled` reachable from any non-terminal state.
Both the API and the Razor portals call it. A DB check constraint backs the allowed values.

**One category master, referenced by FK.** `SkillCategory` is admin-managed; both `Partner.SkillCategoryId`
and `GigTask.CategoryId` point at it. They deliberately share one taxonomy — matching a task to a partner
is impossible if "Plumbing" and "Plumber" can both exist. Free-text category columns were migrated away
on purpose; never reintroduce one.

- Uniqueness is case-insensitive, via a functional index `UX_SkillCategories_Name_Lower` created in raw
  SQL (EF cannot express `LOWER(...)` indexes).
- Both FKs are `DeleteBehavior.Restrict`. A category in use **cannot** be deleted — deactivate it instead.
  `IsActive = false` hides it from every picker while leaving existing rows valid.
- Every category picker goes through `ICategoryLookup` so an inactive category can never be selected from
  anywhere. `GetOptionsIncludingAsync` additionally returns the currently-selected one, so a partner sitting
  on a deactivated category still sees it in their own edit form.
- Servers validate the id on every write — never trust the posted category.

## Commands

Run from the repo root.

```bash
dotnet build GigApp.Api/GigApp.Api.sln
```

```bash
dotnet run --project GigApp.Api/GigApp.Api/GigApp.Api.csproj
```

```bash
dotnet ef migrations add <Name> --project GigApp.Api/GigApp.Api/GigApp.Api.csproj
```

```bash
dotnet ef database update --project GigApp.Api/GigApp.Api/GigApp.Api.csproj
```

`dotnet ef` with `--no-build` uses a stale assembly and will silently skip a
just-added migration. Always let it build.

Postgres 18 runs locally as a service; `psql` is at `C:\Program Files\PostgreSQL\18\bin\psql.exe`
(not on PATH). Database `gigapp_db`.

## Dev accounts

Seeded on every Development startup by `DbSeeder` (idempotent — it only fills gaps).
Password for all: `Gigapp@123`. Sign in with mobile **or** email.

| Role | Mobile | Email |
|---|---|---|
| admin | 8683846689 | rahuljangra807@gmail.com |
| customer | 7879838798 | amit@gmail.com |
| partner | 7459867732 | shivam@gmail.com |

## Gotchas that have already bitten

**Razor renders `bool` attributes as HTML boolean attributes.** `value="@(!x)"` emits `value="value"`
when true and *omits the attribute* when false. Both fail to bind and silently default to `false` — which
made an "Approve" button perform a revoke. Always render booleans as strings in markup:
`value="@(x ? "false" : "true")"`. Guard the action with a `ModelState.IsValid` check too.

**Npgsql rejects non-UTC `DateTime` on `timestamp with time zone`.** JSON without an offset deserializes
as `Unspecified` and throws at save time. Run every client-supplied timestamp through
`DateTimeExtensions.ToUtc()`. Use `DateTime.UtcNow`, never `DateTime.Now`.

**Swagger only documents `api/*`.** A `DocInclusionPredicate` in `Program.cs` excludes the Razor
portals — Swashbuckle throws on any action without an explicit HTTP verb. Give every MVC action one anyway.

**Accepting a task must stay atomic.** `AcceptTask` uses a single conditional `ExecuteUpdateAsync`
(`WHERE Id = @id AND Status = 'pending'`) — the WHERE clause is the lock. Verified under 8 concurrent
requests: exactly one 200, seven 409s. Do not refactor this into read-then-write.

## Admin portal layout

`/admin` uses `_AdminLayout.cshtml` (sidebar shell), not the plain `_Layout` the other two portals use.
The sidebar is grouped: **Dashboard**, **Masters** → Skill categories, **Approvals** → Partner KYC,
**User management** → Customers / Partners / Administrators, **Operations** → Tasks.

- Active menu state is derived from `Context.Request.Path` in the layout — no per-view flag to keep in sync.
- The pending-KYC badge count is set once in `AdminController.OnActionExecutionAsync`, not per action.
- The mobile drawer is a CSS-only checkbox toggle; there is no sidebar JavaScript.
- Adding a master: new section under `Masters` in `_AdminLayout`, routes under `/admin/masters/...`.

The only JavaScript in the project is `wwwroot/js/category-picker.js`, which binds a `<datalist>` name
input to a hidden id field. An unrecognised name resolves to an empty id, which the server then rejects —
so a typo cannot invent a category.

## Conventions

- Controllers never bind entities from the request body — always a DTO. `CustomerId`/`PartnerId` come
  from the token, never the payload.
- Return `UserDto`/`PartnerDto`/`GigTaskDto`, never the entity (`PasswordHash` must not leak).
- List endpoints use the `DetailedTasks` queryable so responses have a consistent shape.
- Auth failures return one message for both "no such account" and "wrong password".
- Portal POST actions use PRG: set `TempData["Success"]`/`TempData["Error"]`, then redirect.

## Not done yet

- Phone/OTP verification (structure is ready; SMS provider and challenge table are not)
- Refresh tokens — JWTs currently last 7 days because mobile cannot re-auth silently
- Rate limiting on login and registration
- `Jwt:Key` and the DB password are in config files; production must supply `Jwt__Key` via environment
- The three Flutter apps have not been started
- `IsAvailable` does not currently gate task acceptance (only `IsVerified` does)
