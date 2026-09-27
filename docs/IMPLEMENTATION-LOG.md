# Implementation log

Business rules that were decided in conversation and are **not obvious from the code**.
The Flutter apps have to reproduce every rule here, so this file is the contract between the
web portals and mobile. Newest entry first.

Each entry says: what was asked, the rule as implemented, and where it lives so a mobile
developer can call the same thing.

---

## 2026-09-27 — The storefront becomes area-gated, on web and mobile alike

**Asked:** limit which services show by the customer's own area — some services cannot be delivered
everywhere yet — with location taken either manually (from a list of areas we serve) or auto-detected
from the device's GPS, on both the website and `customer_app`.

**A `ServiceZone` is a circle, not a polygon or a pincode list** — a centre point plus a radius in
km, the same shape `Partner.BaseLocation`/`ServiceRadiusKm` already used for partner service areas
(`Services/Geo/GeoPoint.cs`, PostGIS `geography` distance). This was chosen over a free-text Google
Places search specifically to avoid the Places API's cost and a new SDK dependency on three
surfaces — the admin only has to draw a circle, not a service-area polygon editor.
`ServiceZoneCategory` is the join: which `SkillCategory` rows are enabled inside which zone. A
category can be live in Gurugram and not yet in a new city — that decision lives entirely in this
table, nothing in code branches on a city name.

**The storefront fails closed, not open, when no zone is known.** `ShopController.SellableItems`/
`BuildCategoriesAsync` require `zoneId != null` **and** a matching `ServiceZoneCategories` row —
with no zone picked, every storefront query returns empty rather than falling back to "show
everywhere." This was a deliberate reversal of the obvious first draft (`zoneId == null || ...`,
which fails *open* and defeats the entire feature for any caller that forgets to send a zone) — the
early version of this change had exactly that bug caught before it shipped.

**`GET api/servicezones/nearest?lat=&lng=`** is the auto-detect endpoint: nearest zone whose circle
the point actually falls inside, `404` with `"We do not serve this area yet."` if none does — being
near a zone is not enough, the point has to be inside its radius. `GET api/servicezones` is the
plain `{id, name}` list the manual picker shows. Both `[AllowAnonymous]`, same reasoning as
`api/skillcategories`'s active list: nothing here is sensitive, and a guest has to be able to pick a
location before signing in.

**Web resolves the zone from a cookie, mobile always sends it explicitly.** `ShopController.
ResolveZoneId` takes an explicit `?zoneId=` query param first and falls back to the `gigapp_zone`
cookie — the same explicit-param-wins-cookie-or-session-is-fallback shape `CartService.Read()`
already uses for the same reason (a mobile client has no cookie jar). The web picker
(`wwwroot/js/global.js`'s `wireZonePicker`, `_ZonePicker.cshtml`) writes `gigapp_zone` (id) and
`gigapp_zone_name` (display) as year-long cookies and reloads the page; `customer_app` persists the
same pair via `ZoneStorage` (secure storage) and sends `zoneId` on every storefront/category/search/
bookable-items call once picked. Both surfaces open the exact same picker automatically the first
time — no cookie yet on web, `zoneControllerProvider`'s state is `null` on mobile — and reopen it on
"change location," which on mobile is just resetting the stored zone (`app.dart`'s `_PostLoginGate`
already routes to the picker whenever the zone is null, so clearing it is the whole navigation).

**The old single-item "post a task" flow (bidding, not the fixed-price catalogue) is zone-aware too,
optionally.** `GET api/serviceitems/bookable` takes an optional `zoneId` — filtered when sent,
unfiltered when not, so it stays backward compatible with any caller that predates area gating.
`customer_app`'s `PostTaskScreen` always sends the current zone once one exists.

**A pre-existing bug, found while building this**: `Admin.formToJson(form)` posts a hidden `Id`
input's value as `""` for a brand-new record, and ASP.NET Core's `[FromBody]` JSON binder throws a
hard `400` trying to convert `""` into `int?` — unlike classic form binding, which happily treats an
empty value as absent. `CommissionPlansController`'s "new plan" flow has carried this bug since it
shipped (confirmed live with a direct API call). The new `ServiceZoneForm.cshtml` avoids it by
setting `payload.id = id ? parseInt(id, 10) : null` before posting rather than trusting
`formToJson`'s raw output — any other plain-JSON (non-`FormData`) admin form with an optional `Id`
should do the same until `formToJson` itself is fixed to omit blank fields.

---

## 2026-09-15 — `customer_app`'s Home becomes the storefront, with a real cart and checkout

**Asked:** make `customer_app`'s Home page match the web storefront — banners, categories, popular
and new-and-noteworthy strips, search — with a working cart and checkout, the same "book now, pay
after the job" flow the web storefront already uses (there is no separate payment gateway on either
side; "payment" here means the existing catalogue-to-booking flow, not a new Razorpay/UPI integration).

**Three new JSON endpoints, mirroring the exact ViewModel the Razor storefront already builds** —
`ShopController.Index`/`Category`/`SearchAsync` were each split into a private `BuildXxxAsync`
returning the ViewModel and a thin action calling `View(vm)`; `GET /api/shop/home`, `GET
/api/shop/category/{id}` and `GET /api/shop/search?q=` call the same builders and `Ok(vm)` instead.
Zero new business logic — the storefront's categories, banners, popular/fresh picks and search were
already exactly the shape a mobile client needed, just never returned as JSON. `POST /checkout`
(`ShopController.PlaceOrder`) is called directly from mobile too, over the bearer token — no separate
mobile checkout endpoint, since JWT auth doesn't care whether the caller is a browser or an app.

**A real, pre-existing bug: `PlaceOrder`'s `[Authorize(Policy = Policies.CustomerOnly)]` was not
actually enforced.** `ShopController` carried a class-level `[AllowAnonymous]` (correct for browsing
and the cart, which must work signed out) and `PlaceOrder` relied on an action-level `[Authorize]` to
require a customer — but ASP.NET Core lets a class-level `[AllowAnonymous]` silently defeat an
action-level `[Authorize]` on the same action; it does not compose the other way round. Verified live:
an anonymous `POST /checkout` executed the action body and only failed inside `User.GetRequiredUserId()`,
landing on the generic error page instead of a clean 401. Fixed by removing `[AllowAnonymous]` from the
class and adding it individually to every action except `PlaceOrder` — the same class-plain,
action-tightens shape `ServiceItemsController`/`PartnersController` already use, just inverted. Anyone
touching this controller again: a class-level `[AllowAnonymous]` blocks every `[Authorize]` beneath it,
full stop — put `[AllowAnonymous]` on individual actions instead when only some of a controller's
actions are public.

**`CartService.Read()` only ever checked the session — which meant it always came back empty for
mobile**, since a mobile client carries no session cookie, only the bearer token; `SaveAsync` already
wrote every change to the `CartItems` table too, so the DB side was fine, it just was never read back.
Fixed by having `Read()` query `CartItems` directly for a signed-in customer, falling back to the
session only for a guest. This is not a second source of truth to keep in sync — `SaveAsync` already
mirrors every write to both places — just a switch in which one is authoritative on read. Verified over
a bearer-token-only `curl` session (no cookie jar) that add → read-back → checkout round-trips
correctly, exactly the shape mobile uses.

**`app.UseStaticFiles()` ran before `app.UseCors(...)` in `Program.cs`**, so an uploaded category,
service or banner image never got a CORS header — invisible on the same-origin Razor pages, but it
silently broke every image on Flutter web's separate-origin dev server (`ERR_FAILED` / CORS console
errors, `Image.network`'s `errorBuilder` masking it as a plain placeholder icon with no visible error
in the UI). Static files bypass the rest of the pipeline once they match, so anything after them —
including `UseCors` — never runs for that request. Fixed by moving `UseStaticFiles()` to after
`UseCors()`. Does not affect a real Android/iOS build at all (native HTTP has no CORS concept), but
would have made every future Chrome-based verification pass on this app quietly show broken images.

**Mobile's storefront reuses `ServiceItem`/`SkillCategory`-shaped JSON but not the existing Flutter
entities** — `provider_app`'s `SkillCategory` is a bare `{id, name}` for a dropdown, too thin for a
storefront tile (needs an image, a service count, a starting price). New `CatalogCategory`,
`CategoryStrip`, `CatalogBanner`, `StorefrontStats`, `StorefrontHome` and `Cart`/`CartLine` entities
were added rather than bloating the dropdown-only entity with fields it never needs. `resolveImageUrl`
(`core/utils/image_url.dart`) prefixes every relative `imageUrl` the API returns with `Env.apiBaseUrl`
— the API sends `/uploads/...`, never a full URL, since it does not know its own public host.

**One shared `CartController` (`presentation/cart/cart_controller.dart`), not a quantity field per
screen.** `StateNotifierProvider.autoDispose<CartController, AsyncValue<Cart>>` — `AddToCartControl`
(the +/- stepper, reused on Home's strips, Category, and the Cart screen itself) and `CartButton` (the
AppBar badge) all read the same provider, so adding an item on Home instantly updates the Cart screen
and the badge without any manual refresh wiring. A mutation method (`add`/`setQuantity`/`remove`) lets
its exception propagate rather than writing it into `state` — the widget's own try/catch shows a
SnackBar, and the cart's last-known-good data stays on screen instead of being replaced by an error.

**Verified live end-to-end in Chrome, both desktop and mobile (375×812) viewport widths**: Home's
banners/categories/strips render with real images and no layout overflow, add-to-cart quantity steppers
sync live across Home → Category → Cart, checkout with an existing saved address places a real booking
that immediately shows up in Orders' Active tab, and the cart clears after a successful checkout.

---

## 2026-09-15 — `customer_app` goes from read-only "My tasks" to the full booking loop

**Asked:** advance `customer_app` to cover posting a task, order history, and every profile section
the customer portal has — addresses, bank account, manage devices, change password — the same depth
`provider_app` already reached.

**A customer needed a way to browse real bookable services, and none existed for mobile.**
`ServiceItemsController` was class-level `[Authorize(Policy = Policies.AdminOnly)]`, so a signed-in
customer could not call it at all. Changed to plain `[Authorize]` at the class level with
`[Authorize(Policy = Policies.AdminOnly)]` restored individually on `GetById`, `Save`, `Toggle` and
`Delete` — the same override shape `PartnersController` already used, not a new pattern. Added `GET
api/serviceitems/bookable?categoryId=`, returning only `IsActive` items in that category — never
trusting a client-supplied `showInactive` the way the admin list does. `PostTaskScreen`
(`presentation/booking/post_task_screen.dart`) calls it after a category is picked, shows the fixed
price for an instant-booking item or an editable budget field for a bidding one, and posts through
the existing `POST api/gigtasks` — no new create-task business rule, `BookingMode` is still decided
server-side exactly as CLAUDE.md describes.

**Order history reuses the customer portal's own paged endpoint**, `GET api/profile/orders` — not
`GET api/gigtasks/my`, which only ever returns live work. Its `ratings`/`enquiries` come back as a
JSON object keyed by task id as a **string** (`Dictionary<int,V>` serializes that way), so
`task_repository_impl.dart` runs every key through `int.parse` before matching it to a task — anyone
adding a third such dictionary-shaped field should expect the same string-keyed object, not an
array.

**`OrderHistoryController` is a deliberate exception to the shared `PagedListController<T>`.**
Wallet and Notifications only ever need `items`/`page`/`hasNext` — nothing else about a ledger entry
changes after it's fetched. An order's rating and enquiry status *can* change from an action taken on
the very same screen (rating a completed job, cancelling a pending one), so
`presentation/orders/order_history_controller.dart` is its own small non-generic state
(`orders`/`page`/`hasNext`/`ratings`/`enquiries`) with `replaceTask`/`putRating`/`putEnquiry`
mutators the UI calls straight after a successful action, instead of refetching the whole page. Reach
for the generic controller by default; write a bespoke one only when a screen's list needs
supplementary per-item state the generic shape has nowhere to hold.

**Every profile section, notification, and device-management screen already built for
`provider_app` copied over unchanged** — `AppNotification`/`DeviceSession` entities and their
data layers, `NotificationsScreen`, `DevicesScreen`, `RealtimeClient`,
`presentation/common/paged_list_controller.dart`, and the `bank_account_dialog.dart` /
`change_password_dialog.dart` / `edit_profile_dialog.dart` trio — proving the `provider_app` shape
really is portal-agnostic, not partner-specific. `ProfileRepository` picked up one addition
`provider_app` never needed, `getBankAccount()` (`GET api/profile/bank`), since a partner's bank
details live under KYC/earnings while a customer's exist purely for refunds.

**The customer shell stays deliberately simpler than the partner one** — three bottom-nav sections
(Home, Orders, Profile), no drawer, matching CLAUDE.md's own reasoning that a partner's feature list
grows over time while a customer's three jobs (browse, track, manage) do not.

