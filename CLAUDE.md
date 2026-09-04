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
   - **Everything written to a file — web page text, docs, commit messages, log messages,
     validation messages:** **Indian English only. No Hinglish, ever.** Those are read by
     users and other developers, so they stay in plain professional English.
4. **Breaking changes → top 3 bullets first**, before any code or explanation.
5. **Max one clarifying question.** If more than one thing is unclear, assume best practice, list the
   assumptions at the top, and keep going. Never block waiting for an answer.
6. **No comments in code.** No `///` summary blocks, no `<param>` or `<see>` tags, no block
   comments, no section banners — in C#, JavaScript, Razor or SQL. Name things well enough that
   the code reads on its own. If a line genuinely cannot be understood without help, one plain
   single-line comment is allowed, and that is the ceiling.
   - **Never write the same comment twice.** If an explanation belongs in more than one file it is
     not a comment, it is a rule — put it in this file or in [docs/REUSABLE.md](docs/REUSABLE.md)
     once. Repeating it is the same duplication that rule 8 bans for code, and it rots faster,
     because the copies drift as soon as one of them is edited.
   - The "why" belongs in this file and in [docs/REUSABLE.md](docs/REUSABLE.md), not scattered
     through the source.
7. **Alerts use SweetAlert2, never the browser dialogs.** No `alert()`, `confirm()` or `prompt()`.
   Go through `App.confirmAction`, `App.notify` or `showToast` in `wwwroot/js/global.js`.
8. **Repeated code → reusable helper.** If the same code appears in more than two places, extract it
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

**One account per identifier per role.** The same phone or email may hold a customer account **and**
a partner account — the unique indexes are `(Phone, Role)` and `(Email, Role)`, not the plain columns.
One person testing both sides, or a partner who also books work, needs exactly this. What is still
refused is a second account with the same role.

- The rows are separate `User` records with their own password, profile and history. They are not
  linked, and nothing is shared between them.
- Login therefore has to be told which one. `LoginRequest.Role` carries it; each portal passes its
  own `RequiredRole`, and the admin portal is expanded to accept `superadmin` too. Mobile clients
  must send it as well.
- With no role, `AuthService` verifies the password against every candidate. Exactly one match signs
  in; more than one returns "sign in from the portal for the account you want". Nothing is revealed
  before the password is right, so this does not leak which accounts exist.
- `admin` and `superadmin` are separate role values, so in theory one phone could hold both. There is
  no public admin registration, and promotion mutates the existing row, so it cannot happen by accident.

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

**Roles are lowercase strings**: `customer`, `partner`, `admin`, `superadmin` — see `UserRoles`. Enforced by a DB check
constraint. The seed data originally used capitalised roles; a migration normalised them. Never compare
role strings case-insensitively as a workaround — fix the data.

**Task lifecycle is a state machine.** `GigTaskStatus.CanTransition` is the single authority:
`pending → accepted → in_progress → completed`, with `cancelled` reachable from any non-terminal state.
Both the API and the Razor portals call it. A DB check constraint backs the allowed values.

**Partner KYC is a four-state column, not a boolean.** `Partner.KycStatus` holds
`not_submitted → pending → approved | rejected`, backed by `CK_Partners_KycStatus`. The old
`IsVerified` boolean could not tell "never reviewed" from "rejected", so an admin could not see
what they had already refused and a partner was never told why. `Partner.IsVerified` still exists
but is now a computed `KycStatus == approved` and is `Ignore`d by EF — never map or assign it.

- **Changing skill sends an approved partner back to `pending`.** The approval was for that skill, so
  it cannot carry over. `PartnerKyc.ChangeSkill` is the only place this happens, and both the portal
  and `PartnersController.UpdateMyProfile` call it. The partner is warned with a SweetAlert confirm
  before the form submits.
- `Partner.KycReviewNote` says **why** a partner is in the queue — "Skill changed from Plumbing to
  Electrical on 04 Sep 2026" — so an administrator knows what to re-check rather than reviewing blind.
  `PartnerKyc.Review` clears it once a decision is made.
- Rejecting **requires a reason**; both `AdminController.SetVerification` and
  `PartnersController.SetVerification` refuse without one. It is the only thing the partner sees.
- Any KYC resubmission sets the status straight back to `pending` and clears the reason, whatever
  it was before. That is what lets a rejected partner get back in the queue without a new account.
- `/admin/approvals` lists everything that is not approved, ordered pending → not submitted →
  rejected. The sidebar badge counts **only `pending`** — a rejected partner is waiting on
  themselves, so counting them would keep the badge lit with no work behind it.

**The role claim is refreshed from the database on every request.** `OnTokenValidated` checks
`IsActive` and overwrites the token role with the stored one. Tokens last seven days, so without
this a promotion to `superadmin` (or a demotion) would do nothing until the token expired — which
is exactly what made the super-admin screens invisible after the seeder promoted the founder.

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
| customer | 7879838799 | amit@gmail.com |
| partner | 7459867732 | shivam@gmail.com |

## Gotchas that have already bitten

**Razor renders `bool` attributes as HTML boolean attributes.** `value="@(!x)"` emits `value="value"`
when true and *omits the attribute* when false. Both fail to bind and silently default to `false` — which
made an "Approve" button perform a revoke. Always render booleans as strings in markup:
`value="@(x ? "false" : "true")"`. Guard the action with a `ModelState.IsValid` check too.

**Never name a Razor view local `page`.** `@page.TotalCount` is parsed as the `@page` directive, not
as a property access, and the view fails to compile with an unrelated-looking error. Every paged view
names it `list` instead.

**A file upload form needs `enctype="multipart/form-data"`.** Without it the browser posts only the
file names, every `IFormFile` binds as null, and nothing reports an error — the upload silently does
nothing. Applies to partner registration, KYC upload and the profile photo.

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

All JavaScript lives in `wwwroot/js/global.js` and is driven by `data-` attributes, so views stay
markup-only. Pickers bind a visible name input to a hidden id field; an unrecognised name resolves
to an empty id, which the server then rejects, so a typo cannot invent a category.

SweetAlert2 is vendored at `wwwroot/lib/sweetalert2/` and loaded by both layouts. `data-confirm` on a
form opens a SweetAlert confirm instead of the browser one — add `data-confirm-title`,
`data-confirm-ok` and `data-confirm-icon` to tune it, and `data-confirm-changed="fieldId"` to skip
the prompt when that field still holds its original value. Because SweetAlert is asynchronous, the
handler always cancels the first submit and re-submits after confirmation, carrying the clicked
button name and value forward so multi-button forms still work.

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
- `IsAvailable` does not currently gate task acceptance (only an approved KYC does)
