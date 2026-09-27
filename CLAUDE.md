# GigApp

Local services marketplace (India). Customers post tasks; verified partners accept and complete them.
Three-sided: customer, partner ("provider"), admin.

**Stack:** ASP.NET Core 8 · PostgreSQL · vanilla JS + CSS · Flutter (mobile)

Read [docs/CLAUDE-MEMORY.md](docs/CLAUDE-MEMORY.md) too — a backup of Claude's own standing
memory for this project, checked in so it survives a machine reinstall.

**The customer web portal is permanent.** Local services are found through search, and an app cannot
be indexed, so the Razor customer pages are an acquisition channel and grow public category and city
landing pages. Only the partner portal is replaceable by the app.

## Working agreement

How the user wants help delivered on this project. These override default response style.

1. **Be concise.** After code, write a **one or two line** summary in simple Indian English that
   anyone can follow. Do not pad it. Do not narrate the investigation — what was checked, files
   read, theories tried, tests run. Just the code and the one/two-line summary. This has been said
   multiple times; do not drift back to long explanations.
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
     once. Repeating it is the same duplication that rule 9 bans for code, and it rots faster,
     because the copies drift as soon as one of them is edited.
   - The "why" belongs in this file and in [docs/REUSABLE.md](docs/REUSABLE.md), not scattered
     through the source.
7. **Alerts use SweetAlert2, never the browser dialogs.** No `alert()`, `confirm()` or `prompt()`.
   Go through `App.confirmAction`, `App.notify` or `showToast` in `wwwroot/js/global.js`.
8. **Record every business rule in [docs/IMPLEMENTATION-LOG.md](docs/IMPLEMENTATION-LOG.md).**
   Whenever a specific behaviour is asked for — a validation, a state change, who may see what —
   write it there before moving on. The Flutter apps have to reproduce the same rules, so this file
   is the contract between the portals and mobile. Newest entry first.
9. **Repeated code → reusable helper.** If the same code appears in more than two places, extract it
   into a reusable function, partial or utility instead of copy-pasting. Record every new helper in
   [docs/REUSABLE.md](docs/REUSABLE.md) with its signature and a usage snippet, so next time it can be
   called directly without reading the implementation again.

## Structure

| Path | What | State |
|---|---|---|
| `GigApp.Api/` | ASP.NET Core 8 API + Razor portals + PostgreSQL | **Active — all real code lives here** |
| `customer_app/` | Flutter | Sign-in and a read-only "My tasks" list — see "Not done yet" |
| `provider_app/` | Flutter | Sectioned app (Home / Orders / Wallet / Profile) — see "Not done yet" |
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
- Every phone/email uniqueness check anywhere in the app must filter by `Role` — `ProfileService.UpdateAsync`
  didn't, and a person holding two roles on the same phone was refused when saving their own Details on
  either one, since it matched the other role's row on plain phone equality. Fixed by adding `Role`
  to the check; watch for the same gap before writing a new one.

**Phone is the primary identifier, email is optional.** `User.Email` is nullable and uniquely indexed
(Postgres allows many NULLs). Login accepts either — `LoginRequest.Identifier` is routed by whether it
contains `@`. This shape exists because phone/OTP verification was always the planned direction, and
it has now landed for the customer and partner portals — see below.

**Mobile OTP is the default sign-in for Customer and Partner; password is a fallback, not a second
front door.** `AuthSettings` (one row, edited by a super admin at `/admin/settings/auth`) holds
`CustomerLoginMode`/`PartnerLoginMode`, each `password` or `otp`, defaulting to `otp`; admin always
stays password-only. `AuthService`'s password path is completely untouched — OTP is an additive
second way in, `AuthService.LoginWithOtpAsync`, that skips the password check entirely for a
phone+role that already has an account. `IOtpService` (`OtpChallenge`: hashed 6-digit code,
5-minute validity, 5 attempts) issues and checks the code; `IOtpSender` is the send channel,
following the exact same pluggable-channel shape as `INotificationChannel` — today's
`LoggingOtpSender` just logs it (and hands it back in the response when `IsDevelopment()`), and
wiring a real SMS provider later means registering a different `IOtpSender`, nothing else. Each
concrete SMS provider (Twilio, MSG91, TextLocal, ...) has its own credential shape, so making the
provider and its credentials admin-configurable — rather than one fixed config block — is unbuilt,
scoped work, not a small addition.

- **There is exactly one entry point per portal, never a separate "register" link.** A known number
  signs in; an unknown customer number is asked for a name on the same screen and goes through the
  ordinary `AuthService.RegisterCustomerAsync` with a random password the user never sees; an
  unknown partner number is sent into the existing `/provider/register` KYC form with phone
  pre-filled and locked and the password fields hidden (still generated and posted, so that form's
  validation is untouched) — OTP replaces the password there, never the KYC review. Nothing in the
  UI should ever link to a classic Register page as an alternative to Login — `_ShopHeader`,
  `_ShopLayout`'s footer and `Checkout.cshtml` each had two links/buttons for this and now have one.