Verified live in Chrome end-to-end: post a task (category → real bookable item → address → submit),
the new task appearing correctly in Home and in Orders' Active tab, rating a partner, raising a help
enquiry, cancelling a pending order (moves it into the Cancelled tab), editing the profile, saving a
bank account, adding/editing/deleting/setting-default on an address, and manage devices' single and
bulk revoke (including "this device" staying signed in after "sign out others" — the same
`X-Refresh-Token` header fix `provider_app` needed applies here too, since both apps share
`DeviceRemoteDataSource`'s shape).

---

## 2026-09-15 — The remaining `provider_app` backlog: offer widget, service area, manage devices

**Asked:** the three items left on the mobile backlog — the personal first-refusal "offer" countdown
card, a partner's own service area (base location + radius), and manage devices.

**Personal offer widget reuses a field the dashboard already returned.**
`ProviderDashboardDto.Offer` (`IOfferService.LiveOfferForPartnerAsync`) was already part of `GET
api/partners/me/dashboard`'s response — only the mobile side had never parsed it. `OfferBanner`
(`presentation/home/widgets/offer_banner.dart`) shows on Home whenever `offer.isLive`, with a
client-side `Timer.periodic` countdown computed from `expiresAt` (no server polling needed for the
ticking itself) and Accept/Pass buttons calling the existing `POST api/offers/{id}/respond` — when
the countdown reaches zero the widget just invalidates `dashboardProvider`, since `OfferExpiryWorker`
on the backend has already moved the job on by then anyway.

**Service area, not a general address book.** The request was specifically "let a partner set the
area they work in," which is the existing `GET/PUT api/partners/me/service-area` endpoint (base
lat/lng, radius, city, pincode — already built for the web portal), not the separate
`api/addresses` CRUD that stores a customer's multiple named addresses. `ServiceAreaScreen`
(`presentation/settings/service_area_screen.dart`) adds a "Use my current location" button backed
by the `geolocator` package (new dependency; `ACCESS_FINE_LOCATION`/`ACCESS_COARSE_LOCATION` added
to `AndroidManifest.xml`) rather than a map picker — asking for a full interactive map (and the API
key that comes with it) was more than the request called for when "where I'm standing right now" is
what a partner actually wants most of the time. Denied or unavailable location fails into a plain
error message; the radius/city/pincode fields still work without a pin.

**Manage devices needed one small, deliberate backend change — `DevicesController.cs`.** Both
`GET api/devices` and `POST api/devices/revoke-others` decide "which row is this caller's own
device" by reading the `gigapp_refresh` cookie — which only the web portals have. A mobile client
calling either endpoint got `currentToken == null`, which made every device (including its own)
look like "somebody else's" — concretely, `RevokeAllExceptAsync` with a null current-token excludes
nothing, so "sign out other devices" from mobile would have silently signed out the calling device
too. Fixed by having both actions accept an optional `X-Refresh-Token` header and fall back to it
when the cookie is absent (`DevicesController.CurrentRefreshToken`) — the mobile client
(`data/datasources/device_remote_data_source.dart`) sends its own stored refresh token through that
header on exactly these two calls, nowhere else. Verified live: the calling browser session showed
up correctly labelled "This device," survived its own "sign out other devices," and an individual
device's own revoke button worked independently.

---

## 2026-09-15 — `provider_app` gets live notifications, SignalR, and strict KYC gating

**Asked:** three things — (1) a notification bell/list on mobile for the same events the web bell
already shows (task assigned, KYC approved/rejected); (2) live updates instead of pull-to-refresh,
with a preference for reusing the existing SignalR hub over a Firebase-only approach where possible;
(3) an unverified partner (`pending` or `rejected` KYC) must see **only** the KYC status and a
resubmit form — no Home stats, no Orders, no Wallet, no Profile, nothing else, not even the "My
jobs" exception the web portal keeps for already-accepted work.

**SignalR client, not just a REST poll.** `core/realtime/realtime_client.dart` wraps a
`signalr_netcore` `HubConnection` to the same `/hubs/app` the web portals use, authenticating via
`accessTokenFactory` reading the stored access token (the hub already reads `access_token` off the
query string for any `/hubs` path — see CLAUDE.md — so this needed zero backend changes).
`AppShell` connects once per session (`initState`, `realtimeClientProvider` is `.autoDispose` so
signing out — which unmounts `AppShell` — disconnects it too) and reacts to both server events
exactly like `global.js` does: a `"refresh"` topic of `"tasks"` invalidates `dashboardProvider`
(live job/bid/offer updates, no manual refresh needed while the app is open), and a
`"notification"` event invalidates the summary and pushes a `SnackBar` toast via a
`notificationToastProvider` StateProvider (kept separate from the client itself, since the client
lives in a plain `Provider` with no `BuildContext` to show UI from directly).

**In-app notifications reuse the same endpoints the web bell already called** —
`GET api/notifications/summary` (badge count + AppBar `NotificationBell`) and `GET
api/notifications` (paged, `NotificationsScreen`) — no new backend reads. `POST
api/notifications/read` marks one or all as read.

**True push when the app is fully closed still needs Firebase — this was explained to the user,
not silently deferred.** A WebSocket/SignalR connection only survives while the process is alive;
Android (and iOS) kill a backgrounded app's socket after a short window unless a foreground service
keeps it alive (the same reason Rapido/Uber/Ola show a persistent "you're online" notification).
SignalR-over-foreground-service is one path (no Firebase, but a persistent notification icon and
extra battery use while on duty); Firebase Cloud Messaging is the other (works even fully closed,
but needs a Firebase project the user has to create). Both are follow-up work, not part of this
pass — what shipped here is real-time delivery *while the app is open or freshly backgrounded*,
which covers the common case without new external dependencies.

**KYC gating moved from "Home shows less" to "the shell renders a completely different screen."**
`AppShell` now watches `dashboardProvider` itself (previously each child screen watched it
independently) and branches before ever building the drawer, bottom nav, or any of the four
sections: `!profile.isVerified` renders only `KycStatusScreen` — status card (with the rejection
reason when rejected), a resubmit form reusing the same `ImagePickerTile` from registration, and a
sign-out button in the AppBar (the only other action available). This is a **deliberate departure
from the web portal's own rule** (`/provider` keeps "My jobs" visible for an unverified partner so
a skill change can't strand a customer mid-booking) — the user explicitly asked for zero exceptions
on mobile. Worth revisiting if a partner ever gets sent back to `pending` while holding an
in-progress job, since right now they'd lose visibility into finishing it from the app.

**A real Dart generics bug, caught by live testing, not static analysis.** The paginated-list
controller shared by Wallet and Notifications (`presentation/common/paged_list_controller.dart`)
originally defaulted `PagedState.items` to `const []` in the constructor. In a generic class, a
`const []` default is typed `List<Never>`, not `List<T>` — `flutter analyze` never flags it, but
DDC (the web compiler) enforces the mismatch strictly at runtime and threw inside `copyWith` on
every single fetch, silently swallowed by the `try/catch` into `state.error`, which neither screen
rendered — so real API data (verified via the network tab) rendered as an empty "nothing here yet."
Fixed with `PagedState.initial()`, a factory that builds `<T>[]` at a call site where `T` is bound.
Both screens now also render `state.error` distinctly from a genuinely empty list, so a future bug
like this fails loudly instead of looking like empty data.

---

## 2026-09-15 — `provider_app` split into a sectioned navigation shell: Home, Orders, Wallet, Profile

**Asked:** the single flat dashboard screen crammed available work, bids and jobs into one long
scroll — fine for testing with a handful of rows, but the request was to organise it the way a
consumer ride-hailing partner app does (Rapido was the named reference) before order volume grows,
since a partner is not assumed to be technical and needs each feature to open from one obvious
place rather than be found by scrolling.

**Bottom nav for the two screens opened constantly, a drawer for the rest.** The user's first
description suggested a drawer for everything; once they shared their actual reference app's own
screens, the real pattern turned out to be a 2-item bottom `NavigationBar` (Home, Orders — the ones
a partner taps all day) plus a hamburger drawer for the occasional ones (Wallet, Profile, sign out).
`AppShell` (`lib/presentation/shell/app_shell.dart`) still holds one `IndexedStack` switched by
`shellSectionProvider` regardless of which control moved it — a tab keeps its scroll position and
in-flight state whether it was reached from the bottom bar or the drawer.