- **Password stays reachable, deliberately, through "More options" on the code step** — some users
  will always prefer it, and admin needs a password-only mode it can switch either portal back to.
  "More options" opens a small modal (`#otp-more-options` in `_OtpAuthForm.cshtml`, matching the
  Uber/Microsoft "choose another way to sign in" pattern) rather than navigating away; picking
  "Sign in with password" swaps in a third inline step of the same card, carrying the phone number
  already entered, and posts through the same `data-login-form`/`wireLoginForm` every classic
  login page uses. The modal lists exactly one entry today — real SMS/WhatsApp/email OTP delivery
  is not built, so there is nothing else to offer yet; adding one later is a new modal row plus its
  own step, not a redesign. Nothing links to the classic Login/Register pages except the modal
  trigger's own `href` (a plain fallback if Bootstrap's JS has not loaded), and
  `CustomerController.Register`'s GET redirects to `/customer/login` whenever
  `CustomerLoginMode == otp`, so a stale bookmark cannot resurrect the two-page split.
- A successful OTP sets `IsPhoneVerified`/`PhoneVerifiedAt` — this is the moment those columns
  stopped being seeder-only and dead everywhere else.
- **`api/auth/otp/request`/`otp/verify` are the mobile-facing counterpart of the portals' own OTP
  actions.** The portal ones call `SignInJsonAsync`, which sets the HttpOnly cookie and returns
  `{redirectTo}` — useless to a client with no cookie jar. These two on `AuthController` take an
  explicit `Role` in the body (the portal knows its own `RequiredRole`; a generic API controller
  does not) and return the same `AuthResponse` shape `api/auth/login` does, or
  `{requiresPartnerRegistration:true}` for an unknown partner number, so a mobile client knows to
  route into registration instead of treating it as a failure. Both call straight into the same
  `IOtpService` the portals use — no OTP logic exists twice.

**JWT is the single source of truth for auth.** One `AuthService` issues tokens for every client.
Mobile apps send `Authorization: Bearer`; the Razor portals park the same JWT in an HttpOnly cookie
(`gigapp_token`) that `JwtBearerEvents.OnMessageReceived` reads. There is deliberately **no separate
cookie/session auth scheme** — do not add ASP.NET Identity or `AddCookie` alongside this.

**The JWT is short-lived; a rotating refresh token is what keeps anyone signed in past that.**
`Jwt:ExpiryMinutes` is 60. Every login, registration and OTP verify also issues a refresh token
(`RefreshTokenService`, `RefreshTokens` table) good for `Jwt:RefreshTokenExpiryDays` (30) of activity
— `AuthResponse.RefreshToken`/`RefreshExpiresAtUtc` carry it alongside the JWT. **Redeeming one is
single-use**: `RedeemAsync` revokes the token it was given and issues a brand new one in the same
call, so a captured-and-replayed old token stops working the instant the real device redeems its own
copy — there is no window where both the original and a thief's copy are simultaneously valid.
Mobile clients call `POST api/auth/refresh` themselves; the Razor portals never see either token
value — `gigapp_token` and a second cookie, `gigapp_refresh`, are both HttpOnly, and `global.js`'s
`wireSessionRefresh` silently calls the shared `POST /{portal}/refresh-session` every 20 minutes to
renew both before the short-lived one expires. **Signing out revokes the refresh token itself**,
not just the two cookies — `PortalControllerBase.ClearAuthCookieAsync` reads the raw refresh cookie
and revokes that exact row before deleting anything, so a stolen copy cannot outlive a logout.
**Manage devices** (a profile section, every role) is this same table read the other way — each row
is one device, human-labelled from its User-Agent (`DeviceLabel.FromUserAgent`), revocable
individually or all-at-once from `api/devices`.

**Login, registration and OTP are rate-limited, not just correct.** Two named policies in
`Program.cs` (`RateLimiterPolicies.Auth` — 10/minute, `.OtpRequest` — 3/5 minutes, both keyed by
client IP): the tighter one exists because requesting an OTP is what will eventually cost a real SMS
once a provider is wired in, so it is deliberately harder to hammer than a login attempt. A
rate-limited request gets the same `{title, status}` JSON shape as every other error, via a shared
`OnRejected` handler — no special-casing needed on the client. Add `[EnableRateLimiting(...)]` to
any new endpoint that accepts unauthenticated, attacker-reachable input.

**No public admin registration.** `AuthController` exposes `register/customer` and `register/partner`
only. Admins are seeded or promoted by an existing admin. `/admin` is login-only by design.

**Roles are lowercase strings**: `customer`, `partner`, `admin`, `superadmin` — see `UserRoles`. Enforced by a DB check
constraint. The seed data originally used capitalised roles; a migration normalised them. Never compare
role strings case-insensitively as a workaround — fix the data.

**Task lifecycle is a state machine.** `GigTaskStatus.CanTransition` is the single authority:
`pending → accepted → in_progress → completed`, with `cancelled` reachable from any non-terminal state.
Both the API and the Razor portals call it. A DB check constraint backs the allowed values.

**How a partner is chosen depends on `GigTask.BookingMode`, and the service item decides it.**
A `ServiceItem` with `AllowsInstantBooking` and a `BasePayout` above zero books as `instant`;
everything else books as `bidding`. Nothing else sets the mode, and the customer cannot choose it.

- **Instant** is a fixed price. Both `Budget` and `AgreedAmount` are set from `BasePayout` at
  booking, the posted amount is ignored, bidding is refused, and the first approved, **on-duty**
  partner in that category claims it. `ITaskClaimService.ClaimAsync` is the only claim path — the
  same conditional `ExecuteUpdateAsync` lock as before, extended with `BookingMode = instant` so a
  bidding task can never be grabbed this way. Only the partner's own direct claim checks
  `IsAvailable` this way — the admin's manual `AssignAsync` override does not, on purpose.
- **Bidding** is unchanged: partners quote, the customer accepts one. Placing a bid
  (`BidService.PlaceAsync`) checks `IsAvailable` the same way `ClaimAsync` does — an off-duty partner
  cannot start a new negotiation, though one already underway (`AcceptCounterAsync`) is not re-checked.
  A customer may view any partner who has bid on one of their tasks, not only a partner already
  assigned to one — `PartnersController.MayViewPartnerAsync` checks `TaskBids` for exactly this,
  since deciding whom to accept means seeing the bidder's profile first.
- **Assignment is automatic for a fixed price.** `IOfferService` offers the job to the best-ranked
  partner for `Platform:OfferWindowSeconds`, then the next, and opens it to everyone when the chain
  runs out. `IMatchService.RankPartnersAsync` does the scoring — distance 60%, rating 40%.
  `Platform:AutoAssignInstant` turns it off and falls back to first-come-first-served.
- **An offer is first refusal, not a lock.** While one is live nobody else may claim that job; once
  it lapses everyone may. `OfferExpiryWorker` sweeps every 30 seconds so a silent partner cannot
  strand a customer. Passing carries no penalty, by design — punish declining and partners stop
  answering at all.
- **Support can still assign by hand.** `ITaskClaimService.AssignAsync` records the choice with a
  required note and closes any open bids. `/admin/tasks` shows the same ranked shortlist, and
  `AssignAsync` accepts a `cancelled` task exactly like a `pending` one — the same "Assign" button
  reads "Reassign" and notifies the newly assigned partner as well as the customer.
- **A partner cancelling an accepted job reopens it, excluding only that partner.**
  `ProviderController.UpdateStatus` special-cases `status == cancelled`: it clears `PartnerId`, puts
  the task back to `pending`, restarts the offer chain for an instant job, and notifies the customer
  and every admin. `TaskCancellations(GigTaskId, PartnerId, Reason, CancelledAt)` is the exclusion
  list — `OfferService.StartAsync`, `TaskClaimService.ClaimAsync` and the partner's own
  available-tasks query all check it, so the job never resurfaces for the partner who dropped it,
  but an admin can still reassign it to them by hand.

**A rating is one row per side per task, and the average is a cache on `User`.** `TaskRatings` is
unique on `(GigTaskId, RaterRole)`, so neither side rates twice and neither can revise. The partner
must rate the customer to close a job — the rating and the status change are written in one save —
while the customer's rating of the partner is optional and can be left until later. `User.AverageRating`
and `User.RatingCount` belong to the person, not the `Partner` profile, so one pair of columns serves
both sides; `RatingService.RefreshAverageAsync` recomputes them from the full history rather than
incrementing, so a bad write cannot drift them permanently.

**Commission is a plan, and tax is a dated country rule.** `CommissionPlans` decides what the
platform keeps — zero percent with a monthly fee is the subscription model. `TaxRules` is a
percentage of one base (`commission`, `gross_earning` or `subscription_fee`), for one country,
between two dates. Running in another country is a data change: add rules with that `CountryCode`
and set `Platform:CountryCode`.

- **The rate is snapshotted onto the ledger when the job settles** — `AppliedPercent`, `BaseAmount`,
  `CommissionPlanId` and `TaxRuleId` are written on the entry. A plan change or a tax revision must
  never alter a job that is already finished.
- **A rate change is a new rule, not an edit.** End the old one with `EffectiveTo` and add its
  replacement. Editing a percentage in place would misstate what past jobs deducted.
- A tax whose base is zero is skipped, so a zero-commission partner pays no GST on commission but
  still has tax deducted on the gross where the law says so.

**Partner earnings run on an append-only ledger.** `LedgerEntries` is never updated or deleted — a
correction is a new row, and `PartnerWallets.Balance` is a cache written in the same transaction.
Completing a task posts the gross earning and the platform commission as **two separate lines**, so
the partner sees the full agreed amount and changing the rate never rewrites history. Every entry
carries a unique `IdempotencyKey`, which is what stops a repeated completion or a retried payout
from paying twice. `Amount > 0` is a check constraint; the direction carries the sign.