- **Home** — greeting, the duty toggle, two stat cards (active jobs, this month's earning) and a
  single tappable card into Orders' Available tab. The old dashboard's full available-work list is
  gone from here on purpose — Home is a summary, not a worklist.
- **Orders** — one screen, three tabs (Available / My bids / My jobs) instead of three separate
  entries, since these are one job's states, not three unrelated features. All the action logic
  that used to live in `dashboard_screen.dart` (accept, bid, withdraw, accept-counter, start,
  complete, cancel) moved here unchanged — same `DashboardActionsController`, same dialogs, same
  confirm copy. My jobs additionally filters client-side into Active / Completed / Cancelled chips,
  matching the reference app's own order-history split — no new endpoint, `GigTask.status` already
  carries everything needed.
- **Wallet** — new screen, new to mobile but not to the backend: `GET api/partners/me/earnings`
  (summary + bank account) and `GET api/partners/me/earnings/entries` (paged ledger) already existed
  for the web partner portal's own earnings page and needed no backend change. A balance card up
  top, a paged transaction list below with a manual "Load more" (`LedgerController`,
  `StateNotifier`-based, appends pages rather than replacing them).
- **Profile** — identity (name, phone, rating, skill, KYC badge) with an edit option
  (`PUT api/profile`, the same shared endpoint every portal uses — the response replaces the
  session's `AppUser` via `SessionController.setSignedIn` so the header updates immediately, no
  re-login needed), bank account (view + edit through `PUT api/profile/bank`) and settings (change
  password via `POST api/profile/password`, sign out).

**Deliberately not chasing every reference-app screen.** The shared screenshots also showed rate
cards, subscription plans, a demand planner and a referral program — all real features of that
product, none of them a GigApp business rule that exists anywhere in this backend. Building UI for
numbers no endpoint produces would be fabricating a feature, not porting one; the navigation *shape*
was the transferable idea, not each individual widget. My addresses and manage devices are also
still not on mobile — deferred, not missed.

**Nothing new on the backend.** Every endpoint this pass calls already existed for the web portals;
the gap was entirely that mobile had never parsed, rendered or (for profile/bank/password) written
what those endpoints already supported.

---

## 2026-09-14 — The working-partner loop on mobile: available work, bids, jobs, duty toggle

**Asked:** "whatever the partner website does, build it in the same format on the app" — bring
`provider_app` up to feature parity with the web dashboard's actual working loop, design left to
judgement for anything not explicitly the web's own shape.

**Reused the existing endpoint, added no new reads.** `GET api/partners/me/dashboard` already
returned full `availableTasks`/`myBids`/`myJobs` lists — the earlier mobile pass only parsed their
`.length` for the summary counts. This pass parses the full lists into real `GigTask`/`Bid`
entities and renders them, so the only backend-visible change is the client asking for data that
was already in the response.

**Actions match the web's, one call each, same as `Views/Provider/Index.cshtml`'s script:**
`PUT api/gigtasks/{id}/accept` (fixed-price claim), `POST api/bids/task/{id}` (place or change a
bid — same endpoint for both, an existing open bid on that task is simply replaced),
`POST api/bids/{id}/withdraw`, `POST api/bids/{id}/accept-counter`,
`PUT api/gigtasks/{id}/status` (`in_progress` / `completed` with required `stars` (1–5) and optional
feedback / `cancelled` with an optional reason), and `PUT api/partners/me/availability`. Every
mutation runs through one `DashboardActionsController` that shows a busy state, invalidates
`dashboardProvider` on success so the list re-fetches itself, and surfaces the server's error
message in a red snackbar on failure — the same "re-run the same load()" shape used for SignalR on
web, just triggered by the client's own action instead of a push.

**Design departed from the web on purpose where mobile has a better native answer, not because the
business rule differs:** cards instead of tables (`AvailableTaskCard`/`MyBidCard`/`MyJobCard`), and
Material `AlertDialog`s instead of Bootstrap modals for the three multi-field prompts (bid
amount+note, complete's star rating+feedback, cancel's reason) plus a shared `showConfirmDialog` for
the plain yes/no ones (take a fixed-price job, start, withdraw, accept a counter) — same copy as the
web's `App.confirmAction` calls in every case, so the *rule* being confirmed reads identically even
though the widget doesn't.

**Not brought over in this pass, on purpose:** the dashboard's personal "this job is yours first"
offer card with its own accept/pass buttons and countdown (`IOfferService`'s first-refusal chain,
`POST api/offers/{id}/respond`) — a fixed-price job a partner is offered this way still shows up in
the plain Available-work list once the offer window lapses (or immediately, if
`Platform:AutoAssignInstant` is off), so nothing is unreachable, it just is not yet surfaced with
its own priority card and countdown. Earnings, notifications, SignalR live refresh (the web dash
still needs a manual pull-to-refresh on mobile for now), manage devices and settings are separate,
undone slices.

---

## 2026-09-14 — Partner KYC registration on mobile, `customer_app` started, and a real
`flutter_secure_storage` web bug found and fixed

**Asked:** continue the mobile work — build partner KYC registration (the piece the previous pass
left as a placeholder screen) and start `customer_app` on the same pattern.

**Partner KYC registration (`provider_app`)** mirrors `/provider/register` exactly, since the
business rule is the same regardless of client: name, optional email, a skill category (now fetched
from `GET api/skillcategories`, made `[AllowAnonymous]` for this — see CLAUDE.md), a selfie and both
sides of the Aadhaar card via `image_picker` (camera or gallery, offered in a bottom sheet — the
same "choose another way" shape as the OTP "More options" sheet), and an Aadhaar number. The
password field the web hides and auto-fills is skipped entirely on mobile — the repository
generates one (32 random hex characters + a fixed suffix guaranteeing all four character classes
`ValidationPatterns.Password` requires) and the UI never sees it. Posts multipart to
`api/auth/register/partner`, which is unchanged apart from also accepting `phoneVerifiedViaOtp`
(new — see below) and returns the same `AuthResponse` shape login does, so registering signs the
partner straight in with no separate login step.

**`AuthController.RegisterPartner` gained a `phoneVerifiedViaOtp` form field.** Without it, a
partner registering through the mobile OTP flow would have their phone marked unverified forever —
the flag-setting `ExecuteUpdateAsync` only ever existed on `ProviderController.Register` (the portal
MVC action), which mobile does not call. Mirrors the exact same field and the exact same
after-the-fact `ExecuteUpdateAsync` the portal action already used, so the same known staleness
applies: the `AuthResponse.User.IsPhoneVerified` the registration call itself returns still reads
`false` for one call, until the next `me`/session-restore.

**`customer_app` started** on the identical clean-architecture shape as `provider_app` — same
`core/network`/`core/storage` almost verbatim (deliberately duplicated rather than extracted into a
shared package this early; see docs/REUSABLE.md). Sign-in has no KYC branch — an unknown customer
number auto-registers inline, so the `code` step carries an always-optional "Your name" field
instead of routing anywhere — and the first authenticated screen is a "My tasks" list off the
existing `GET api/gigtasks/my` (no new endpoint needed; it already returns either side's own tasks
depending on the caller's role).

**Found and fixed a real concurrency bug in `flutter_secure_storage`'s web backend**, not a bug in
this app's own code: `flutter_secure_storage_web`'s `_getEncryptionKey` does a check-then-create on
its AES-GCM wrapping key against `localStorage` with no locking. `TokenStorage.save()` was writing
its four keys with `Future.wait` (concurrently) — under concurrency, more than one write can see "no
key yet" at once, each generate a *different* wrapping key, and each write its own key to
`localStorage`; whichever write lands last silently wins, leaving the other values permanently
encrypted under a key that no longer exists anywhere. The next `read()` then throws a WebCrypto
`OperationError` **forever** for those values — no retry count fixes it, because the corruption is
in the ciphertext itself, written once. Fix: `save()` now `await`s each of the four writes in
sequence, so only the very first write in the app's lifetime ever hits the "no key yet" branch and
every write after it — same call or a later one — reuses the one key already in `localStorage`.
Applied to both `provider_app` and `customer_app`. Native Keychain/Keystore never had this problem —
this was a web-target-only bug, but a real one, not a testing artifact to shrug off.

---

## 2026-09-13 — Mobile-facing OTP endpoints, and the provider Flutter app started

**Asked:** start real work on the mobile apps. `customer_app`/`provider_app`/`admin_panel` were
still the untouched `flutter create` template, and CLAUDE.md already says only the partner portal
is meant to be replaced by an app — the customer web portal stays permanent for SEO — so this pass
targets `provider_app` first: clean architecture (`data`/`domain`/`presentation`), Riverpod, a typed
Dio client with a single auth interceptor, and the OTP-first sign-in flow the web portals already
use, ending on a real dashboard screen fed by `GET api/partners/me/dashboard`.

**Gap found and closed first: OTP had no JSON-only entry point.** `otp/request`/`otp/verify` existed
only on `ProviderController`/`CustomerController`, and both end by setting the `gigapp_token`
cookie — nothing a mobile client can use. Added `POST api/auth/otp/request` and
`POST api/auth/otp/verify` to `AuthController`, taking an explicit `Role` (a portal controller
already knows its own; a shared API controller does not) and returning the same `AuthResponse`
shape login does — or `{requiresPartnerRegistration:true}` for a partner number with no account
yet, mirroring the portal's own redirect-to-KYC branch. Both are thin wrappers over the existing
`IOtpService`; no OTP business rule was written twice.

**Provider app, phase 1 — sign-in only, verified against the real API:**
- Phone entry → code entry (OTP) → dashboard, and "More options" → inline password step, matching
  the web's own flow pixel-for-pixel in intent even though the widgets are native.
- An unknown partner number gets a "finish your registration" placeholder screen for now — the real
  KYC form (skill picker, selfie + Aadhaar photo upload) is real, separate scope, not done in this
  pass.
- Tokens live in `flutter_secure_storage`; a single Dio interceptor attaches the bearer token,
  catches a `401`, redeems the refresh token via `api/auth/refresh`, and retries the original
  request once — the same rotate-on-redeem contract `RefreshTokenService` already enforces
  server-side, so a stolen mobile refresh token dies the same way a stolen web one does.

**Not done in this pass:** partner KYC registration, the dashboard's real feature set (jobs, bids,
earnings, manage devices, SignalR realtime), and `customer_app`. Those are the next slices, in that
order, once sign-in itself has been used for real rather than re-decided each time.

---

## 2026-09-13 — "More options" is a modal on the code step, not a link to a second page

**Asked:** the user recorded Uber's own web login flow as a reference — phone/email entry, then a
code-entry step whose "More options" opens a small dialog ("Choose another way to verify") listing
alternate ways in, rather than sending the visitor to a separate page. Picking "Password" there
swaps the same step in place to a password field, pre-filled with the number already entered.
Clarified over chat: GigApp does not have a real SMS/WhatsApp/email OTP channel yet, so today the
dialog should offer exactly one entry, "Sign in with password" — further delivery channels get
added to the same dialog once a real provider exists, not before.

**Rule.** `_OtpAuthForm.cshtml` gained a third step, `data-otp-step="password"`, alongside the
existing phone and code steps. "More options" (moved to the code step in the previous pass) now
opens `#otp-more-options`, a Bootstrap modal with one list item, "Sign in with password". Selecting
it copies the phone number already captured in the code step into the password step's hidden
`Identifier` field and label, then swaps steps client-side — no navigation, no data re-entry. The
password step's form carries the same `data-login-form` attribute the classic full-page login form
uses, so it posts to the current URL and is handled by the exact same `AuthService.LoginAsync` path
as before; nothing new was added to the server. "Use a code instead" on the password step reverses
the swap. The modal trigger is still a real `<a href="/{portal}/login?mode=password">` under
`data-bs-toggle="modal"`, so the classic full-page password form remains a working fallback if
Bootstrap's JS fails to load — it was never deleted, only stopped being the primary path.

**Not done:** the other entries Uber's own dialog offers — phone call, WhatsApp, email — since none
of those delivery channels exist yet in `IOtpSender`. Google/Apple/QR-code sign-in shown in the same
recording was also left out; GigApp has no OAuth provider configured and that was not part of what
was asked for.

---

## 2026-09-13 — Real-time updates over SignalR, replacing manual page refresh

**Asked:** the portals only ever showed live data on the request that loaded the page — a partner
had to refresh to see a new job appear on the board, a customer had to refresh to see a bid come in,
and an accepted bid did not disappear from a rejected partner's own list until they reloaded too.
Interval polling was explicitly ruled out ("har kuch time baad database hit karta hai jo ki galat
hai") — the fix had to push, not poll — and it had to be built so a future Flutter app can reuse the
exact same connection with no server-side rework.

**Rule.** One hub, `/hubs/app`, authenticated with the same JWT every other endpoint uses — the
cookie for a browser, `?access_token=` on the query string for anything that cannot set a header
during the WebSocket handshake (mobile's own bearer token works unchanged). A connection joins one
or more groups on `AppHub.OnConnectedAsync`, looked up fresh from the database every time: its own
`user-{userId}`, `category-{categoryId}-partners` if it is a partner (their own skill category), and
`admins` if it is staff.

**The server never pushes data, only a topic name.** `IRealtimeNotifier` sends a `"refresh"` event
carrying a short string like `"tasks"`; the receiving page's own already-written `load()` function
re-runs and re-fetches from the same REST endpoint it already calls on first paint. This was a
deliberate trade — a typed push DTO per feature would have meant maintaining two representations of
every list; re-running `load()` means the live case and the first-paint case can never drift apart,
at the cost of one extra round trip per update. Wired into every point a task or bid actually
changes: `TaskClaimService` (claim, admin assign), `GigTasksController` (create, status change,
reopen-after-cancellation), `BidService` (place, withdraw, counter, reject, accept, accept-counter).

**A second, separate `"notification"` event drives the bell live**, going through the existing
`INotificationChannel` plug-in point as a new `SignalRNotificationChannel` — every call site that
already called `INotificationService.PushAsync` needed zero changes, it now simply also arrives
instantly instead of only on next page load. `GET api/notifications/summary`, `GET
api/notifications` and `POST api/notifications/read` are new — the bell's data existed only as
Razor `ViewData` before this, which is nothing for a mobile client to call.

**Verified live**, not just built: with a customer's My Tasks page left open and untouched, placing
a bid as a partner from a separate session updated the customer's bell count and re-ran their task
list with no manual refresh; accepting that bid updated the board for other partners in the same
category and for admin, all through the same connection, with no interval timer anywhere in the
client. Group membership is per-connection, so signing out and back in, or a role change, is picked
up on the next connect — there is no stale-group case to handle separately.

---

## 2026-09-13 — Rate limiting, off-duty gating, refresh tokens, and Manage devices

**Asked:** work through four items from the "Not done yet" list — rate limiting on
login/registration/OTP, `IsAvailable` gating task acceptance, refresh tokens, and Manage devices
(the last one added mid-list, and paired with refresh tokens since both need the same session
store). Left for later on purpose: `Jwt:Key`/DB password to environment variables, a real SMS
provider, and the three Flutter apps.

**Rate limiting.** ASP.NET Core's built-in rate limiter, two fixed-window policies keyed by client
IP: `auth` (10 requests/minute — every password login, OTP verify, and register action across all
three portals plus `api/auth`) and the tighter `otpRequest` (3 requests/5 minutes — only
`otp/request`, since each one costs a real SMS once a provider is wired in). A rejected request gets
`{"title":"Too many attempts...","status":429}`, the same `ProblemDetails` shape every other error
already uses, so `apiErrorMessage` in `global.js` displays it without any special-casing.

**`IsAvailable` now gates the two places a partner directly takes work.** `TaskClaimService.ClaimAsync`
(claiming a fixed-price job from the board) and `BidService.PlaceAsync` (placing a bid) both refuse
with "You are marked off duty. Switch to on duty before accepting work / placing a bid." when
`Partner.IsAvailable` is `false` — checked right alongside the existing KYC check, same message
style. Deliberately **not** touched: `OfferService`'s automatic offer chain already filtered on
`IsAvailable` before this (an off-duty partner was never offered a job to begin with), the admin's
manual `TaskClaimService.AssignAsync` override (an admin overriding availability is the point of a
manual assignment), and `BidService.AcceptCounterAsync` (accepting a counter on a bid placed while
available is a narrow enough timing window that gating it added real complexity for little value).

**Refresh tokens.** `Jwt:ExpiryMinutes` dropped from 7 days to 60 minutes; a new `RefreshTokens`
table (`Services/RefreshTokenService.cs`) backs a 30-day, rotating refresh token issued alongside
every JWT. Rotation means single-use: redeeming a refresh token immediately revokes it and issues a
new one, so a captured-and-replayed old token is caught the moment the real device redeems its own
copy first — verified live (redeeming the same raw token twice: first call succeeds, second is
`401`). `AuthResponse` now carries `RefreshToken`/`RefreshExpiresAtUtc` alongside the existing
`Token`/`ExpiresAtUtc`, so mobile clients get both from the same login/register/OTP-verify call and
are expected to call the new `POST api/auth/refresh` themselves when the access token is close to
expiring or a request comes back `401`.

- **The portal (cookie) flow never lets the browser see either token.** `PortalControllerBase.IssueAuthCookie`
  now sets two HttpOnly cookies — `gigapp_token` (60 min) and `gigapp_refresh` (30 days) — and a new
  shared `POST /{portal}/refresh-session` reads the refresh cookie, redeems it, and re-issues both.
  `global.js`'s `wireSessionRefresh` calls that endpoint every 20 minutes on any portal page, silently
  — a portal session now outlives the short access token exactly the way the old 7-day cookie used
  to, without a single JS timer failure ever surfacing to the user (it only becomes visible at the
  point the access token genuinely expires, which falls back to the existing login-redirect behavior).
- **Signing out revokes the refresh token, not just the cookies.** `ClearAuthCookieAsync` reads
  the current `gigapp_refresh` cookie and revokes that exact row server-side before deleting both
  cookies — a token an attacker captured earlier stops working the moment the real owner logs out,
  rather than lingering until it happens to expire on its own.

**Manage devices.** The profile section that was a disabled "Coming soon" placeholder is live —
`_ProfileDevices.cshtml` lists every active (non-revoked, unexpired) `RefreshTokens` row for the
signed-in user via `GET api/devices`, with a human `DeviceLabel` parsed from the User-Agent
(`Services/DeviceLabel.cs` — no external library, a short heuristic covering the common
browser/OS combinations), IP address, and last-used time. "Sign out" revokes one row
(`DELETE api/devices/{id}`); "Sign out all other devices" revokes every row except the one
currently in use (`POST api/devices/revoke-others`) — both wrapped in the standard SweetAlert
confirm, matching every other destructive action in the app. The section is available to every
role (customer, partner, admin, superadmin) — a session to revoke is not role-specific.

---

## 2026-09-13 — OTP is now the default sign-in for Customer and Partner; one entry point, not two

**Asked:** there should never be a separate "register" page a visitor has to choose over "login" —
one page only, where entering a phone number either signs the person straight in (if the number is
already known) or walks them into account setup / KYC (if it is not). This is what the OTP flow
already did for a portal set to OTP mode; the gap was that **Password was still the default** for
both Customer and Partner, so most visitors were seeing the classic two-page Login/Register split
rather than the unified one. Password stays available on purpose — reachable only through "More
options" on the OTP screen, exactly as before — for anyone who prefers it.

**Rule.** `AuthSettings.CustomerLoginMode` and `PartnerLoginMode` now default to `LoginMode.Otp`
(the model default, the seeded row, and `AuthSettingsService.GetAsync`'s no-row fallback all
changed together). The existing dev database's row was updated the same way through
`/admin/settings/auth` — a super admin can still flip either portal back to password-first at any
time; this only changes what a fresh install, or an unconfigured settings row, starts on.

**Removed the second link everywhere one existed**, since a real second entry point defeats the
point of the phone number deciding sign-in vs. sign-up on its own:
- `_ShopHeader`'s account dropdown had "Login" and "Sign up" as two menu items; it is now one icon
  linking straight to `/customer/login`.
- `_ShopLayout`'s footer had "Register as a partner" and "Partner sign in" as two links under "For
  professionals"; now one — "Become a partner / sign in" — to `/provider/login`.
- `Checkout.cshtml`'s anonymous-visitor card had "Login" and "Create an account" as two buttons; now
  one "Sign in" button, with copy explaining a new number is set up on the spot.

**Not removed:** the classic `/customer/register` and `/provider/register` forms themselves — they
are still what "More options" → password mode needs for someone with no account yet, so they stay,
just no longer linked from anywhere as a primary path. `CustomerController.Register`'s GET already
redirected to `/customer/login` whenever `CustomerLoginMode == Otp`, from the OTP work earlier this
session — that guard is what keeps a stale bookmark to `/customer/register` from showing the old
two-page split even now that OTP is the default everywhere, not just where an admin had opted in.

**Asked separately, not yet built:** whether OTP provider credentials (API key, sender id, etc.)
could be made admin-configurable instead of hardcoded, since `IOtpSender` is already pluggable and
`LoggingOtpSender` is a dev-only stand-in. Different providers (Twilio, MSG91, TextLocal, Gupshup,
AWS SNS) do not share a credential shape, so this would need a provider picker plus per-provider
fields, not one fixed form. Real work, scoped separately — not part of this change.

---

## 2026-09-13 — Provider's Register, every portal's Login/OTP screens, and the storefront cart converted

**Asked:** continue the API+JS conversion through the three remaining items, in order — Provider's
Register (KYC signup), the password/OTP login screens shared by all three portals, and the
storefront's post-first-paint behaviour (cart, checkout). All three written and built first, one
browser-verification pass at the end, per the standing "update everything, test once" instruction.

**Provider's Register.** `RegisterPartnerViewModel` (the GET/POST-shared ViewModel) is gone —
GET now returns a minimal `RegisterPartnerShellViewModel` (just the category list for the
anonymous-safe local picker), and POST binds `RegisterPartnerRequest` directly, the same request
DTO `api/auth/register/partner` already used. The view is a static form; its own inline script
builds `FormData`, generates a throwaway password client-side (`crypto.randomUUID()` + a fixed
suffix, mirroring `OtpCredentials.GenerateRandomPassword()`) when arriving from a verified-OTP
redirect, and posts to `/provider/register` — the same MVC route, not `api/auth`, because only the
portal controller can call `IssueAuthCookie`. The action now returns JSON (`{redirectTo}` or a
`ProblemDetails` error) instead of re-rendering the view.

**Login and OTP, all three portals.** `PortalControllerBase` gained `SignInJsonAsync` — the same
contract as the existing `SignInAsync` (issue the cookie, check the role, run an `onSuccess`
callback) but returning a JSON error instead of re-rendering a view. Every portal's password
`Login` POST and Customer/Provider's `RequestOtp`/`VerifyOtp` now go through it and return
`{redirectTo}` on success. `_LoginCard` and `_OtpAuthForm` are shared partials with no
`@section Scripts` of their own — the actual wiring (`wireLoginForm`, `wireOtpAuthForm`) lives in
`global.js`, driven by `data-login-form` / `data-otp-auth` and friends, exactly like
`wireLocationCapture` already did for address pins. The OTP card now renders both steps (phone
entry, code entry) in the same page and toggles between them with `hidden`, instead of the old
full-page re-render per step; "Resend" and "Change number" no longer reload the page either.

**Fixed — JSON binding does not turn `""` into `null` the way form binding does.** `VerifyOtpRequest.Name`
is optional but carries `[StringLength(MinimumLength = 2)]`; sending an empty string for a blank
name field (correct for a classic form POST, where ASP.NET Core's form binder silently converts
`""` to `null` for a nullable string) fails that validation when the same value arrives as a JSON
body, because `System.Text.Json` has no such conversion. Caught live in the browser — verifying
OTP without typing a name returned "Enter the 6-digit code." even with a correct code. Fixed by
having the client send `null` instead of `""` for a blank optional field before posting JSON —
worth remembering for the next optional string field with a `MinimumLength` that moves from form
binding to `[FromBody]`.

**Storefront cart and checkout.** New `CartController` (`api/cart`, anonymous, wrapping the
existing `ICartService`) replaces `ShopController`'s old `AddToCart`/`UpdateCart` actions, which
posted back to the same page and relied on a full reload to show the new state. `_ServiceCard` and
`_QuantityStepper` are now static, data-attribute-driven markup; `wireCartControls` in `global.js`
handles add/increment/decrement/remove anywhere they appear (tile grids, the cart page, the
checkout sidebar) and re-renders every on-page cart surface — the header badge, the sticky cart
bar, per-line totals, the payment summary, the checkout button's own total — from one
`CartViewDto` response, firing a `cart:updated` event so a page can react to its own edge cases
(Cart.cshtml reloads if the cart becomes empty; Checkout.cshtml redirects back to `/cart` for the
same reason, matching what the old GET action already did). `ShopController.PlaceOrder` now binds
`PlaceOrderRequest` from the JSON body and returns `{redirectTo}` instead of a redirect.

**Cleanup.** Removed the now-dead `CreateAddress`/`UpdateAddress`/`SetDefaultAddress`/`DeleteAddress`
actions and `AddressError` helper from `PortalControllerBase` — a gap missed in the previous
pass, fully superseded by `AddressesController`'s API since the Addresses conversion. Removed the
`AddressService`/`BankAccounts`/`OrderHistory`/`Ratings` fields from `PortalControllerBase` and the
matching constructor parameters from `AdminController`, `CustomerController` and
`ProviderController` — all four were injected purely to pass through to the base class and never
read once the profile sections that used to call them moved to `ProfileController`'s API. Removed
`OtpAuthStep` and the `Step`/`Phone`/`Name`/`DevCode` properties on `OtpAuthViewModel` (state that
now lives entirely in the page's own JS), and the dead `FirstModelError` duplicate helper on
`ProviderController` (`PortalControllerBase.FirstError` already does the same thing).

**Not converted:** Customer's own password-based Register screen — still classic Razor, since it
was never in scope for this pass (Provider's Register, and the shared Login/OTP screens, were).

---

## 2026-09-13 — Customer's Post a task, My tasks and My addresses converted

**Asked:** finish the Customer-only profile sections left over from the previous pass — Post a task,
My tasks and Addresses — converted together on purpose, since Post-a-task and Addresses share the
same `_AddressForm`/`_AddAddressModal` and were explicitly deferred together for that reason. Write
all the code first, build once, then do a single browser-verification pass at the end rather than
testing section by section.

**Rule — every task/bid/partner endpoint already existed.** `POST api/gigtasks` (create),
`GET api/gigtasks/my` (live list), `GET/PUT api/gigtasks/{id}/status` (cancel), `GET api/bids/task/{id}`,
`POST api/bids/{id}/accept|counter|reject` and `GET api/partners/{id}/public` were all built during
earlier mobile-facing API work. Nothing new was needed on the task/bid side — only the web views had
to stop building their own copies of this data server-side and start calling these directly.

**Addresses is now one shared, static shell.** `_AddressForm` and `_AddAddressModal` moved from a
per-instance `@model AddressDto` partial (one rendered per row) to fixed-id static markup embedded
once per page, driven by a single `Admin.openAddressModal(address, onSaved)` helper in `global.js`
that fills the form, wires its submit to `POST`/`PUT api/addresses`, and calls `onSaved` back into
whichever page opened it. `_ProfileAddresses.cshtml` (My addresses) and `_ProfilePostTask.cshtml`
("Add another address" inline) both call the same helper now — the coupling that made Addresses get
deferred in the previous pass is exactly what this shared helper resolves. See
[docs/REUSABLE.md](docs/REUSABLE.md) for the signature.

**Fixed — `PartnersController.MayViewPartnerAsync` was too narrow.** `GET api/partners/{id}/public`
only allowed a customer to view a partner **assigned** to one of their tasks. Opening the bidder's
profile from the bids-review modal before deciding whom to accept — the whole point of showing that
modal — used to 403. It now also allows a customer to view any partner who has an **open bid** on one
of their tasks, checked via `TaskBids`.

**Cleanup.** Deleted `_AddressBook.cshtml`, `_TaskBidsModal.cshtml`, `_PartnerInfoModal.cshtml`
(all confirmed dead by grep before deletion — replaced by the fetch-on-demand shared modals). Removed
`CreateTask`, `RateTask`, `CancelTask`, `AcceptBid`, `CounterBid`, `RejectBid` and their private
helpers from `CustomerController`, along with the now-unused `CustomerDashboardViewModel` and
`AddressBookViewModel` classes and the `Work`/`Addresses` properties on `ProfilePageViewModel` that
only those Razor-rendered sections ever populated.

**Verified in the browser end to end:** posted a fixed-price task (category → service cascading
picker, budget auto-filled and locked for a fixed-price service), added/edited/made-default/removed
an address from both My addresses and inline from Post a task, placed a partner bid, sent a customer
counter-offer, accepted it as the partner, opened the bidder's profile from the bids modal before
accepting (the `MayViewPartnerAsync` fix), and cancelled a pending task with the SweetAlert confirm.
Spot-checked Addresses renders correctly on `/admin/profile/addresses` and `/provider/profile/addresses`
too, since the shared `PortalControllerBase.ProfileSectionAsync` was touched. No exceptions in
`run-err.log` across the whole pass.

**Not converted yet:** Provider's Register and password/OTP login screens, and the storefront's
post-first-paint behaviour (cart, filters, pagination).

---

## 2026-09-13 — The profile sections shared by every portal converted: Details, Bank, Settings, Orders

**Asked:** continue the API+JS conversion into the profile sections every portal shares — Personal
details, Bank account, Change password, and Order history. Convert everything first, verify once at
the end, rather than testing page by page.

**Rule — almost every endpoint already existed.** `ProfileController` (`api/profile`) already had
`GET`/`PUT` for details, `POST photo`, `POST password`, `GET`/`PUT bank`, `GET history` — all built
during earlier mobile-facing API work and simply never wired to the web views. Only Order history
needed something new: `GET api/profile/orders` (paged + searched + status-filtered, bundling the
caller's own ratings and enquiries for those tasks in the same response) and `POST
api/profile/orders/{id}/help` (self-service raise, wrapping the same `ISupportService.RaiseAsync`
the old Razor action called). Rating a partner reuses the existing `POST api/gigtasks/{id}/rate` —
no new endpoint needed there either.

**Fixed — a real, pre-existing bug in `ProfileService.UpdateAsync`.** Its phone/email uniqueness
checks compared across every role (`u.Phone == phone`, no `Role` filter), contradicting the
documented `(Phone, Role)`/`(Email, Role)` uniqueness the rest of the app enforces — a person holding
both a customer and a partner account with the same phone (explicitly supported, see the
architecture section above) could not save their own Details on either account without the update
being rejected as "already in use" by their own other account. Both checks now include
`u.Role == user.Role`, matching the actual unique index. Found while testing this conversion against
dev data that has exactly that shape (an admin and a partner account sharing a phone from earlier
OTP testing) — not something the JS conversion itself touched, but a latent bug the same phone
number surfaced.

**Skipped on purpose — Addresses.** `_AddressForm`/`_AddAddressModal` are shared with
`_ProfilePostTask` (Customer's not-yet-converted post-a-task form), which relies on the classic
POST-and-reload behavior to refresh its address dropdown after adding one inline. Converting the
address form to fetch-based submission would need PostTask's own JS updated in the same pass to stay
consistent — better done together with Customer's conversion than half now, half later.

**Cleanup.** `PortalControllerBase` lost `UpdateProfile`, `UpdateProfilePhoto`, `ChangePassword`,
`SaveBankAccount`, `RaiseEnquiry` and the private `LoadOrdersAsync`/`ProfileError` helpers —
`ProfileSectionAsync` now only loads what the still-Razor sections (Addresses, PostTask, Tasks) and
the sidebar (`Partner`, for the KYC badge) actually read. `ProfilePageViewModel`'s `Form`, `History`,
`BankAccount`, `BankForm` and `Orders` properties, and the `OrderHistoryViewModel` class, are gone —
nothing read them once the four sections fetch their own data.

**Not converted yet:** Addresses (see above), Provider's Register and the password/OTP login
screens, Customer's Post-a-task/My tasks sections and the storefront's post-first-paint behavior.

---

## 2026-09-12 — Provider's KYC, Earnings and Service area profile sections converted

**Asked:** continue the API+JS conversion into the three Provider-only profile sections — KYC
(status, skill change, document upload, history), Earnings (summary, plan, ledger statement) and
Service area (location pin, radius, saved-address copy).

**Rule — a partial rendered mid-page cannot use `@section Scripts`.** Every earlier page in this
conversion put its JS in the top-level view's `@section Scripts` block, which the layout places
*after* jQuery/bootstrap/`global.js` load. These three sections are Razor partials nested inside
`_ProfileBody`, itself nested inside `Profile.cshtml` — a `@section` declared that deep is invisible
to the layout, so a plain inline `<script>` runs immediately at parse time, before jQuery has loaded,
and `$` is undefined. The fix: wrap the whole script in
`document.addEventListener('DOMContentLoaded', function () { ... })` instead of an immediately-invoked
function — by the time `DOMContentLoaded` fires, every script tag before `</body>`, jQuery included,
has already run. **Any future partial-embedded (not top-level-view) inline script must do the same.**

**New self-service endpoints, all on `PartnersController` (`api/partners/me/...`)**: `GET .../kyc-history`,
`GET .../earnings` (summary + bank account in one call), `GET .../earnings/entries` (paged statement),
`GET .../service-area` and `PUT .../service-area` (the exact logic that lived in
`ProviderController.UpdateServiceArea`, moved as-is). Skill change and document upload already had
API endpoints (`PUT api/partners/me`, `POST api/partners/me/kyc`) from before this session's
conversion work — the KYC section's forms just point at them now instead of posting to the Razor
action.

**Fixed — `Admin.showImagePreview` only supported one image-upload widget per page.** It set every
`[data-image-thumb]` on the page to the same URL, which is exactly wrong on the KYC page's three
side-by-side uploads (selfie, Aadhaar front, Aadhaar back). It now takes an optional `fieldName` to
scope the update to the one `_ImageUpload` widget whose `<input name="...">` matches — existing
single-widget callers (`CategoryForm`, `ServiceForm`, `BannerForm`) are unaffected since they omit it.

**Cleanup.** `ProviderController` lost `UpdateServiceArea`, `UpdateSkill`, `SubmitKyc`,
`ReplaceKycAsync` and `GetOwnPartnerAsync`, along with the `IFileStorageService`/`IKycHistoryService`/
`IEarningsService` fields that existed only to support them — `LoadProfileExtrasAsync` now only
resolves the partner for the sidebar's KYC badge, nothing heavier. `_KycReview`, `_KycHistory`
partials and `PartnerEarningsViewModel`/`ServiceAreaViewModel` are gone — nothing reads them once the
three sections fetch their own data.

**Not converted yet:** Provider's Register and the password/OTP login screens, and the shared profile
sections every portal uses (Details, Bank, Addresses, Orders, Settings) — those are cross-portal
infrastructure in `PortalControllerBase`, larger and riskier to touch than a single portal's own
section, and are better done as their own pass. Customer's authenticated pages and the storefront's
post-first-paint behavior are still next after that.

---

## 2026-09-12 — Provider dashboard converted, and the cancel-reopens-job rule moved to the shared API

**Asked:** continue the API+JS conversion into Provider, starting with the dashboard — the page every
partner sees every day (available work, my bids, my jobs, a live job offer).

**Rule — one endpoint composes the whole dashboard.** `GET api/partners/me/dashboard`
(`PartnersController`) returns the profile, my jobs, and — once verified — available tasks
(skill-matched, radius-filtered, distance-sorted, excluding anything already bid on or previously
cancelled by this partner), my bids and any live offer, in one call. It is a direct port of the logic
that used to live in `ProviderController.Index()`; nothing about the ranking, exclusion or ordering
rules changed, only where they run.

**Fixed — the API's `PUT api/gigtasks/{id}/status` was missing the cancel-reopens-job rule.**
`ProviderController.UpdateStatus` (the Razor action) had the full
`TaskCancellations`/notify-customer/notify-admins/restart-offer-chain logic; the generic API action
of the same name did not — it just moved the status to `cancelled` with the partner still attached.
Since the API is the one endpoint mobile and every web page are meant to share, the gap meant a
mobile partner cancelling a job would silently skip the entire rule. The reopen logic now lives in
`GigTasksController.UpdateStatus` itself, gated on `newStatus == cancelled && isPartner && task.PartnerId is not null`,
so every caller gets it. `UpdateGigTaskStatusRequest` gained an optional `CancelReason`.

**New — `OffersController` (`api/offers/{id}/respond`)**, a thin wrapper over the unchanged
`IOfferService.RespondAsync`. Everything else the dashboard needed already existed:
`BidsController` (place/mine/withdraw/accept-counter), `GigTasksController` (`.../accept`,
`.../status`), `PartnersController` (`me/availability`).

**Cleanup.** The dashboard's own countdown timer replaced `global.js`'s `wireCountdowns` — that
helper only ever had the one caller, and a page-specific timer belongs inline like everything else in
this pattern. `_MyJobs`, `_MyBids` and `_JobOffer` partials, `ProviderDashboardViewModel`, and
`ProviderController`'s `PlaceBid`/`RespondToOffer`/`AcceptTask`/`WithdrawBid`/`AcceptCounter`/
`UpdateStatus`/`ReopenAfterCancellationAsync`/`ToggleAvailability`/`BidError` are gone, along with the
`IBidService`/`ITaskClaimService`/`IMatchService`/`IOfferService` fields that existed only to support
them.

**Not converted yet:** Provider's profile sections (KYC, Earnings, Service area), Register and the
password/OTP login screens — those stay Razor-form-based for this pass. Customer's authenticated
pages and the storefront's post-first-paint behavior are next after Provider is finished.

---

## 2026-09-12 — The rest of Admin converted: Tasks, Support enquiries, Payouts, Error log

**Asked:** continue the API+JS conversion through the last four Admin pages, finishing the whole
portal.

**Rule — the assign-partner dialog became one shared modal, populated on demand.** The old
`/admin/tasks` view rendered one hidden modal per open row, each carrying its own pre-computed
`RankPartnersAsync` call — every page load ranked partners for every open task whether or not
an admin ever opened that row. `GigTasksController` gained `GET api/gigtasks/all` (paged list) and
`GET api/gigtasks/{id}/matches` (ranking, called only when "Assign"/"Reassign" is actually clicked)
plus `POST api/gigtasks/{id}/assign`, wrapping the unchanged `ITaskClaimService.AssignAsync`. One
`#assign-modal` in `Tasks.cshtml` is now reused for every row.

**Rule — Support enquiries reuses the existing service untouched.** `ISupportService.ListAsync`
already did search, status filtering and the open-first ordering; the new `EnquiriesController`
(`api/enquiries`) is a two-method pass-through (`GET`, `POST {id}/review`). No business logic moved.

**Rule — Payouts split cleanly along the same lines the old controller already had.** The balances
list (`GET api/earnings/balances`) and a partner's full ledger page (`GET api/earnings/{partnerId}`,
bundling the partner, summary, bank account, current plan and plan history in one response so the
page needs one fetch, not five) are new on `EarningsController`; `GET .../entries`,
`POST .../payout`, `POST .../adjustment` (still super-admin only) and `POST .../plan` wrap
`IEarningsService`/`IPlanService` exactly as the Razor actions did. The per-partner ledger keeps its
own route (`/admin/payouts/{id}`) since it is a real page, not a modal — the shell just carries the
partner id and lets JS fill in the title and every card.

**Rule — Error log's inline EF query became `ErrorLogsController`**, same search/`showResolved`
filter and the same `POST {id}/resolve`; nothing about the audit trail shape changed.

**Cleanup.** `AdminController` lost `Tasks`'s query logic, `AssignTask`, `Payouts`'s balance query,
`PartnerLedger`'s five-service assembly, `AssignPlan`, `RecordPayout`, `RecordAdjustment`,
`Enquiries`'s query, `ReviewEnquiry`, `Errors`'s query and `ResolveError` — along with the
`ITaskClaimService`, `IMatchService`, `IEarningsService`, `IPlanService` fields that existed only to
support them. `AdminTasksViewModel`, `AdminEnquiriesViewModel`, `AdminPayoutsViewModel`,
`AdminPartnerLedgerViewModel` and `AdminErrorLogsViewModel` are gone from `AccountViewModels.cs`,
replaced by `PartnerLedgerShellViewModel { int PartnerId }` for the one shell that still needs to
carry a route parameter.

**All of Admin is on the new pattern now.** Provider, Customer's authenticated pages and the
storefront's post-first-paint behavior are next.

---

## 2026-09-12 — Partners and Approvals converted, and the KYC/account modals became shared

**Asked:** continue the API+JS conversion into Partners and Approvals — the highest-risk pages so
far, since they carry the real KYC approve/reject decision and a super admin's password-reset/
deactivate controls, not a settings master.

**Rule — the KYC and account modals moved from Razor to shared JS.** `_KycModal.cshtml` and
`_AccountModal.cshtml` are now static, id-based shells included once per page (not once per row);
`Admin.openKycModal(partner, history, onDecision)` and `Admin.openAccountModal(user, onSaved)`
(`global.js`) populate and wire them, so `Partners.cshtml`, `Approvals.cshtml` and `Users.cshtml`
all drive the same two modals instead of each keeping its own copy. `PartnersController` gained
`GET api/partners/all` (paged, `search`/`verified`/`categoryId`) and `GET api/partners/{id}/kyc-history`;
the existing `PUT api/partners/{id}/verify` needed no change — its approve/reject validation was
already correct and is now called directly from the JS decision callback.

**Rule — the ordering business rule moved into the query, not the view.** `/admin/approvals` must
list pending → not submitted → rejected → approved, oldest rule unchanged from the Razor version;
`GetAllPaged`'s `OrderBy` now expresses that priority directly, so `Partners.cshtml` (all statuses)
and `Approvals.cshtml` (`verified=false`) share one endpoint and one ordering instead of Approvals
needing its own query.

**Cleanup.** `_PartnerTable.cshtml` and the old per-row `_UserAccountModal.cshtml` are deleted —
nothing referenced them once Partners.cshtml stopped using either. `AdminController.ResetUserPassword`,
`SetUserActive` and their private `LocalOr` helper are deleted too — `_UserAccountModal` was their
only caller. `AdminPartnersViewModel` and `KycModalViewModel` are gone from `AccountViewModels.cs`.

**Not converted yet:** Tasks (the ranked-partner assign/reassign UI), Support enquiries, Partner
payouts, Error log — the rest of Admin — plus the entirety of Provider, Customer, and the
storefront's post-first-paint behavior. Same pattern, same page-by-page care.

---

## 2026-09-12 — Admin portal starts moving to JSON API + page JS, one page at a time

**Asked:** stop rendering the app in Razor — every controller should expose its data through a JSON
API, and each page's view should be a thin shell driven by JavaScript, so it is obvious what belongs
to what. Confirmed scope: eventually all three portals, but the public storefront's first page load
stays server-rendered (SEO — see CLAUDE.md), and everything after that first paint goes through the
same API pattern as the rest. Refined mid-session: a page's script lives **inline** in that view's
`@section Scripts` block, not as a separate `.js` file per page — open the view and its behaviour is
right there. Code shared by more than one page belongs in `wwwroot/js/global.js` instead of being
copy-pasted between pages' inline scripts.

**Rule — one endpoint does both create and update.** `SkillCategoriesController.Save` (`POST
api/skillcategories`) is the first page rebuilt this way: `SaveSkillCategoryRequest.Id` absent means
create, present means update — one method, not a `Create`/`Update` pair, so a future change to this
rule is made once. The MVC `Categories`/`CategoryForm` views became empty shells; the rendering
against the API is inline JS inside each view. This is the template — every later page (~24 more
Admin pages, then Provider, then Customer, then the storefront's post-first-paint behavior) repeats
the same shape rather than reinventing it. See `docs/REUSABLE.md` for the concrete pattern (`App.api`,
`Admin.*` helpers, both defined once in `global.js`).

Five more Admin masters converted the same session, same pattern throughout: **Services**
(`ServiceItemsController`, `api/serviceitems` — reuses the existing `data-master` autocomplete
picker unchanged, since that already talks to `api/masters` over AJAX), **Banners**
(`BannersController`, `api/banners`), **Commission plans** and **Tax rules**
(`CommissionPlansController`/`TaxRulesController`, `api/commissionplans`/`api/taxrules`), **Menu**
(`MenuItemsController`, `api/menuitems`, including the dynamic "valid parent" dropdown as
`GET .../parent-options?excludingId=`). Plans, Taxes and Menu were the easy case:
`IPlanService`/`ITaxService`/`IMenuService` already held every rule (validation, the `int? id`
create-or-update split, even the `IActionDescriptorCollectionProvider` link check for Menu) — the
new API controllers are a thin pass-through, no logic moved. **Check for an existing service first;
a controller that already delegates to one needs no re-deriving, just a JSON wrapper around it.**

**Customers and Administrators converted too** — one shared shell view (`Users.cshtml`) drives both,
same as before; `UsersController` gained a paged/searchable `GET api/users/all` and the super-admin-
only `POST {id}/reset-password` / `POST {id}/active` actions (`IUserAdminService`, unchanged). The
old per-row `_UserAccountModal` partial became a single modal built once and populated by JS on
"Manage" — cheaper than one hidden modal per table row. **Partners and Approvals are not converted
yet** — they share `_UserAccountModal` and the KYC review modal with real approve/reject logic, so
`AdminController.ResetUserPassword`/`SetUserActive` stay in place until that page moves too.

**Not converted yet, and deliberately left for a dedicated pass:** Partners and Approvals (the
partner list carries the KYC review modal and the super-admin account modal — real approval and
password-reset flows, not a CRUD master), Tasks (the ranked-partner assign/reassign UI), Support
enquiries, Partner payouts, Error log — all of Admin's remaining pages — plus the entirety of
Provider, Customer, and the storefront's post-first-paint behavior. These carry more business risk
(KYC gates real partner income; task assignment touches live jobs) than a settings master does, so
they get the same page-by-page care rather than being rushed through at the end of a long session.
The pattern above is unchanged for them — same API-controller shape, same shell-view-plus-inline-JS
convention.

---

## 2026-09-12 — Mobile OTP as an admin-switchable alternative to password, and icon navbars

**Asked:** customer and partner portals should be able to sign in with a phone number and an OTP
instead of a password, toggled from an admin settings screen (admin itself always stays on
password); a single phone-entry screen should serve both sign-in and sign-up; every navbar should
use icons instead of labelled buttons.

**Rule — the login mode is a per-portal setting, not a build flag.** `AuthSettings` is a one-row
table (`CustomerLoginMode`, `PartnerLoginMode`, each `password` or `otp`) edited at
`/admin/settings/auth` (super admin only). Each portal's `GET login` action reads it and renders
either the existing password form or the new OTP flow — nothing about `AuthService`'s password path
was removed, so flipping the switch back is instant and lossless.

**Rule — one phone-entry screen is both login and sign-up.** `POST {portal}/otp/request` sends a
6-digit code (`OtpChallenge`, 5-minute validity, 5 attempts, hashed like a password) and re-renders
the same view at its code step; `POST {portal}/otp/verify` checks it, and what happens next depends
on whether the number already has an account:

| Phone | Customer | Partner |
|---|---|---|
| Known | Signed in via `AuthService.LoginWithOtpAsync` — no password ever checked | Same |
| Unknown | Name is asked inline on the same screen, then `RegisterCustomerAsync` runs with a server-generated random password the user never sees | Redirected into the existing `/provider/register` KYC form, phone pre-filled and locked, password fields hidden (still generated and posted so the untouched validation path keeps working) |

A verified OTP sets `User.IsPhoneVerified`/`PhoneVerifiedAt` — the columns CLAUDE.md's architecture
notes said would stay unused "until OTP lands." `IOtpSender` is a pluggable channel exactly like
`INotificationChannel`; `LoggingOtpSender` just logs the code today (also returned in the response
in `Development`, so the flow is testable with no SMS account), and a real provider replaces it
without touching `OtpService`.

**Every portal's top navbar is icon-only now**, via a shared `_Icon.cshtml` partial (`cart`, `user`,
`bell`, `log-out` — inline SVG, no icon library). `_UserMenu.cshtml` (the signed-in bell + avatar
dropdown, shared by all three portals) and `_ShopHeader.cshtml` (the public storefront header, guest
and signed-in states both) carry no more labelled buttons — an unauthenticated visitor's account icon
opens a small dropdown with Login/Sign up instead of two separate buttons.

---

## 2026-09-12 — One customer home page, a partner's cancellation reopens the job, and a UC-style storefront

**Asked:** collapse the duplicate `/customer` dashboard into `/services`, with a guest cart that
survives login; reopen a task when a partner cancels after accepting, excluding that partner from
seeing it again; let an admin reassign a cancelled task; restyle customer-facing buttons.

**Rule — `/services` is the only customer home page.** `CustomerController.DashboardPath` overrides
the base to `/services`, so login, registration and the portal-home brand link all land there.
`GET /customer` is kept only as a redirect for old bookmarks — it renders nothing of its own
anymore.

**Rule — a guest cart is merged into the account at login, not just carried by session.**
`CartItem(CustomerId, ServiceItemId, Quantity)` is the durable copy; `ICartService` mirrors every
`AddAsync`/`SetQuantityAsync` into it whenever the caller is signed in as a customer, and
`MergeIntoAccountAsync` — called from `CustomerController.Login`/`Register` via a new `onSuccess`
hook on `PortalControllerBase.SignInAsync` — folds the session cart into whatever is already stored
for that customer id the moment they sign in. A guest who never logs in still only ever gets the
session cart; nothing is written for them.

**Rule — a partner cancelling an accepted job reopens it, and that partner never sees it again.**
`ProviderController.UpdateStatus` special-cases `status == cancelled`: it records a
`TaskCancellation(GigTaskId, PartnerId, Reason, CancelledAt)` row, clears `PartnerId` and any
admin-assignment fields, puts the task back to `pending`, notifies the customer and every admin,
and — for an instant-booking job — restarts the offer chain via `IOfferService.StartAsync`.
`TaskCancellations` is the exclusion list everywhere a partner could otherwise re-acquire the same
job: `OfferService.StartAsync` excludes it alongside `TaskOffers` when ranking the next partner,
`TaskClaimService.ClaimAsync` refuses a direct claim from a partner who cancelled it, and the
partner's own available-tasks query filters it out. Unlike `TaskOffers`, there is no unique index —
if an admin manually reassigns the same job to the same partner and they cancel again, that is a
second row, not an error.

**Rule — an admin can reassign a `cancelled` task exactly like assigning a `pending` one.**
`TaskClaimService.AssignAsync` now accepts either status as the starting point (both the guard
check and the `ExecuteUpdateAsync` predicate), and now also notifies the newly assigned partner —
previously only the customer was told. `/admin/tasks` shows the same "best matches" shortlist and
required-note form for a cancelled task as for a pending one; the button reads "Reassign" instead of
"Assign" when the task is already cancelled. Admin reassignment is deliberately **not** blocked from
picking the partner who just cancelled — the automatic paths exclude them, a human override does not
have to.

**Customer-facing buttons use `.btn-uc-primary` / `.btn-uc-outline` / `.btn-uc-pill`**, defined in
`wwwroot/css/customer-ui.css` and reusing the existing `--gig-accent` tokens from `site.css`. They
sit alongside Bootstrap's `btn` class rather than replacing it, so focus/disabled/sizing behaviour is
unchanged — only the color, border-radius and hover elevation are custom. Applied to the storefront
(header, cart, checkout, search, service cards) and the customer-only profile sections
(`_ProfilePostTask`, `_ProfileTasks`); provider and admin views are untouched.

---

## 2026-09-06 — The storefront reads like a shop, and promotions are a master

**Asked:** build the storefront out — a spotlight banner, "New and noteworthy", and per-category
strips on the home page.

**Rule — a banner is a row, not markup.** `Banners` is admin-managed at `/admin/masters/banners`,
so a campaign is something support adds rather than a deployment.

| Field | Rule |
|---|---|
| Placement | `spotlight` (the card row under the hero) or `wide` (one full-width strip between sections) |
| Image | **Required.** A banner with no artwork is not a banner, so the form refuses to save one |
| Link | Must be a page on this site. An external URL is refused, because an administrator should not be able to point the front page anywhere |
| Dates | `StartsAt` and `EndsAt` are both optional. Outside its window a banner is simply not rendered, and an end before the start is refused |
| Deleting | Removes the image file too |

The storefront asks only for banners that are active, have an image and are inside their window —
one query, ordered by `SortOrder`. The spotlight row is hidden entirely when there are none, rather
than leaving an empty heading.

**Rule — the home page is sections, and every section hides itself when empty.** In order: hero with
category tiles, spotlight, New and noteworthy (the six newest sellable services), Most booked (by
completed task count), the wide banner, then one strip per category with a **See all** link that
only appears when the category has more than the six shown. A visitor never sees a heading with
nothing under it.

**The hero statistics are real and hidden until they mean something.** Average rating is weighted by
each partner's rating count, not a mean of means. `StorefrontStats.IsWorthShowing` keeps the whole
strip off the page until there are at least five ratings or twenty completed jobs — a shop claiming
"4.6 from 2 ratings" reads worse than one claiming nothing.

**Search** is on every storefront page: `/services?q=` matches a service name, its category name or
its description. No match falls back to the category grid rather than a dead end.

---

## 2026-09-06 — A public storefront with a guest cart

**Asked:** a customer landing page that works like a website for a guest, with a cart and a login
button, item by item, so a customer buys a service at the payout we set and support assigns a
partner. Also: the category image did not appear, and services need one too.

**Rule — what is sellable.** The storefront lists a service item only when **all** of these hold:
the item is active, its category is active, `AllowsInstantBooking` is set, and `BasePayout` is above
zero. Anything else has no fixed price to show, and a storefront without a price is a brochure.

**Rule — the cart belongs to the browser, not to an account.**

- Browsing, adding, changing quantity and viewing the cart all work **signed out**. Asking someone
  to register before they know what anything costs is how you lose them.
- The cart is held in session and stores **only service item ids and quantities**. Every price is
  looked up again on the server, so editing the cart cannot change what anything costs.
- A line whose service was deactivated after it was added is **dropped silently** rather than
  failing the checkout on something the visitor can no longer see.
- Twenty lines and twenty units per line, so a bad script cannot build an unbookable order.
- Signing in does not disturb the cart — the session cookie is the same one, so `/checkout` carries
  straight on.

**Rule — checkout creates one task per cart line.** A line is one service at one address, which is
what a partner is actually sent to do. The quantity rides along on `GigTask.Quantity` so three units
stay one visit for one partner, and the amount is the line total. Every line is created as
`BookingMode = instant` at the catalogue price, which means the existing offer chain picks it up:
each line looks for its own partner, because they are different trades.

Nothing is charged at checkout — the storefront says so plainly. Payment on completion is what the
platform does today, and Razorpay has not landed.

**Fixed alongside:** the category image saved correctly but there was nowhere to see it, so
`/admin/masters/categories` now shows a thumbnail column. Service items now carry an image too, on
the same component, and the storefront uses it. One helper, `ApplyImageAsync`, handles both — it
saves through `IFileStorageService`, swaps the name onto the row and deletes the file it replaced.

---

## 2026-09-06 — Notifications, and automatic assignment switched on

**Asked:** build the notification channel so auto-assign can work.

**Rule — notifications.** `Notifications` is one row per person per event, with a type, a title, a
body and a local link. `INotificationService.PushAsync` writes the row and then hands the same
request to every registered `INotificationChannel`.

- **In-app is the only channel that exists.** SMS needs DLT registration and push needs Firebase and
  the Flutter apps; neither exists. When they arrive they are registered alongside, and **nothing
  that raises a notification changes** — that seam is the whole point of the interface.
- **A channel that throws is logged and skipped.** A provider being down must never roll back the
  job it was telling somebody about.
- The bell is on every page of every portal, filled once in `PortalControllerBase.OnActionExecutionAsync`.
  Opening `/{portal}/notifications` marks everything read — a badge that survives the click is noise.

**Rule — automatic assignment.** A fixed-price job is now offered rather than left for whoever
claims it first. Bidding work is untouched: there the customer chooses.

| Step | What happens |
|---|---|
| Booking | `IOfferService.StartAsync` ranks partners and offers the job to the best one, from both the portal and the API |
| Window | `Platform:OfferWindowSeconds`, five minutes by default. Shorten it once a job reaches a phone as a push |
| Accept | The job is theirs, and the customer is told who is coming and on what number |
| Pass | Straight to the next partner. Explicitly **no penalty** — a partner punished for declining will stop answering |
| Silence | `OfferExpiryWorker` sweeps every 30 seconds, expires the offer and moves it on |
| Chain ends | The job **opens to every partner** and administrators are told nobody took it |

The rules that keep it honest:

- **An offer is first refusal, not a lock.** `TaskClaimService.ClaimAsync` refuses a claim from
  anyone else *while an offer is live*, and allows everyone once it lapses. A partner who closed the
  tab can never strand a customer.
- **One offer per partner per task**, enforced by a unique index, so a chain cannot loop back to
  somebody who already passed.
- **Only available, in-range partners are offered.** Being off duty or outside your own travel radius
  pushes you down the admin's shortlist but takes you out of the automatic chain entirely — an offer
  somebody cannot accept just wastes the window.
- **The customer is told from `TaskClaimService`, not from the offer.** Every route to a job becoming
  somebody's — accepting an offer, claiming it after the chain lapses, an administrator assigning it —
  goes through that one class, so the notification cannot be missed on one path and sent on another.
  That was a real gap, caught by the tests.

`Platform:AutoAssignInstant` turns the whole thing off and falls back to first-come-first-served.

---

## 2026-09-06 — Distance is real, and partners are ranked by it

**Asked:** start on distance and PostGIS.

**Rule.** PostGIS is enabled and distance is measured on `geography(Point, 4326)` columns, so it is
metres over the earth rather than a flat approximation of degrees. Two tables carry a point:

| Column | Meaning |
|---|---|
| `GigTasks.Location` | Where the work is. Snapshotted at booking from the chosen address, exactly like the existing latitude and longitude. |
| `Partners.BaseLocation` | Where the partner sets out from. Moves when they move — it is not history. |

Both have a **GIST index**, and the migration backfilled every existing row from the coordinates
already stored, so matching worked on day one rather than only for bookings made afterwards.

- **`GeoPoint.From(lat, lng)` is the only place a point is constructed.** PostGIS takes X then Y,
  which is longitude then latitude; swapping them silently puts Gurugram in the Indian Ocean, so
  the order is decided once and never repeated.
- **A partner sets their own service area** at `/provider/profile/area` — a pin plus a radius
  between 1 and 100 km, captured from the browser or copied from one of their saved addresses. Half
  a pin is refused: a latitude without a longitude is not a location.
- **The board filters by that radius and sorts by distance.** PostGIS drops out-of-range work in the
  query, so it never loads. A task with **no** pin stays in — an old booking without one is still
  worth doing — and a partner with no pin still sees everything, with a prompt to set one. Urgency
  still wins over distance: a same-day job nearby beats a flexible one next door.
- **`/admin/tasks` now suggests partners rather than asking support to search.** `IMatchService.RankPartnersAsync`
  returns the shortlist for a pending task, best first.

**How the ranking scores.** Distance is 60% of it and rating 40%, because a plumber four kilometres
away with no ratings is a better answer than a five-star one across the city.

- Distance bands: within 2 km scores full, then 5, 10 and 20 km step down, beyond that it is nearly
  nothing. **No pin scores 0.35** — below anyone who has one, but never hidden.
- An **unrated partner sits at the midpoint**, not at zero, or nobody new would ever be picked.
  Confidence ramps over the first ten ratings, so one lucky five-star does not beat a steady record.
- Past their own travel limit multiplies by 0.4; off duty by 0.5. Both are **pushed down, never
  filtered out** — support may still have a reason to pick them, and hiding the option would hide
  the reason too.

**Automatic assignment is still not switched on.** The ranking exists and support picks from it, so
a human is still the decider. Turning it on needs a notification channel and an acceptance window —
what happens when the best partner does not answer — and neither is built.

---

## 2026-09-06 — The customer home page is a catalogue, and the work moved into the profile

**Asked:** move Post a task and My tasks out of the customer dashboard into the profile menu, turn
the dashboard into a list of service categories with images, and give a category an image.

**Rule.** `/customer` is now a catalogue and nothing else. It lists every **active** category as a
tile carrying its image, how many active services sit under it, and the lowest fixed price among
them ("From ₹450"), shown only when that category has at least one instant-bookable service. A tile
links to `/customer/profile/post?categoryId=N`, which preselects the category on the booking form.

The customer profile menu now runs: Profile · **Post a task** · **My tasks** · **Order history** ·
Account details · My addresses · Manage devices · Settings.

- **My tasks shows only live work** — pending, accepted or in progress. Anything completed or
  cancelled belongs to Order history, so the two never show the same row.
- Every task and bid action now returns to `/customer/profile/tasks`, and rating returns to
  `/customer/profile/orders`. Landing back on the catalogue after cancelling a task would hide the
  result of what was just done.
- Posting a task and My tasks are **customer-only sections**. A partner takes work rather than
  posting it, and `SectionApplies` refuses those two paths for anyone else.

`SkillCategory.ImageFileName` holds the artwork, uploaded through the standard image component on
the category form and stored in the new public `FileCategory.CategoryImage` folder. A category with
no image falls back to its first letter on a tinted tile, so the catalogue never has a hole in it.

---

## 2026-09-06 — Order history, and a support enquiry against one order

**Asked:** show all orders in the profile for both the customer and the partner, with a "need help"
button per order, and an enquiry screen in the admin panel carrying a resolution status.

**Rule — order history.** `/{portal}/profile/orders` renders the same list for both sides;
`IOrderHistoryService.ForUserAsync` decides the filter from the role and nothing else. Paged, with a
search box and a status filter. An administrator has no order history — they neither book nor work.

**Rule — enquiries.** `SupportEnquiry` is raised against exactly one order.

| Rule | Detail |
|---|---|
| Who may raise one | Only the customer on that task or the partner assigned to it. |
| Topic | `payment`, `quality`, `behaviour`, `timing` or `other`, backed by a check constraint. |
| Message | At least ten characters. "Help" tells the support team nothing. |
| One at a time | A second enquiry is refused while that person still has one open on the same order — the answer belongs on the existing thread. |
| Status | `open → in_progress → resolved`. |
| Resolving | **Requires a reply.** It is the only thing the person who raised it ever sees. |
| Re-resolving | Refused. A resolved enquiry is closed; a new problem is a new enquiry. |

On success the person is told **"We have noted your complaint. Our team will connect with you
shortly."** along with a reference of the form `SUP-42`, which is what they quote on a call.

`/admin/enquiries` lists them **open first, then being looked at, then resolved**, newest within
each — a resolved enquiry is waiting on nobody and must not push live work down the page. The
sidebar badge counts open **and** in-progress, because both are still the support team's problem.

Both sides see the status and, once resolved, the reply, in their own order history.

---

## 2026-09-06 — One image component for every upload

**Asked:** the "select image" control is very old; build one reusable, modern component, because
image fields are used in many places already and more are coming.

**Rule.** No view renders a bare `<input type="file">`. Every image field is `_ImageUpload`, taking an
`ImageUploadModel`. Seven inputs were replaced: three on partner registration, three on the KYC
section, and the profile photo.

The behaviour a mobile client must match:

- **Resize before upload.** Longest edge is capped at `MaxPixels` (1600 by default) and the image is
  re-encoded as JPEG at 0.85 quality. A file already inside both limits is uploaded untouched.
- **EXIF orientation is applied during the resize**, so a sideways phone photo arrives upright.
- **The extension follows the bytes.** A re-encoded PNG is renamed to `.jpg`, because the server
  checks the extension and the magic bytes separately and a mismatch would be stored.
- **A refused file is discarded, not just reported.** Wrong type or over the limit clears the field so
  it cannot be posted at all.
- Camera capture is offered for the selfie and both Aadhaar sides — those are taken on the spot, not
  picked from a gallery.

Server validation is unchanged and still authoritative: size, extension, `image/*` content type and
the actual file signature are all re-checked in `FileStorageService.SaveAsync`. The client-side work
is there to make a 10 MB photo uploadable on a slow connection, never to be trusted.

**Fixed while testing:** `FileStorage:AllowedExtensions` was binding to eight entries rather than
four, because a configuration array appends to a non-empty default. The upload error listed every
type twice, and removing an extension from `appsettings.json` would have had no effect.

---

## 2026-09-06 — Both sides rate a finished job, but only one of them has to

**Asked:** a rating must be required when the partner completes the work, and optional for the
customer, with a feedback message.

**Rule.** One rating per side per task, held in `TaskRatings` with a unique index on
`(GigTaskId, RaterRole)` and a check constraint keeping stars between one and five.

| Who | When | Required? | Sees |
|---|---|---|---|
| Partner rates the customer | While closing the job | **Yes** — the job will not close without it | Only administrators |
| Customer rates the partner | Any time after it is completed | No | Everyone, as the partner's average |

- **The partner's rating is written in the same save as the completion.** The status change and
  the rating go through one `SaveChangesAsync`, so a completed job can never exist without one.
  `POST /provider/tasks/{id}/status` and `PUT /api/gigtasks/{id}/status` both refuse with "Rate the
  customer before you close this job." when stars are missing or out of range.
- **The customer can only rate a completed job**, and only their own. `RatingService.BuildAsync`
  checks both. The partner is exempt from the completed check because they rate mid-transition.
- **Nobody rates twice.** A second attempt is refused rather than overwriting, so a rating cannot
  be revised after the fact.
- **Averages are a cache on `User`,** not on `Partner` — `AverageRating` and `RatingCount` are the
  person's, and the same two columns serve a customer's rating and a partner's. They are recomputed
  from the whole history by `RatingService.RefreshAverageAsync` after each new rating, never
  incremented, so a bad write cannot drift them permanently.

Ratings exist because assignment needs them. The matching algorithm ranks on rating and distance,
and until this landed there was nothing to rank on.

---

## 2026-09-06 — Support can assign a partner to a pending task

**Asked:** when a customer states a requirement to the support team, an administrator should be able
to put a chosen partner on that task.

**Rule.** `ITaskClaimService.AssignAsync(adminUserId, taskId, partnerId, note)` is the only way this
happens, and `POST /admin/tasks/{id}/assign` is the only caller. It refuses unless every one of the
following holds:

| Check | Message when it fails |
|---|---|
| A note was written | "Write why this partner is being assigned. The note is what the next person reads." |
| The partner exists | "Choose a partner." |
| KYC is approved | "*Name* has not passed KYC, so they cannot be given work." |
| The account is active | "*Name*'s account is deactivated." |
| The partner's skill matches the task category | "*Name* works in a different category, so they cannot take this job." |
| The task is still `pending` | "A *status* task cannot be assigned." |

On success the task moves to `accepted`, `AgreedAmount` is settled at the agreed figure (or the
budget when there is none), and `AssignedByUserId`, `AssignedAt` and `AssignmentNote` are written.
**Every bid still open on that task is rejected in the same call**, so no partner is left waiting on
a decision that has already been made.

The note is required because it is the only record of why a human overrode the normal flow. The
customer sees "Assigned for you by our support team"; the partner sees the note itself under
**My jobs**; the administrator sees who assigned it and when, on `/admin/tasks`.

This exists because automatic assignment needs ratings and distance, and neither is built. A human
decides until they are.

Partners are picked through the `assignable-partner` master, which lists only approved, active
partners in the task's category. That master is **admin-only** — `IMasterSource.Roles` gates it,
because it carries partner names and mobile numbers.

---

## 2026-09-06 — Instant booking is now real, and it skips bidding

**Asked:** confirm whether a service marked for instant booking behaves the same as a bidding one.

It did. `ServiceItem.AllowsInstantBooking` was saved by the admin form and shown as a badge, but
nothing read it, and `GigTask.BookingMode` was never set to `instant`. Every booking went to bidding.

**Rule.** The service item decides the booking mode, and the server decides the price.

- A service item is instant when `AllowsInstantBooking` is set **and** `BasePayout` is above zero.
  The flag alone is never enough — the admin form already refuses the combination, and the booking
  path checks it again for rows that predate that rule.
- Booking an instant service writes `BookingMode = instant`, and sets **both** `Budget` and
  `AgreedAmount` to `BasePayout`. The amount the customer types is ignored, so the price cannot be
  negotiated down by editing the form.
- **Bidding on an instant task is refused** — `BidService.PlaceAsync` returns "This is a fixed-price
  job. Accept it from your dashboard instead of bidding."
- **A partner claims an instant task outright**, first come first served. `ITaskClaimService.ClaimAsync`
  backs both `PUT /api/gigtasks/{id}/accept` and `POST /provider/tasks/{id}/accept`. It requires an
  approved KYC and a matching skill category, and the claim itself is one conditional
  `ExecuteUpdateAsync` so two partners racing cannot both win.
- **A bidding task can never be claimed this way.** The API previously let a partner accept any
  pending task directly; that now returns 403 with "This job is open for bids. Place a bid and wait
  for the customer to accept it." On a bidding task the customer chooses, and letting a partner grab
  it would make the bids they are comparing worthless.

There is no automatic partner selection. First-accept is deliberate for now — real assignment needs
ratings and distance, and until those exist a human assigns instead (see the entry above).

The customer's booking form switches the budget field to the fixed price as soon as an instant
service is picked, and makes it read-only. That is a convenience, not the rule: the server sets the
price whatever the form posts.

---

## 2026-09-06 — Adding an address never leaves the page

**Asked:** "You have no saved addresses. Add one before posting a task." should open a modal instead
of sending the customer away, and the same modal should be reusable wherever an address is needed.

**Rule.** `_AddAddressModal` is the one add-address dialog. It takes the portal slug and renders
`_AddressForm` inside a Bootstrap modal, carrying a hidden `returnTo` set to the page it was opened
from. `CreateAddress` redirects back there rather than always to the address book, so the customer
lands back on the half-filled booking form with the new address selectable.

Used on the customer dashboard, in the My addresses section of both profiles, and available for the
partner gig screens. `_AddressBook` renders the same partial rather than its own copy.

---

## 2026-09-06 — A partner confirms before starting and before completing

**Asked:** confirm with the partner before work starts.

**Rule.** Both transitions ask, through the standard SweetAlert confirm.

| Action | Dialog |
|---|---|
| Start | "Start this job? Start only once you have reached *the address*. The customer is told the work has begun, and the job cannot go back to accepted." |
| Complete | "Mark this job complete? *Amount* will be credited to your balance and the customer will be asked to rate you. This cannot be undone." |

Both name the consequence rather than asking "are you sure" — starting is visible to the customer,
and completing posts money to the ledger and cannot be reversed.

---

## 2026-09-06 — A typed password is the one that gets set

**Asked:** typing into "Or type one" should set exactly that password; leaving it blank should
generate one. It was not working.

**The bug.** The form carried a "Generate a strong password" checkbox that defaulted to checked, and
the service read `request.Generate || string.IsNullOrWhiteSpace(request.NewPassword)`. The checkbox
won, so a password the administrator had typed was silently thrown away and a random one was set
instead.

**Rule.** The field alone decides, and the checkbox is gone.

| What the administrator does | What happens |
|---|---|
| Types a password | That exact password is set |
| Leaves it empty | A strong one is generated and shown once |
| Types a weak password | Refused with the password rule; **nothing changes** |

A generated password is still shown once and never stored. When the password was typed, the
confirmation does not repeat it — the administrator already has it.

**Still to come:** emailing the new password to the account holder instead of reading it out. That
needs the mail sender, which does not exist yet.

---

## 2026-09-06 — Commission plans, and tax as a country-aware master

**Asked:** let a partner take a plan that earns with zero commission, manage those plans from the
admin portal, and deduct GST per government rules through a master that would also work in another
country.

### Commission plans

`CommissionPlans` is the master, at `/admin/masters/plans`. Any administrator may look; only a
super admin may add or change one.

| Field | Meaning |
|---|---|
| `CommissionPercent` | What the platform keeps per job. **Zero is the subscription model** |
| `SubscriptionFee` + `BillingPeriod` | A recurring fee. Zero for a pure commission plan |
| `IsDefault` | Where a partner lands with no plan of their own. Exactly one plan holds it |

Seeded: **Standard** 15% and no fee, **Lite** 8% with ₹499 a month, **Zero commission** 0% with
₹1,499 a month.

`PartnerPlanSubscriptions` records who is on what, and when. Only one row is active per partner. A
plan with a billing period gets an `EndsOn`; once that passes the partner falls back to the default
plan rather than keeping a discount they have stopped paying for.

Assigning a plan optionally debits the fee immediately as a `subscription_fee` ledger line.

### Tax

`TaxRules` is the master, at `/admin/masters/taxes`. One rule is **a percentage of one base, for one
country, between two dates**. That shape is what makes it portable: any country's deduction on a
marketplace payout fits it.

| Field | Meaning |
|---|---|
| `CountryCode` | Only rules for `Platform:CountryCode` are applied |
| `AppliesTo` | `commission`, `gross_earning` or `subscription_fee` |
| `Percent` | The rate |
| `ThresholdAmount` | Skip the rule below this amount |
| `EffectiveFrom` / `EffectiveTo` | When the rule was law |

Seeded for India: **GST 18% on the commission** (the platform sells a service to the partner, so GST
is on the fee, not the job), **GST 18% on the plan fee**, and **TDS 1% on the gross** under section
194-O. The rates are a starting point and the note on each says to confirm them before going live.

**Running in another country is a data change, not a code change.** Add rules with that
`CountryCode` and set `Platform:CountryCode` in configuration.

### The two rules that matter

**The rate is snapshotted onto the ledger when the job settles.** `LedgerEntry` carries
`AppliedPercent`, `BaseAmount`, `CommissionPlanId` and `TaxRuleId`. Moving a partner to a new plan,
or a government changing GST, must never alter what a finished job earned.

**A rate change is a new rule, not an edit.** End the old rule with `EffectiveTo` and add a new one
starting the next day. Editing the percentage in place would misstate what past jobs should have
deducted. The tax form says so.

### What a completed job now posts

A ₹4,500 job on the Standard plan:

| Line | Amount | Balance |
|---|---|---|
| Job earning | +4,500.00 | 4,500.00 |
| Platform commission at 15% | −675.00 | 3,825.00 |
| GST on commission at 18% of 675 | −121.50 | 3,703.50 |
| TDS at 1% of 4,500 | −45.00 | 3,658.50 |

The same job on the Zero commission plan posts the earning and the TDS only — there is no commission,
so there is no GST on it either, because a tax whose base is zero is skipped.

---

## 2026-09-06 — Partner earnings run on an append-only ledger

**Asked:** a partner earnings tab with the order details behind each amount, and the money ledger,
straight away.

**Rule.** `LedgerEntries` is **append-only**. A row is never updated or deleted; a correction is a
new row. `PartnerWallets.Balance` is a cache of the ledger, written in the same transaction.

| Entry type | Direction | Raised when |
|---|---|---|
| `job_earning` | credit | The task moves to `completed`, for the agreed amount |
| `platform_commission` | debit | Same moment, at the rate of the partner's plan |
| `tax` | debit | Same moment, one line per tax rule in force |
| `subscription_fee` | debit | When a plan with a fee is assigned |
| `payout` | debit | An administrator records a bank transfer that has already gone out |
| `adjustment` | credit or debit | Super admin only, and a reason is required |

- **Gross, commission and each tax are separate lines.** The partner always sees the full amount the
  customer agreed. The rate comes from the commission plan — see the plans and tax entry above.
- **Every entry carries an `IdempotencyKey` with a unique index.** A job earning is keyed
  `job_earning:{taskId}`, so completing the same task twice cannot pay twice. A payout is keyed on
  the bank reference, so a retry with the same UTR is refused. This is what makes the ledger safe to
  connect a payment gateway to later.
- A check constraint enforces `Amount > 0`; direction carries the sign, never the amount.
- A payout larger than the balance is refused, as is an adjustment that would push the balance
  negative.
- **Recording a payout does not move money.** The administrator transfers through the bank first and
  records it here with the reference.

Partner sees it at `/provider/earnings`, with each row carrying the task, its category, the customer
and the address. Admin sees what the platform owes at `/admin/payouts`, and one partner's statement
at `/admin/payouts/{id}`.

---

## 2026-09-06 — Unhandled errors get a reference the user can quote

**Asked:** the same error handling as the Falcon project.

**Rule.** `GlobalExceptionFilter` catches everything that escapes an action and writes one
`ErrorLogs` row with a short reference such as `E260906-A3F91C`.

- `/api/*` and XHR requests get a `ProblemDetails` with the reference in `extensions.reference`.
- Portal pages redirect to `/Home/Error?reference=...`, which shows the reference and nothing else.
- **The message and stack trace never reach the user.** They ask about the reference; the
  administrator searches for it at `/admin/errors`, which is super admin only.
- A cancelled request is not an error and is not logged.

**The log writes through its own DbContext scope.** The request's context is usually the thing that
just failed — a rolled-back transaction cannot save anything more — so reusing it silently loses
every database error. This was found in testing: the filter redirected correctly but nothing was
written.

Resolving an entry keeps the row and takes it off the open list.

---

## 2026-09-06 — Toasts, and the customer portal is permanent

**Asked:** one reusable `showToast()` matching the supplied design, used everywhere. And the
customer portal is not throwaway.

**Rule.** `showToast(title, message, kind)` in `global.js` renders a card with a coloured left bar,
a round icon and a title over the message. Kinds are `success`, `warning`, `error` and `info`.
Passing a null title uses the kind's own word. `App.toastSuccess(message)` and friends are the short
form. Every `TempData["Success"]` and `TempData["Error"]` now surfaces as a toast through `_Flash`.

It is plain CSS, not SweetAlert. SweetAlert stays for `confirmAction` and `notify`, where a modal is
the point.

**The customer web portal is permanent, not a test harness.** Local services are found through
search — "plumber in Gurugram" is free acquisition that a Flutter app cannot receive, because an app
is not indexed. Razor server-rendered pages are better for that than a single-page app, so the
customer portal stays and grows public category and city landing pages. The partner portal can still
be replaced by the app, since partners do not arrive through search.

---

## 2026-09-05 — The admin sidebar is a master, not markup

**Asked:** build the menu dynamically from a table the admin manages, with CRUD, so new screens can
be added without a developer.

**Rule.** `MenuItems` drives the admin sidebar. Managed at `/admin/masters/menu`, **super admin
only** — a plain administrator can neither see the screen nor reach it.

| Column | Meaning |
|---|---|
| `Label` | What the sidebar shows |
| `ParentId` | Null for a top-level row. The sidebar is **two levels deep**, no more |
| `ControllerName` + `ActionName` | The target. Resolved with `Url.Action`, so attribute routes work |
| `Url` | An alternative target for links outside the admin controller, such as `/swagger` |
| `Icon`, `SortOrder`, `IsActive`, `OpensInNewTab` | Presentation |
| `Visibility` | `all` or `super_admin` |
| `BadgeKey` | Optional counter. Only `pending_kyc` is wired today |

- **A row with no controller, action or URL is a group heading.** That is how "Masters" and
  "Approvals" exist without being links.
- **A link cannot point at nothing.** On save the controller and action are checked against
  `IActionDescriptorCollectionProvider`, so a typo is refused rather than shipped as a dead link.
- A group with children cannot be turned into a link, and a group cannot sit inside a group.
- Deleting a group with children is refused; hide it with `IsActive` instead, which keeps the row.
- The current sidebar is seeded on first run, so nothing was lost in the move.

**Adding a badge needs code.** `BadgeKey` names a counter the application knows how to compute;
add the constant to `MenuBadgeKeys` and resolve it in `_AdminLayout`. An unknown key is refused on
save rather than rendering a silent zero.

---

## 2026-09-05 — Navbar carries the account menu

**Asked:** the name and photo in the navbar should open a small box with Profile on the left and
Logout on the right, with a notification icon beside it.

**Rule.** `_UserMenu` renders both, in the admin sidebar header and in the customer and partner
navbar. The avatar falls back to the first letter of the name when no photo is uploaded.

The **notification bell only renders where there is a real number behind it**. Today that is the
admin, where it counts partners waiting for KYC review and links to the approvals queue. The
customer and partner navbars show the account menu without a bell, because there is no notification
feed yet — a bell that always reads zero is worse than no bell. It comes with the notification
system.

---

## 2026-09-05 — A customer is told when their partner changes skill

**Asked:** if a partner has accepted a job and then changes skill, tell that customer on the task.

**Rule.** On the customer's task list, a task whose `CategoryId` no longer matches the assigned
partner's `SkillCategoryId` carries: "This partner has changed their skill to Electrical since
accepting your task. They are still expected to finish it as agreed."

**It only shows while the work is still open.** Once the task is completed or cancelled the note
disappears — the skill no longer matters on a job nobody is going to do.

It is derived, not stored — `GigTaskDto.PartnerChangedSkill` compares the two category ids and
checks `GigTaskStatus.IsOpen`, so it also clears itself if the partner changes back. Only customers
who actually have that partner assigned see it; nobody else is told anything.

The partner side follows the same rule: `_MyJobs` flags a mismatched job only while it is open.

---

## 2026-09-05 — Profile is section based, and KYC moved into it

**Asked:** move the KYC details out of the partner dashboard into the profile, give the profile a
left-side menu, and add a bank account section.

**Rule.** `/{portal}/profile` is now a shell with a left menu. Sections:

| Section | Route | Who sees it |
|---|---|---|
| Profile | `/{portal}/profile` | everyone |
| Account details | `/{portal}/profile/bank` | customers and partners |
| My addresses | `/{portal}/profile/addresses` | customers and partners |
| KYC | `/provider/profile/kyc` | partners only |
| Manage devices | — | **not built yet**, see Scope below |
| Settings | `/{portal}/profile/settings` | everyone |

- Password change moved from the profile form into **Settings**.
- **An administrator gets neither Account details nor My addresses** — they are never paid and never
  booked. The menu hides them and the routes redirect back to the profile, so a typed URL does not
  reach a section that means nothing for that role.
- The address book moved from its own page into the **My addresses** section. `/{portal}/addresses`
  still answers, but redirects there, so old links keep working. The POST routes did not move.
- Earnings moved the same way, into **My earnings**. `/provider/earnings` redirects to it.
- **The navbar carries no section buttons.** Everything that belongs to an account is reached from
  the profile menu, so Addresses and Earnings were removed from the top bar rather than duplicated.
- **Bank account**: one per user, stored in `BankAccounts` with a unique `UserId`. Fields are
  account holder name, account number, IFSC, bank name, branch and an optional UPI id. IFSC is
  validated as `^[A-Z]{4}0[A-Z0-9]{6}$`; the account number is stored as typed and shown masked
  except the last four digits. It is needed for partner payouts and customer refunds, so it hangs
  off `User`, not `Partner`.
- API: `GET|PUT /api/profile/bank`.

**Scope — manage devices (not built).** Intended to list the sessions or devices signed in to an
account and let the user revoke one. It cannot be built yet: JWTs are stateless and last seven
days, with no refresh-token or session table to revoke against. Build it together with refresh
tokens, otherwise "revoke" would be a button that does nothing.

---

## 2026-09-05 — An unapproved partner sees only their KYC status

**Asked:** a partner whose KYC is not approved should see only a greeting and the KYC status — no
available work, no bids. Jobs they had already accepted stay visible.

**Rule.** `GET /provider` returns the trimmed dashboard whenever `KycStatus != approved`. Available
work and My bids are not rendered at all, rather than rendered empty. The partner gets the status
card, the reason if rejected, and a link to the KYC section of their profile.

**Jobs already accepted are the exception.** A partner sent back to `pending` — usually by a skill
change — keeps the **My jobs** section, showing only tasks that are not yet completed or cancelled.
Without this a customer would be stranded halfway through a booking the partner had already agreed
to. Completed and cancelled history stays hidden until they are approved again.

A job whose `CategoryId` no longer matches the partner's `SkillCategoryId` carries a line saying so:
"Booked under Painting, which is no longer your skill. Finish it as agreed — you cannot take new
work in this category." It is informational only; Start, Complete and Cancel all still work on that
job, and it disappears once the job is completed or cancelled.

The API already refuses the same actions, so mobile can rely on `partner.isVerified` from
`GET /api/partners/me` to decide which screen to show, and on `task.categoryId` against
`partner.skillCategoryId` to show the same mismatch line.

---

## 2026-09-05 — Approving a previously rejected partner asks for confirmation

**Asked:** if an admin already rejected a KYC and then clicks Approve, warn them first.

**Rule.** The Approve button on the KYC modal carries a confirmation only when
`KycStatus == rejected`. The dialog repeats the rejection reason that was given, so the
administrator is reminded what they refused before reversing it. Approving a partner who was never
rejected still needs no confirmation.

This is a portal-only guard. The API (`PUT /api/partners/{id}/verify`) does not ask, because a
confirmation is a user-interface concern, not a rule the server can enforce.

---

## 2026-09-05 — KYC history comes from the existing audit trail

**Asked:** show the full KYC history in the review modal, and build a log table if one does not
exist.

**A log table already exists** — `TrackingLogs`, added earlier in this project. It carries exactly
the columns that were asked for: `Id`, `TransactionDate`, `DocNo`, `DocDate`, `EntryType`
(insert / update / delete), `Payload` (jsonb request and result) and `Remark`. It is filled
automatically by `TrackingActionFilter`, a global MVC filter, so no controller has to remember it.
See **C# — audit trail** in [REUSABLE.md](REUSABLE.md).

**Rule.** Every write that touches a partner's KYC is tagged `[TrackForm("Partner")]` with the
**partner id** as `DocNo`. That is what makes a single-partner history possible:

| Event | Where |
|---|---|
| Registered with documents | `AuthController.RegisterPartner`, `ProviderController.Register` |
| Documents re-uploaded | `ProviderController.SubmitKyc`, `PartnersController.SubmitKyc` |
| Skill changed | `ProviderController.UpdateSkill`, `PartnersController.UpdateMyProfile` |
| Approved or rejected | `AdminController.SetVerification`, `PartnersController.SetVerification` |

`IKycHistoryService.ForPartnerAsync` reads those rows and turns each one into a sentence with the
status it produced. Nothing is written twice — the history is a **read** over the audit trail, not
a second table to keep in sync.

**When adding a new KYC action, tag it `[TrackForm("Partner")]` and call `TrackDoc(partner.Id, …)`,
or it will not appear in the history.**

---

## 2026-09-04 — One identifier may hold one account per role

**Asked:** the same email or phone should be able to hold a partner account and a customer account,
but not two accounts of the same role.

**Rule.** Unique indexes are `(Phone, Role)` and `(Email, Role)`. The rows are separate `User`
records with their own password and history; nothing is shared between them.

**Mobile must send the role on login.** `LoginRequest.Role` decides which account is meant. Without
it the password is checked against every candidate: one match signs in, more than one is refused
with "sign in from the portal for the account you want". The customer app sends `customer`, the
partner app sends `partner`.

---

## 2026-09-04 — Changing skill sends an approved partner back for review

**Asked:** warn the partner that changing their skill needs KYC approval again, and tell the
administrator why the partner reappeared in the queue.

**Rule.** `PartnerKyc.ChangeSkill` is the only place this happens.

- Approved partner changes skill → `KycStatus` returns to `pending`, and they cannot accept work.
- Not-approved partner changes skill → status is untouched; they were already waiting.
- Same skill re-submitted → nothing happens at all.
- `Partner.KycReviewNote` records "Skill changed from Plumbing to Electrical on 04 Sep 2026" so the
  administrator knows what to re-check. `PartnerKyc.Review` clears it once decided.

Mobile shows the same confirmation before calling `PUT /api/partners/me`.

---

## 2026-09-04 — KYC is a four-state column

**Asked:** an administrator could not see that they had already rejected someone, and the partner
was never told why.

**Rule.** `Partner.KycStatus` is `not_submitted → pending → approved | rejected`.

- Rejecting **requires a reason**. It is the only thing the partner is shown.
- Any resubmission returns the status to `pending` and clears the reason, so a rejected partner
  rejoins the queue without creating a new account.
- The admin badge counts only `pending`. A rejected partner is waiting on themselves.