**A notification is a row plus zero or more channels.** `INotificationService.PushAsync` writes to
`Notifications` and hands the same request to every registered `INotificationChannel`. In-app
(direct DB read) and `SignalRNotificationChannel` (live push, below) both exist — SMS still needs
DLT registration and push needs Firebase and the Flutter apps — so adding either means registering
a channel and changing nothing that raises a notification. A channel that throws is logged and
skipped: a provider being down must not roll back the thing it was announcing. The bell is filled
once in `PortalControllerBase.OnActionExecutionAsync` for first paint, and kept live afterwards by
the SignalR layer below. `GET api/notifications/summary`, `GET api/notifications` (paged) and
`POST api/notifications/read` exist purely for a client with no Razor first paint — the mobile app.

**Real-time updates go over one SignalR hub, `/hubs/app`, never polling.** A page that needs to stay
live re-runs its own already-written `load()` function when told to, rather than the server pushing
data — `AppHub` only ever sends a **topic string** (e.g. `"tasks"`) over a `"refresh"` event, plus a
`"notification"` event for the bell. This keeps the wire surface to two events total instead of a
typed DTO per feature. `App.realtime.on('topic', handler)` in `global.js` is the client-side
subscription point; a page wires it with one line — `App.realtime.on('tasks', load);` — right after
its own initial `load()` call, so the exact same render path handles both first paint and every
live refresh. Connections join groups in `AppHub.OnConnectedAsync`, looked up fresh from the DB
every time (no caching): `user-{userId}` (personal), `category-{categoryId}-partners` (job-board
liveness for a partner's own skill), and the constant `admins`. `IRealtimeNotifier` is the only way
a service broadcasts — `NotifyUserAsync`/`NotifyCategoryPartnersAsync`/`NotifyAdminsAsync` for the
"refresh" topic, `PushNotificationAsync` for the bell toast, called from `TaskClaimService`,
`GigTasksController` and `BidService` at every point a task or bid changes.
**The hub authenticates mobile exactly like it authenticates the browser.** `JwtBearerEvents.OnMessageReceived`
reads `access_token` off the query string for any path under `/hubs`, because a WebSocket upgrade
cannot carry a custom `Authorization` header — this is the same JWT the cookie carries for the web
portals, so a future Flutter client points its own `AccessTokenProvider` at the same bearer token
and the hub needs no separate mobile-facing work.

**Unhandled errors get a reference, never a stack trace.** `GlobalExceptionFilter` writes one
`ErrorLogs` row and hands the user a code such as `E260906-A3F91C`. `ErrorLogService` saves through
**its own DbContext scope** — the request context is usually the thing that just failed, and a
rolled-back transaction cannot save anything more.

**Every portal has moved from server-rendered Razor to JSON API + page JS — Admin, Provider's
dashboard and its own profile sections, the profile sections every portal shares, Customer's own
sections, Provider's Register, every portal's Login/OTP screens, and the storefront's cart and
checkout.** See
[docs/REUSABLE.md](docs/REUSABLE.md) for the concrete
shape: a thin `View()` action with no ViewModel, one API controller per feature whose `Save`/create
endpoints branch on whether an id was posted, and the view's own data-fetch/render/submit logic
written **inline** in that view's `@section Scripts` block — not a separate `.js` file per page.
`App.api` (the fetch wrapper) and the `Admin` namespace (`escapeHtml`, `thumb`, `showImagePreview`,
`showFormErrors`, `formToJson`, `openKycModal`, `openAccountModal`, `openAddressModal`) live in
`wwwroot/js/global.js`, since they're shared by every page's inline script — despite the name, it is loaded by every
portal's layout and is not admin-only. `_KycModal`/`_AccountModal` are static, id-based partials
included once per page and populated by those two helpers — not one modal per table row;
`Tasks.cshtml`'s assign dialog and Provider's `Index.cshtml` bid/complete/cancel dialogs follow the
same one-shared-modal shape, fetching or ranking only when an admin or partner actually opens one
rather than doing it for every row on every page load. `GET api/partners/me/dashboard` composes the
whole Provider dashboard in one call, the same way `GET api/earnings/{partnerId}` composes the whole
payout ledger page. **`PUT api/gigtasks/{id}/status` is the one place a task's status changes for
every caller, web or mobile** — the partner-cancellation-reopens-job rule lived only in the old Razor
action until this pass moved it into the shared API action itself, which is now non-negotiable: any
new status-changing UI must go through this endpoint, never re-implement the transition rules
locally. **A Razor partial nested inside another partial cannot use `@section Scripts`** — the
layout only picks up sections declared by the top-level view, so a partial's own inline `<script>`
must defer with `document.addEventListener('DOMContentLoaded', ...)` rather than an
immediately-invoked function, or it runs before jQuery has loaded. A partial shared across
**multiple top-level views** has the same problem even when each of those views is itself top-level
— `_LoginCard`, `_OtpAuthForm`, `_ServiceCard` and `_QuantityStepper` are all reused by several
different pages, so their wiring lives centrally in `global.js` (`wireLoginForm`, `wireOtpAuthForm`,
`wireCartControls`), driven by `data-` attributes on otherwise-static markup, rather than as inline
scripts that would have to be duplicated in every page that includes them. **Check `api/profile`,
`api/auth` and the other cross-portal API controllers before writing anything new** — most of the
shared profile sections, all of Customer's own sections, and Provider's Register converted with
zero or near-zero new endpoints, because the mobile-facing API already had them; Order history and
`api/cart` were the genuinely new pieces. `_AddressForm` and `_AddAddressModal` are now a static
shell shared by My addresses and Post-a-task's inline "add another address", driven by one
`Admin.openAddressModal(address, onSaved)` helper. **A login or registration POST that must issue
the auth cookie stays on the portal MVC controller, never `api/auth` directly** — `IssueAuthCookie`
needs `Response.Cookies`, which only the portal controller instance has; `SignInJsonAsync` on
`PortalControllerBase` is the fetch-friendly counterpart to the older `SignInAsync`, returning a
JSON error instead of re-rendering a view. **JSON model binding does not convert `""` to `null` the
way form binding does** — an optional string field with `[StringLength(MinimumLength = ...)]` that
moves from a classic form post to `[FromBody]` JSON must have its client send `null`, not an empty
string, for a blank value, or validation fails where the old form post silently passed. **The public
storefront is the one exception to the pattern**: `/services` and friends keep rendering full HTML
on the first request — that page exists specifically so Google can index it (see above), so only
what happens *after* that first paint (cart, checkout) moves to the API+JS pattern, via `api/cart`
and `ShopController.PlaceOrder`, never the initial render.

**The admin sidebar is a master, not markup.** `MenuItems` drives it, managed at
`/admin/masters/menu` by a super admin only. A row with no controller, action or URL is a group
heading; the rest are links, resolved with `Url.Action` so attribute routes keep working. On save
the action is checked against `IActionDescriptorCollectionProvider`, so an administrator cannot
create a dead link. Two levels deep, no more. `BadgeKey` names a counter the application knows how
to compute — adding one means a `MenuBadgeKeys` constant and a line in `_AdminLayout`.

**Profile is a section shell, not one page.** `/{portal}/profile` renders `_ProfileBody`, which draws
the left menu and switches on `ProfilePageViewModel.Section`. Sections: Profile, Post a task and My
tasks (customers only), Order history, Account details (bank), My addresses, My earnings and KYC
(both partners only), Manage devices — every role's own live sessions, backed by `RefreshTokens` —
and Settings, which is where password change lives. Adding one means a `ProfileSections` constant, a
row in the menu list, a partial, and a `GET` on `PortalControllerBase`. A portal supplies extra data
by overriding `LoadProfileExtrasAsync` — that is how the partner portal adds its KYC and the customer
portal adds its booking form. `SectionApplies` is the one place that decides which role sees what.

**`/` is a public storefront with a guest cart.** `ShopController` is `[AllowAnonymous]` — browsing,
adding to the cart and changing quantity all work signed out, and only placing the order needs an
account. A service item appears there only when it is active, its category is active,
`AllowsInstantBooking` is set **and** `BasePayout` is above zero; anything else has no price to show.

- **The cart holds ids and quantities, never prices.** A guest's cart lives in session
  (`gigapp_cart`) only; `ICartService.PriceAsync` looks every price up again, so a tampered cart
  cannot change a total. A line whose service was deactivated is dropped rather than failing the
  checkout.
- **Signed in as a customer, the cart also persists to `CartItem(CustomerId, ServiceItemId, Quantity)`.**
  Every `AddAsync`/`SetQuantityAsync` mirrors into it; `MergeIntoAccountAsync` folds the session cart
  into whatever the customer already had stored the moment they sign in, via the `onSuccess` hook on
  `PortalControllerBase.SignInAsync`. A guest who never logs in only ever has the session copy.
- **Checkout creates one task per cart line**, at the catalogue price, as `BookingMode = instant`,
  with `GigTask.Quantity` carrying the units so three of something stays one visit for one partner.
  Each line then enters the offer chain on its own, because lines can be different trades.
- Signing in mid-checkout keeps the cart — same session cookie.
- The old landing page with the dev credentials moved to `/welcome`.

**Storefront promotions are a master, not markup.** `Banners` is managed at `/admin/masters/banners`
with a placement of `spotlight` or `wide`, a required image, an optional date window and a link that
**must be local** — `Url.IsLocalUrl` is checked on save, so nobody can point the front page off-site.
Outside its window a banner is simply not rendered, and a section with nothing in it hides its own
heading rather than leaving an empty strip. The hero statistics stay hidden until there are five
ratings or twenty completed jobs, because a real number that small reads worse than none.

**The storefront is area-gated — a `ServiceZone` circle decides what a customer can even see.**
`ServiceZone` (centre point + `RadiusKm`, PostGIS geography, same shape as `Partner.BaseLocation`)
and `ServiceZoneCategory` (which `SkillCategory` rows are enabled in which zone) exist because not
every category launches in every area at once. `ShopController.SellableItems`/`BuildCategoriesAsync`
**fail closed**: no zone picked, or the picked zone doesn't enable that category, means the item does
not show — there is no "show everywhere" fallback, on purpose, so a caller that forgets to send a
zone gets nothing rather than an unfiltered catalogue. `GET api/servicezones` (list, for a manual
picker) and `GET api/servicezones/nearest?lat=&lng=` (auto-detect, 404 when the point falls outside
every zone's radius) are both `[AllowAnonymous]` — a guest has to be able to pick a location before
signing in. Web resolves the zone from a `gigapp_zone` cookie its own JS writes
(`wwwroot/js/global.js`'s `wireZonePicker`); `customer_app` always sends `zoneId` explicitly (no
cookie jar there) and persists the choice via `ZoneStorage`. Both open the same picker automatically
the first time and on "change location" — see
[docs/IMPLEMENTATION-LOG.md](docs/IMPLEMENTATION-LOG.md) for the full shape.

**`/services` is the only customer home page; the work lives in the profile.** There is no separate
`/customer` dashboard — `CustomerController.DashboardPath` overrides the base to `/services`, so login,
registration and the portal-home brand link all land on the storefront. `GET /customer` still exists
only as a redirect, for old bookmarks. Posting, tracking and history are profile sections, so every
task action redirects into the profile rather than back to the storefront.

- **My tasks is live work only**; completed and cancelled rows belong to Order history. The two lists
  never overlap.
- `SkillCategory.ImageFileName` is the tile artwork. No image falls back to the first letter, so the
  grid never has a hole in it.

**A support enquiry hangs off exactly one order.** Only the customer or the assigned partner may
raise one, one open enquiry per person per order, and `open → in_progress → resolved`. **Resolving
requires a reply** — it is the only thing the person who raised it sees. `/admin/enquiries` orders
open first and badges open plus in-progress, because both are still the support team's problem.

**KYC lives in the profile, and an unapproved partner sees almost nothing else.** `/provider` renders
only a greeting and the KYC status until `KycStatus == approved` — no available work, no bids. The
one exception is **My jobs**, which keeps showing work already accepted and not yet finished, so a
skill change cannot strand a customer mid-booking. The skill picker and document upload moved to
`/provider/profile/kyc`.

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

**PostgreSQL, not SQL Server — and the schema now depends on it.** The stack is Microsoft
everywhere else, so this was a deliberate choice, not an accident.

- **Licensing.** SQL Server Express caps a database at **10 GB**. `TrackingLogs` takes a row with a
  jsonb payload on every write, so it is the fastest-growing table in the system and would reach
  that cap first. Standard licensing costs several lakh rupees up front. Postgres costs nothing at
  any size.
- **PostGIS.** Matching a task to the nearest partner is the core algorithm, and this is where
  Postgres is genuinely ahead rather than merely cheaper. **Now in use**: `GigTasks.Location` and
  `Partners.BaseLocation` are `geography(Point, 4326)` columns with GIST indexes, and the partner's
  board filters and sorts on real metres. `GeoPoint.From(lat, lng)` is the only place a point is
  built — PostGIS takes longitude first, and swapping the pair puts Gurugram in the Indian Ocean.
- **jsonb.** `TrackingLogs.Payload` is a real jsonb column and can be indexed and queried inside.
  SQL Server stores JSON as `nvarchar`.
- **Functional indexes.** `UX_SkillCategories_Name_Lower` indexes `LOWER(Name)` directly. SQL Server
  needs a persisted computed column first.

**A migration back to SQL Server would break the schema, not just the provider.** Postgres allows
any number of NULLs in a unique index, which is what lets `(Email, Role)` coexist with phone-only
accounts. SQL Server treats NULLs as equal and permits only one, so every nullable unique index
here would have to become a filtered index.

What is given up: SSMS is better than any Postgres client, and Npgsql is a step behind the EF Core
SQL Server provider — the UTC `DateTime` rule under **Gotchas** is an example of that. Neither is
worth the licence.

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
- **`GET api/skillcategories` (the active-list action, not `/all`) is `[AllowAnonymous]`.** It only
  ever returns `{id, name}` for active categories — the same names the public storefront and the
  anonymous `/provider/register` page already show anyone, signed in or not — so this exposes
  nothing new. It exists so a mobile client with no anonymous Razor page to server-render options
  into (unlike the web KYC form) can still populate a skill picker before the visitor has an
  account. Every other action on the controller stays behind `[Authorize]`.
- **`ServiceItemsController` is plain `[Authorize]` at the class level, not admin-only.** A signed-in
  customer needs to browse real bookable items before posting a task, same as any other authenticated
  caller — only the write actions (`GetById`, `Save`, `Toggle`, `Delete`) keep their own
  `[Authorize(Policy = Policies.AdminOnly)]`, the same class-plain-plus-per-action-override shape
  `PartnersController` already used. `GET api/serviceitems/bookable?categoryId=` is the read a mobile
  client actually wants: only `IsActive` items in that category, never trusting a client-supplied
  `showInactive` the way the admin list does.

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

**`dotnet run` leaves the process alive after the terminal that started it closes** — closing a
terminal tab, or a Claude Code session ending, does not send it Ctrl+C. The next `dotnet run` then
fails with `Failed to bind to address http://127.0.0.1:5245: address already in use`, because the
old instance is still holding the port. Before starting the server, always free the port first:

```powershell
Get-NetTCPConnection -LocalPort 5245 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
```

This is more reliable than `Stop-Process -Name GigApp.Api`, which misses the parent `dotnet` process
when the app was launched via `dotnet run` rather than the built `.exe` directly.

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
nothing. Applies to partner registration, KYC upload and the profile photo. `_ImageUpload` is still a
file input underneath, so it does not save you from this.

**Binding a configuration array onto a non-empty default appends to it.** `FileStorageOptions.AllowedExtensions`
had `{ ".jpg", ".jpeg", ".png", ".webp" }` as its default and the same four in `appsettings.json`, so
it bound to eight entries and the error message listed each twice — and removing one from configuration
would have changed nothing. Any option that is a collection must default to empty, with the fallback
expressed as a separate property.

**Npgsql rejects non-UTC `DateTime` on `timestamp with time zone`.** JSON without an offset deserializes
as `Unspecified` and throws at save time. Run every client-supplied timestamp through
`DateTimeExtensions.ToUtc()`. Use `DateTime.UtcNow`, never `DateTime.Now`.

**Swagger only documents `api/*`.** A `DocInclusionPredicate` in `Program.cs` excludes the Razor
portals — Swashbuckle throws on any action without an explicit HTTP verb. Give every MVC action one anyway.

**Accepting a task must stay atomic.** `AcceptTask` uses a single conditional `ExecuteUpdateAsync`
(`WHERE Id = @id AND Status = 'pending'`) — the WHERE clause is the lock. Verified under 8 concurrent
requests: exactly one 200, seven 409s. Do not refactor this into read-then-write.

**A class-level `[AllowAnonymous]` silently defeats an action-level `[Authorize]` on the same
action** — ASP.NET Core does not let the more specific attribute win here, unlike two `[Authorize]`
attributes (which AND together). `ShopController.PlaceOrder` had `[Authorize(Policy =
Policies.CustomerOnly)]` sitting completely inert under the controller's own `[AllowAnonymous]`;
an anonymous request ran the action body and only failed inside `User.GetRequiredUserId()`. Any
controller that mixes public and authenticated actions must put `[AllowAnonymous]` on each public
action individually, never on the class, or every `[Authorize]` beneath it is dead code.

**`app.UseStaticFiles()` must come after `app.UseCors(...)`, not before.** A static file response
bypasses the rest of the pipeline once it matches, so anything registered after it — `UseCors`
included — never runs for that request. With the old order, every `/uploads/...` image came back
with no CORS header: invisible on the same-origin Razor pages, but it silently broke every image on
Flutter web's separate-origin dev server. Does not matter for a native Android/iOS build (no CORS
concept there), but breaks Chrome-based verification of anything that shows an uploaded image.

**`ICartService.Read()` must not trust the session alone.** A mobile client carries no session
cookie, only the bearer token, so a session-only read always came back empty for it even though
`SaveAsync` was already writing every change to the `CartItems` table. `Read()` now queries
`CartItems` directly for a signed-in customer and falls back to the session only for a guest — the
DB was always the correct answer for an authenticated caller, it just was never consulted on read.

**`Admin.formToJson(form)` sends a blank hidden `Id` input as `""`, and `[FromBody]` JSON binding
does not tolerate that for a nullable numeric property** — `int? Id` throws a hard `400` trying to
parse `""`, unlike classic form binding, which treats an empty value as absent. This has been
silently breaking `CommissionPlansController`'s "new plan" flow since it shipped (confirmed live:
`{"id":""}` returns 400). Any plain-JSON admin form with an optional `Id` must convert it explicitly
before posting — `payload.id = id ? parseInt(id, 10) : null` — rather than trusting `formToJson`'s
raw output; `ServiceZoneForm.cshtml` does this correctly and is the reference.

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

- A real SMS provider for OTP — `IOtpSender`/`OtpChallenge` are built and working, `LoggingOtpSender`
  just logs the code instead of texting it (DLT registration is the long-lead item ahead of this).
  Making the provider and its credentials admin-configurable (rather than one hardcoded `IOtpSender`)
  is a separate, real piece of work — providers don't share a credential shape, so it needs a
  provider picker plus per-provider fields, not one fixed settings form.
- `Jwt:Key` and the DB password are in config files; production must supply `Jwt__Key` via environment
- **`provider_app`** covers the working-partner loop end to end and is organised as a sectioned app
  — a bottom nav for the two screens opened constantly, a drawer for the rest: **Home** (greeting,
  duty toggle, two stat cards, a tap-through into Orders), **Orders** (three tabs — Available work /
  My bids / My jobs, the last filterable into Active/Completed/Cancelled — carrying every mutation:
  place/change/withdraw a bid, accept a counter, accept a fixed-price task,
  start/complete-with-rating/cancel a job), **Wallet** (balance, this-month/jobs-paid/commission
  stats, a paged transaction list) and **Profile** (identity with an edit option, KYC badge, bank
  account view/edit, change password, sign out). A bell in the AppBar opens **Notifications**
  (unread badge, paged list, mark-read) off the same `api/notifications/*` endpoints the web bell
  uses. A `RealtimeClient` connects to the same `/hubs/app` SignalR hub web uses — a `"tasks"`
  refresh live-updates the dashboard, a `"notification"` push shows a toast — so a partner sees a new
  job or a KYC decision without pulling to refresh, as long as the app is open (a fully closed app
  needs Firebase to be woken up at all; see [docs/IMPLEMENTATION-LOG.md](docs/IMPLEMENTATION-LOG.md)
  for why that is an OS constraint, not a missing feature, and is deliberately not built yet).
  **An unverified partner (`pending`/`rejected` KYC) sees nothing but a `KycStatusScreen`** — status,
  rejection reason, a resubmit form, sign out — the shell never builds the drawer or bottom nav at
  all in that state. This is stricter than the web portal, which keeps "My jobs" visible for an
  unverified partner so a mid-booking skill change can't strand a customer; mobile has no such
  exception, by explicit request. Sign-in covers OTP, password fallback, session restore and
  refresh-on-401; partner KYC registration handles an unknown number. Profile's Settings also has
  **Service area** (base location via GPS or typed city/pincode, plus travel radius — `geolocator`,
  not a map picker) and **Manage devices** (list, revoke one, "sign out others" — the last needed a
  small backend fix, `DevicesController` now accepts an `X-Refresh-Token` header as the mobile
  equivalent of the web's `gigapp_refresh` cookie for identifying "this device", since without it a
  mobile "sign out others" would have signed itself out too). **Home** also shows the personal
  first-refusal **offer widget** — a live countdown card with Accept/Pass, off the same `Offer`
  field `GET api/partners/me/dashboard` already returned. Every one of these — including Wallet,
  Notifications, profile editing, service area and devices — calls an endpoint the web portals
  already had, bar that one small devices header. Not built yet: real push notifications for a
  fully closed app (needs a Firebase project — see
  [docs/IMPLEMENTATION-LOG.md](docs/IMPLEMENTATION-LOG.md) for why that's an OS constraint the app
  can't route around) — see [docs/REUSABLE.md](docs/REUSABLE.md) for the pattern the next slice
  follows.
- **`customer_app` now covers the full booking loop**, same clean-architecture shape as
  `provider_app`: sign-in (OTP with inline name for a new account, password fallback, session
  restore), a three-tab Active/Completed/Cancelled order history with rate-partner, raise-help and
  cancel actions, live SignalR updates and in-app notifications, and every profile section — edit
  profile, bank account, addresses (CRUD + set-default), manage devices, change password.
  `provider_app`'s notification/device/realtime layer and profile dialogs were copied over unchanged,
  confirming that shape is portal-agnostic.
- **Home is now the storefront**, matching the web `/services` page rather than a bare "post a task"
  form: banners, category tiles, popular/new-and-noteworthy strips and per-category strips, each
  service card with a live add-to-cart quantity stepper, off three new JSON siblings of the existing
  Razor storefront actions — `GET api/shop/home`, `api/shop/category/{id}`, `api/shop/search`. Tapping
  a category opens the same grid in `CategoryScreen`; the AppBar's search field opens `SearchScreen`
  with a debounced live search. The old single-item "post a task" flow (category → item → description
  → budget → urgency → address, for a bidding item that has no catalogue price) still exists as a
  secondary "Need something else?" card on Home, since the storefront only ever lists fixed-price
  catalogue items — see `SellableItems()` on `ShopController`.
- **Cart and checkout reuse the same session-cart backend the web storefront already had** —
  `api/cart/*` and `POST /checkout` — called directly over the bearer token, no mobile-specific
  endpoint. One shared `CartController` (`presentation/cart/cart_controller.dart`,
  `cartControllerProvider`) backs the AppBar's cart badge, every add-to-cart stepper, and the Cart
  screen itself, so a quantity change on Home is instantly reflected everywhere else without any
  manual refresh. `CartService.Read()` had to change for this to work at all — see Gotchas.
- **The whole storefront is area-gated, signed in or not.** A fresh session (`app.dart`'s
  `_PostLoginGate`) always routes to `LocationPickerScreen` until `zoneControllerProvider` holds a
  zone — GPS auto-detect via `geolocator` (`ZoneController.useCurrentLocation`, resolves through
  `GET api/servicezones/nearest`) or a manual pick from `GET api/servicezones`. The choice persists
  in secure storage (`ZoneStorage`) and is sent as `zoneId` on every storefront/category/search/
  bookable-items call; Home's "Delivering to <zone>" row reopens the picker to change it. See
  CLAUDE.md's storefront architecture note and [docs/IMPLEMENTATION-LOG.md](docs/IMPLEMENTATION-LOG.md)
  for why the backend fails closed with no zone rather than showing everything.
  Not built yet: Firebase push notifications for when the app is fully closed (deferred pending the
  user's own Firebase account setup).
- `admin_panel` stays the untouched `flutter create` template permanently — it is slated for
  deletion, replaced by the Razor `/admin` portal.
