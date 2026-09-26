# Reusable helpers

Check this file before writing anything new. If a helper already exists here, **use it — do not write
it again**. When you add a new reusable piece, add one line for it here.

---

## Portals are moving to JSON API + page JS — Admin is done, copy its shape

Every portal page is now a thin HTML shell that fetches from a JSON API and renders client-side —
Admin, Provider's dashboard and its own profile sections, the profile sections every portal shares,
Customer's own sections, Provider's Register, every portal's Login/OTP screens, and the storefront's
cart and checkout. Skill categories was the first page converted; the shape below is what every
later page copies rather than re-deciding the pattern. Only the storefront's first paint
(`/services` and friends) stays server-rendered, deliberately, for SEO.

- **The MVC controller action returns an empty `View()`.** No ViewModel — the JSON API is the only
  data source. `AdminController.Categories`/`NewCategory`/`EditCategory` are the reference.
- **One API controller per master, `Save` does both create and update.** `SkillCategoriesController`
  (`api/skillcategories`) — `SaveXxxRequest.Id` is `null` for create, set for update; the action
  branches on that instead of having two near-identical methods. `[ApiController]`, same
  `[Authorize(Policy=...)]` the MVC action used, **no `[ValidateAntiForgeryToken]`** — the `gigapp_token`
  cookie is `SameSite=Lax`, same as every other API controller.
- **Check for an existing service before writing the controller.** `CommissionPlansController` and
  `TaxRulesController` are thin wrappers over `IPlanService`/`ITaxService`, which already held every
  rule — the AdminController actions being replaced were themselves just calling into that service.
  Only write validation/persistence logic in the new controller when the old MVC action had it
  directly (Categories, Services, Banners did; Plans and Taxes did not).
- **A page's JS lives inline, in that page's own `@section Scripts { <script>...</script> }`
  block** — not a separate `.js` file. Open `Categories.cshtml` and its whole client-side behaviour
  is right there in the same file as its markup; nothing to go hunting for in `wwwroot/js/`. This is
  the answer to "which file has this page's logic" — it's the view you're already looking at.
- **Code shared by more than one page goes in `wwwroot/js/global.js`**, under `App.api` or the
  `Admin` namespace (below) — never copy-pasted between two pages' inline scripts. If a third page
  is about to repeat something two others already do, that's the signal to promote it into
  `global.js`, not to write it a third time.
- **A checkbox posted through `FormData` (or `Admin.formToJson`) needs an explicit `value="true"`.**
  A plain `<input type="checkbox">` with no `value` submits the literal string `"on"` when checked,
  which a `bool` model binder rejects — `asp-for` hides this by generating a hidden companion input,
  but a shell view has no `asp-for`. Always write `<input type="checkbox" name="X" value="true" checked>`.
- **A partial nested inside another partial cannot use `@section Scripts`.** The layout only
  collects sections declared by the top-level view it renders directly; a section declared deeper
  (e.g. a profile section partial reached through `_ProfileBody`) is silently invisible to it. Write
  the `<script>` inline in the partial as usual, but wrap it in
  `document.addEventListener('DOMContentLoaded', function () { ... })` instead of an
  immediately-invoked `(function () { ... })()` — the layout puts jQuery/bootstrap/`global.js`
  *after* `@RenderBody()`, so a script that touches `$` immediately at parse time fails with
  `$ is not defined`; by `DOMContentLoaded` every script tag before `</body>` has already run. A
  top-level view's own `@section Scripts` block never needs this — it already runs after those tags.

### `App.api` — `wwwroot/js/global.js`

The only way a page's inline script talks to the server. Defined in `global.js`, so it's available
everywhere `global.js` is loaded — every portal, no extra `<script>` tag needed.

| Helper | Signature | Purpose |
|---|---|---|
| `App.api.get` | `.get(url, opts?)` → `Promise<body>` | JSON GET. |
| `App.api.post` | `.post(url, data, opts?)` → `Promise<body>` | `data` is a plain object (sent as JSON) or a `FormData` (sent as multipart — use `new FormData(formEl)` for a form with a file input, name inputs to match the DTO's properties). |
| `App.api.put` | `.put(url, data, opts?)` → `Promise<body>` | Same `data` rules as `post`. |
| `App.api.del` | `.del(url, opts?)` → `Promise<body>` | |

A non-2xx response rejects the promise and shows the server's error via `showToast` automatically.
Pass `opts = { silent: true }` to suppress that toast when you're showing the error inline instead
(a form does this; a list-page action usually just wants the toast).

### `Admin` namespace — `wwwroot/js/global.js`

Despite the name, `global.js` loads on every portal's layout, so this namespace is available to any
page's inline script — Provider's dashboard uses `Admin.escapeHtml` the same way an Admin master
does. Add to this object the moment a second page needs the same thing — never redefine it inline
in a page.

| Helper | Signature | Purpose |
|---|---|---|
| `Admin.escapeHtml` | `.escapeHtml(text)` → `string` | HTML-escapes text before it goes into a template-literal row you build yourself. |
| `Admin.thumb` | `.thumb(url)` → `string` | The `_MasterThumb` partial's markup (image or "none"), for a JS-built table row. |
| `Admin.showImagePreview` | `.showImagePreview(url, fieldName?)` | Points an already-rendered `_ImageUpload` widget at an existing image — call after fetching a record to edit, so the preview shows what's already saved. Omit `fieldName` when the page has exactly one `_ImageUpload`; pass it (the widget's `Name`, e.g. `"Selfie"`) when a page has more than one, so each gets its own image instead of all of them showing the last call's URL. |
| `Admin.showFormErrors` | `.showFormErrors(containerSelector, message)` | Renders a red inline alert into the given container — the standard "save failed" display under a form's heading. |
| `Admin.formToJson` | `.formToJson(formEl)` → `object` | Serializes a form to a plain object for `App.api.post`/`.put`, including every checkbox's real `true`/`false` (not just the checked ones `serializeArray` would give you). |
| `Admin.openKycModal` | `.openKycModal(partnerDto, historyEntries[], onDecision)` | Populates and shows `#kyc-modal` (the shared skeleton in `_KycModal.cshtml`) — review tab, history tab, Approve/Reject wiring including the "you rejected this partner before" confirm and the incomplete-KYC-disables-Approve state. `onDecision(approved, reason)` is called when a decision button is clicked; call `PUT api/partners/{id}/verify` from there. Shared by `Partners.cshtml` and `Approvals.cshtml` — the one JS helper big enough that two inline `<script>` blocks would otherwise duplicate real logic. |
| `Admin.openAccountModal` | `.openAccountModal(userDto, onSaved?)` | Populates and shows `#account-modal` (the shared skeleton in `_AccountModal.cshtml`) — reset-password and activate/deactivate forms, both wired to `POST api/users/{id}/reset-password` / `.../active` internally. Call `onSaved` (usually the page's own `load()`) to refresh the list after a status change. Shared by `Users.cshtml` and `Partners.cshtml`. |
| `Admin.openAddressModal` | `.openAddressModal(addressDto \| null, onSaved)` | Populates and shows `#address-modal` (the shared skeleton in `_AddAddressModal.cshtml`, wrapping the static `_AddressForm.cshtml`) — pass `null` for "Add address", an `AddressDto` for "Edit". Wires the form's submit to `POST`/`PUT api/addresses` (branching on whether the address has an id) and converts the hidden lat/lng inputs' empty string to `null` before sending. Call `onSaved(saved)` to refresh whatever list or dropdown opened it. Shared by `_ProfileAddresses.cshtml` (My addresses) and `_ProfilePostTask.cshtml` ("Add another address" inline) — the coupling that once made Addresses get deferred past Post-a-task's conversion. |

## JavaScript — `wwwroot/js/global.js`

Loaded by both layouts. jQuery, jQuery UI, the Bootstrap bundle and SweetAlert2 load before it.

**Never call `alert()`, `confirm()` or `prompt()`.** Everything goes through the helpers below.

| Helper | Signature | Purpose |
|---|---|---|
| `AutoComplete` | `AutoComplete(inputId, hiddenId, url, alertMsg, onSelect)` | Binds a text box to a hidden id field. Text box holds the **name**, hidden field holds the **id**. If nothing is picked from the list the hidden field stays empty, so the server rejects it. |
| `App.masterPicker` | `App.masterPicker(inputId, hiddenId, masterKey, alertMsg, onSelect)` | Shortcut for the above; builds the master URL itself. |
| `App.masterUrl` | `App.masterUrl(key)` → `/api/masters/{key}?term=` | Search URL for a master. |
| `showToast` | `showToast(title, message, kind)` | Corner toast: coloured bar, icon, title over message. `kind` is `success`, `warning`, `error` or `info`. A null title uses the kind's own word. |
| `App.toastSuccess` and friends | `App.toastSuccess(message, title?)` | Short form for the four kinds. Also `toastError`, `toastWarning`, `toastInfo`. |
| `App.confirmAction` | `App.confirmAction(textOrOptions)` → `Promise<bool>` | SweetAlert confirm. Options: `title`, `text`, `icon`, `confirmText`, `cancelText`. |
| `App.notify` | `App.notify(textOrOptions)` → `Promise` | SweetAlert message box, one OK button. Replaces `alert()`. |

### Autocomplete — the easy way (no JavaScript needed)

Add `data-*` attributes and `global.js` wires the field automatically:

```html
<label for="CategoryPicker" class="form-label required">Category</label>
<input id="CategoryPicker" class="form-control"
       data-master="skill-category"      <!-- master key -->
       data-target="CategoryId"          <!-- id of the hidden field -->
       data-alert="category" />          <!-- toast text for an invalid name -->
<input type="hidden" id="CategoryId" name="CategoryId" value="" />
```

To show an existing value on an edit form, set `value="@Model.CategoryName"` on the visible input and
`value="@Model.CategoryId"` on the hidden one. The script syncs them on load.

Call it manually only when you need `onSelect` — for example, filling a second field from the picked row:

```js
App.masterPicker('CityPicker', 'CityId', 'city', 'city', function (item) {
    $('#StateId').val(item.stateId);
});
```

### Other global behaviours (automatic)

| Attribute | Put it on | Effect |
|---|---|---|
| `data-confirm="..."` | `<form>` | SweetAlert confirm before submit. Use for delete, cancel and anything with a consequence. |
| `data-confirm-title` | `<form>` | Heading for that dialog. Defaults to "Please confirm". |
| `data-confirm-ok` | `<form>` | Confirm button text. Defaults to "Yes, continue". |
| `data-confirm-icon` | `<form>` | `warning` (default), `question`, `info` or `error`. |
| `data-confirm-changed="fieldId"` | `<form>` | Skip the dialog while that field still holds its original value. Use it so an unchanged form does not nag. |
| `data-auto-submit` | filter `<form>` | Submits the form as soon as a `<select>` inside it changes. |
| `data-price-field="fieldId"` | picker `<input>` | When the picked master row carries `Extra["fixedPrice"]`, that price is written into the field and the field goes read-only. Clearing the picker gives the field back. |
| `data-price-note="noteId"` | picker `<input>` | Element whose text is swapped while a fixed price is in force. Its original text is restored on clear. |
| `data-price-fixed-note="..."` | picker `<input>` | The text to show there while the price is fixed. |

The confirm handler cancels the first submit, waits for the dialog, then re-submits — carrying the
clicked button name and value, so a form with two named submit buttons keeps working.

```html
<form method="post" asp-action="UpdateSkill"
      data-confirm-title="Change your skill?"
      data-confirm="@PartnerKyc.SkillChangeWarning"
      data-confirm-ok="Yes, change my skill"
      data-confirm-changed="skillCategoryId">
```

### Login / OTP (`wireLoginForm`, `wireOtpAuthForm`)

`_LoginCard` and `_OtpAuthForm` are shared by every portal's Login page, so there is no per-page
script to attach these to — just add the markup and `global.js` wires it on load.

| Attribute | Put it on | Effect |
|---|---|---|
| `data-login-form` | `<form>` | Posts to the current page's own URL (`window.location.pathname`) as JSON, redirects to `body.redirectTo` on success, shows the error inline via `#login-form-errors` on failure. Works from anywhere on the page — including the inline password step below — since it is delegated off `document`. |
| `data-otp-auth` | wrapping `<div>` | Scopes one phone → code → password flow. Everything below is found inside it. |
| `data-otp-step="phone"` / `="code"` / `="password"` | the three step `<div>`s | Toggled with `hidden`; only one shows at a time. |
| `data-otp-request-form` | `<form action="...otp/request">` | Sends `{Phone, ReturnUrl}`, then reveals the code step and fills `data-otp-phone-label`/`data-otp-phone-field`/`data-otp-dev-code`. |
| `data-otp-verify-form` | `<form action="...otp/verify">` | Sends the form as JSON (see the `""` vs `null` gotcha above), redirects to `body.redirectTo` on success. |
| `data-otp-phone-field` | hidden `<input name="Phone">` in the code step | Filled by the request step; carried along on verify. |
| `data-otp-resend` | `<button>` | Re-sends to the phone already in `data-otp-phone-field`. |
| `data-otp-change-number` | `<button>` | Switches back to the phone step, client-side, no reload. |
| `data-otp-use-password` | item inside the `#otp-more-options` modal | Copies the phone from `data-otp-phone-field` into the password step's hidden `Identifier` and its `data-otp-password-phone-label`, then swaps the code step for the password step. The modal itself closes via its own `data-bs-dismiss="modal"`. |
| `data-otp-password-back` | `<button>` in the password step | "Use a code instead" — swaps back to the code step, client-side. |

**"More options" on the OTP code step opens a small modal (`#otp-more-options`), not a page
navigation** — matching the Uber/Microsoft pattern of a "choose another way to sign in" dialog
rather than a dead-end link. Today it lists exactly one entry, "Sign in with password" ( since a
real SMS/WhatsApp/email OTP channel is not built yet — see `LoggingOtpSender` under "Not done
yet"); adding a further delivery channel later is one more `list-group-item` in the modal plus
whatever `data-otp-...` attribute wires its own step, following the same shape as the password
one. The "More options" trigger stays a real `<a href="@Model.PasswordFallbackPath">` under the
`data-bs-toggle="modal"` attributes, so it still degrades to the classic full-page password form
at `/{portal}/login?mode=password` if Bootstrap's JS has not loaded.

### Storefront cart (`wireCartControls`)

Works anywhere this markup appears — a service tile, the cart page, the checkout sidebar — because
every handler is delegated off `document` and re-renders from the fresh `CartViewDto` `api/cart`
hands back.

| Attribute | Put it on | Effect |
|---|---|---|
| `data-service-card` / `data-service-id` | the tile wrapper | Identifies which service `data-add-to-cart` inside it means. |
| `data-add-to-cart` | `<button>` | Adds one unit, then swaps itself for a `_QuantityStepper` inside its `data-cart-control` sibling. |
| `data-cart-control` | container next to the price | Holds either the "Add" button or the stepper — `applyCartToPage` rewrites its contents. |
| `data-qty-stepper` / `data-service-id` | `_QuantityStepper`'s own root | `data-qty-decrement`/`data-qty-increment` change the quantity by one; reaching 0 removes the line (same rule as `ICartService.SetQuantityAsync`). |
| `data-cart-line` / `data-service-id` | a whole cart/checkout row | Removed from the DOM when its line is gone from the fresh cart. |
| `data-cart-remove` | `<button>` inside a `data-cart-line` | Deletes that line outright. |
| `data-line-total` | inside a `data-cart-line` | Kept in sync with that line's total. |
| `data-cart-summary` | a `<dl>` | Fully regenerated (all lines) on every cart change — the checkout/cart payment summary. |
| `data-cart-summary-total` | anywhere | Text set to the cart's grand total — used by both the summary panel and the checkout button's own label, since both should show the same number. |
| `data-cart-badge` / `data-cart-badge-count` | the header cart icon | Hidden when the cart is empty; count kept in sync. |
| `data-cart-bar` / `data-cart-bar-total` / `data-cart-bar-count` | the sticky cart bar | Same idea, for the bar shown on the tile-grid pages. |

Every mutation ends by firing `cart:updated` on `document` with the fresh `CartViewDto` — a page
with its own edge case (Cart.cshtml reloads if the cart just became empty; Checkout.cshtml redirects
to `/cart` for the same reason) listens for that event in its own `@section Scripts` rather than
teaching the generic handler about page-specific navigation.

---

## C# — masters

A single endpoint serves every master: **`GET /api/masters/{key}?term=&limit=`**
The response is always `{ success, data: [{ id, name, hint, extra }] }`, where `extra` is an optional
string dictionary the picking form can react to — `assignable-partner` and `service-item` both use it.

Override `Roles` on the source when the master carries anyone's personal details; the endpoint is
open to every signed-in user otherwise. `assignable-partner` is admin-only for exactly that reason.

**Adding a new master takes two steps:**

```csharp
// 1. Services/Masters/CityMaster.cs
public class CityMasterSource : IMasterSource
{
    public string Key => "city";
    public async Task<IReadOnlyList<MasterItemDto>> SearchAsync(string? term, int limit, CancellationToken ct)
        => /* active rows, filtered by term, capped at limit */;
}

// 2. Program.cs
builder.Services.AddScoped<IMasterSource, CityMasterSource>();
```

That is all. No new controller and no new JavaScript — `data-master="city"` works immediately.

`ICategoryLookup` (`Services/CategoryLookup.cs`) is also available for server-rendered `<select>`
lists and for validating an id:

| Method | Purpose |
|---|---|
| `GetActiveOptionsAsync()` | Active categories only. |
| `GetOptionsIncludingAsync(selectedId)` | Active categories plus the currently selected one, even if it was deactivated. |
| `IsSelectableAsync(id)` | Server-side check before a write. **Do this on every write.** |

`IServiceItemLookup.GetBookableAsync(serviceItemId, categoryId)` is the service-item equivalent. It
validates the pair and returns the row in one query, so the booking path never needs a second one:

```csharp
var service = await _serviceItems.GetBookableAsync(request.ServiceItemId, request.CategoryId, ct);
if (service is null) return BadRequest(/* the pair is invalid */);

var mode = service.IsInstant ? TaskBookingMode.Instant : TaskBookingMode.Bidding;
var amount = service.IsInstant ? service.FixedPrice : request.Budget;
```

### Taking a task — `Services/Booking/TaskClaimService.cs`

| Method | Who | Rule |
|---|---|---|
| `ClaimAsync(partnerUserId, taskId)` | A partner | Instant tasks only, own category, approved KYC. First accept wins. |
| `AssignAsync(adminUserId, taskId, partnerId, note)` | Support | Any pending task. Note required. Closes open bids. |

Both return `TaskClaimResult`, whose `Outcome` maps straight onto HTTP: `Claimed` 200, `NotFound`
404, `Taken` 409, `NotAllowed` 403. Portals just read `Succeeded` and `Error`.

### Orders and support — `Services/Orders/`, `Services/Support/`

| Method | Purpose |
|---|---|
| `IOrderHistoryService.ForUserAsync(userId, role, paging, status)` | Every order that person was on, paged and filtered. The role picks the side; nothing else differs between the two portals. |
| `ISupportService.RaiseAsync(userId, role, taskId, request)` | Raise an enquiry against one order. Enforces ownership, the ten-character minimum and one open enquiry per person per order. |
| `ISupportService.ReviewAsync(adminUserId, enquiryId, request)` | Move it to in-progress, or resolve it with a required reply. |
| `ISupportService.LatestForTasksAsync(userId, taskIds)` | That person's newest enquiry per order — for showing status instead of a "Need help?" button. |
| `ISupportService.ListAsync(paging, status)` | The admin queue: open first, then in-progress, then resolved. |
| `ISupportService.OpenCountAsync()` | The sidebar badge. Counts open **and** in-progress. |

`ISupportService.RaisedMessage` is the confirmation text. Use it rather than retyping the wording.

### Storefront and cart — `Services/Storefront/`

| Method | Purpose |
|---|---|
| `ICartService.Read()` | The raw lines — ids and quantities only. Use it for the header badge. |
| `ICartService.Add(id, qty)` / `SetQuantity(id, qty)` / `Clear()` | Mutations. A quantity of zero removes the line. |
| `ICartService.PriceAsync()` | The cart with names, images and prices resolved from the database. **Never price a cart from what the browser sent.** |
| `ICheckoutService.PlaceAsync(customerId, addressId, note, preferredAt)` | Turns the cart into one instant task per line, starts each offer chain, and empties the cart. |

Storefront views use `_ShopLayout` (its own header with search and the cart badge, no portal navbar).
Pieces: `_ShopHeader`, `_ServiceCard` (set `ViewData["CartQuantity"]` before rendering — decides
whether it shows "Add" or a stepper on **first paint**; `wireCartControls` takes over from there),
`_QuantityStepper` (`QuantityStepperModel`), `_CartBar` for the sticky total, and `_SpotlightCard` /
`_WideBanner` for promotions. All of it is static, data-attribute-driven markup now — see
`wireCartControls` above for the live-update wiring, none of which lives in these partials
themselves.

Section markup on the home page follows one shape — wrap in `.shop-section`, put the heading and an
optional `See all` in `.shop-section-head`, and put cards in `.shop-rail` (scrolls sideways on a
phone) or `.shop-grid` (wraps). **Render nothing at all when the list is empty** — a heading with no
cards under it looks broken.

### Master images — `IHasImage`

A master row that carries catalogue artwork implements `Models.IHasImage`, and the admin controller
saves it through one helper:

```csharp
var error = await ApplyImageAsync(form.Image, entity, FileCategory.ServiceImage, ct);
if (error is not null) { ModelState.AddModelError(nameof(form.Image), error); return View(...); }
```

It validates through `IFileStorageService`, puts the new name on the row and deletes the file it
replaced. Render the form field with `_ImageUpload` and the list column with `_MasterThumb`
(model is the URL, or null for the "none" placeholder). **The form needs `enctype="multipart/form-data"`.**

### Notifications — `Services/Notifications/`

```csharp
await _notifications.PushAsync(new NotificationRequest(
    userId,
    NotificationTypes.JobAssigned,
    "A partner has taken your booking",
    $"{name} ({phone}) is on job #{taskId}.",
    "/customer/profile/tasks"), ct);
```

| Method | Purpose |
|---|---|
| `PushAsync(request)` | One notification. Writes the row, then every registered channel. |
| `PushManyAsync(requests)` | A batch — one save, then the channels. Use it when telling several people about the same thing. |
| `UnreadCountAsync(userId)` / `RecentAsync(userId, take)` | The bell. Already filled for every portal page by the base controller. |
| `ListAsync(userId, paging)` / `MarkReadAsync(userId, id?)` | The notifications page. A null id marks everything read. |

**Adding a delivery channel** (SMS, push) is one class and one registration — implement
`INotificationChannel` and `AddScoped<INotificationChannel, YourChannel>()`. Nothing that raises a
notification changes. Add the new `NotificationTypes` constant and its icon at the same time.

### Offers and automatic assignment — `Services/Booking/OfferService.cs`

| Method | Purpose |
|---|---|
| `StartAsync(taskId)` | Offer a fixed-price job to the next best partner. Call it after creating an instant task; it is safe to call again. |
| `RespondAsync(partnerUserId, offerId, accepted)` | Accept (claims the job) or pass (offers the next partner immediately). |
| `LiveOfferForPartnerAsync(partnerId)` | The offer card on the partner's board. |
| `ExpireDueAsync()` | What `OfferExpiryWorker` calls. You should not need it elsewhere. |

`POST api/offers/{id}/respond` (`OffersController`, `{ accepted: bool }`) is the JS-callable wrapper.
Provider's `Index.cshtml` renders the live-offer card and its own countdown inline — a page-specific
`setInterval` against the `secondsLeft` the API returned, refetching the dashboard at zero. There is
no shared countdown helper; `global.js`'s old `wireCountdowns` had exactly one caller and was retired
when this page stopped being server-rendered.

### Distance — `Services/Geo/`

**Never build a `Point` by hand.** `GeoPoint.From(latitude, longitude)` is the only constructor, so
the SRID and the longitude-first coordinate order live in one place.

```csharp
task.Location = GeoPoint.From(address.Latitude, address.Longitude);
```

| Member | Purpose |
|---|---|
| `GeoPoint.From(lat, lng)` | The point, or null when either half is missing. |
| `GeoPoint.MetresPerKm` | Use it when comparing a radius in km against `Distance()`, which returns metres. |
| `GeoPoint.Describe(km)` | "800 m away", "3.2 km away", "42 km away". Already on `GigTaskDto.DistanceLabel` and `PartnerMatchDto.DistanceLabel`. |
| `IMatchService.RankPartnersAsync(taskId, limit)` | Candidate partners for a task, best first — distance 60%, rating 40%. |
| `IMatchService.DistancesFromPartnerAsync(partnerId, taskIds)` | Kilometres per task from that partner's base, in one query. |

Filtering in the query is what uses the GIST index — do this, rather than loading rows and measuring
in memory:

```csharp
var radiusMetres = partner.ServiceRadiusKm * GeoPoint.MetresPerKm;

query = query.Where(t => t.Location == null
                      || t.Location.Distance(partner.BaseLocation) <= radiusMetres);
```

A row with no point is deliberately kept, not dropped — an old booking without a pin is still real
work. Decide that explicitly every time.

### Ratings — `Services/Ratings/RatingService.cs`

| Method | Purpose |
|---|---|
| `RateAsync(raterUserId, raterRole, taskId, stars, feedback)` | The standalone path. Saves the rating and refreshes the average. Use it for the customer. |
| `BuildAsync(raterUserId, raterRole, task, stars, feedback)` | Validates and returns an unsaved row, or null. Use it when the rating must be saved **with** something else — the partner's rating rides along with the completion. Call `RefreshAverageAsync` yourself afterwards. |
| `RefreshAverageAsync(ratedUserId)` | Recomputes `User.AverageRating` and `User.RatingCount` from the full history. |
| `ForTasksAsync(taskIds, raterRole)` | Ratings already given, keyed by task id — for hiding the "Rate" button on a row that has one. |

Averages live on `User`, not `Partner`, so the same two columns serve both sides. Read them through
`PartnerDto` / `PartnerPublicDto`, both of which expose `AverageRating`, `RatingCount` and a
`RoundedRating` ready for `_Stars`.

---

## C# — pagination

Every list is paged **on the server**. Never load a full list and page it in memory.

```csharp
// Controller
public async Task<IActionResult> Customers([FromQuery] PageRequest paging, CancellationToken ct)
{
    var query = _context.Users.AsNoTracking().Where(u => u.Role == UserRoles.Customer);

    if (!string.IsNullOrWhiteSpace(paging.Search))
        query = query.Where(u => u.Name.ToLower().Contains(paging.Search.Trim().ToLower()));

    var list = await query.OrderByDescending(u => u.CreatedAt).ToPagedResultAsync(paging, ct);
    return View(new AdminUsersViewModel { Users = list.Map(UserDto.From) });
}
```

```cshtml
@* View — never name the variable `page`; Razor treats `@page` as a directive *@
@{ var list = Model.Users; }
@foreach (var user in list.Items) { ... }
<partial name="_Pagination" model="PagerModel.From(list, Context)" />
```

| Piece | Purpose |
|---|---|
| `PageRequest` | `Page`, `PageSize` (default 10, max 100), `Search`. Binds from the query string. |
| `PagedResult<T>` | `Items`, `TotalCount`, `TotalPages`, `FirstRow`, `LastRow`, `HasNext`, `HasPrevious`. |
| `.ToPagedResultAsync(paging, ct)` | Runs COUNT plus one page. Falls back to the last page if the requested page is out of range. |
| `.Map(dto => ...)` | Converts a page of entities into a page of DTOs. |
| `PagerModel.From(list, Context)` | Model for the `_Pagination` partial. Carries existing filters and search forward. |

---

## C# — audit trail (TrackingLog)

Every state-changing controller action is logged automatically by a global
`TrackingActionFilter`. **You do not call anything for API endpoints** — the filter
reads the request model and the returned DTO by itself.

| Column | Filled from |
|---|---|
| `EntryType` | HTTP verb — POST = insert, PUT/PATCH = update, DELETE = delete |
| `FormType` | Controller name, or `[TrackForm("Partner")]` |
| `DocNo` | Route `id`, else the `Id` on the returned DTO |
| `Payload` | jsonb `{ "request": {...}, "result": {...} }`, secrets redacted |
| `Remark` | An argument or model field named remark / reason / feedback / comment |

There is **no OldValues column by design** — the previous row for the same
`FormType` + `DocNo` is the old value.

**Razor portal actions need one extra line.** They redirect instead of returning a
DTO, so hand the saved record over after a successful save:

```csharp
await _context.SaveChangesAsync(ct);
TrackDoc(task.Id, GigTaskDto.From(task));   // from PortalControllerBase
```

Other rules:
- `[SkipTracking]` on actions that change no data (sign-in, sign-out).
- A portal action that sets `TempData["Error"]` is treated as failed and is not logged.
- Passwords and tokens are masked by `JsonRedactor`. Add new secret field names to
  its `SensitiveKeys` list, not to individual controllers.

---

## C# — profile

`IProfileService` (`Services/Profile/`) holds every profile rule. The API controller
and all three portals call it, so web and mobile cannot drift apart.

| Method | Rule it enforces |
|---|---|
| `UpdateAsync` | Phone/email uniqueness; changing the phone clears `IsPhoneVerified` |
| `ChangePasswordAsync` | Current password must be correct, and the new one must differ |
| `UpdatePhotoAsync` | Saves to the public folder, deletes the old file after the save |
| `GetHistoryAsync` | Rebuilds "what changed and when" by diffing consecutive audit rows |

The portal routes live on `PortalControllerBase`, so `/admin/profile`,
`/provider/profile` and `/customer/profile` all come from one implementation —
attribute routes on a base action are picked up under each derived prefix. Add a
shared portal page the same way rather than copying it three times.

---

## C# — auth actions that must stay on the portal controller

Login, OTP and Register all set the `gigapp_token` cookie server-side, so they cannot be pure
`fetch`-to-`api/auth` calls the way every other converted page is — `IssueAuthCookie` needs
`Response.Cookies`, which only the MVC action instance has. The pattern:

```csharp
[HttpPost("login")]
[AllowAnonymous]
[SkipTracking]
public async Task<IActionResult> Login([FromBody] LoginViewModel model)
{
    if (!ModelState.IsValid)
        return BadRequest(new ProblemDetails { Title = FirstError() ?? "...", Status = 400 });

    var failed = await SignInJsonAsync(() => AuthService.LoginAsync(new LoginRequest { ... }));
    if (failed is not null) return failed;

    return Ok(new { redirectTo = LocalRedirectTarget(model.ReturnUrl) });
}
```

| Helper (`PortalControllerBase`) | Signature | Purpose |
|---|---|---|
| `SignInJsonAsync` | `.SignInJsonAsync(Func<Task<AuthResult>> attempt, Func<AuthResponse, Task>? onSuccess = null)` → `Task<IActionResult?>` | Fetch-friendly sibling of `SignInAsync` — same cookie-issuing and role-check logic, but returns a JSON `ProblemDetails` (never a `View`) on failure. `null` means it worked; the caller builds its own success response so it can add fields (`redirectTo`, a "requires registration" branch, etc). |
| `LocalRedirectTarget` | `.LocalRedirectTarget(string? returnUrl)` → `string` | The string half of `RedirectToLocalOr` — same "must be local, must not be the login page" check, for a JSON action that needs the target as data rather than a `Redirect()` result. |
| `FirstError` | `.FirstError()` → `string?` | First `ModelState` error message, for a single-line `ProblemDetails.Title` instead of a full validation summary. |

**Because `[FromBody]` binds via `System.Text.Json`, not the form value providers, an empty string
stays `""` — it is never silently turned into `null` the way a classic form post does.** An optional
string field with `[StringLength(MinimumLength = ...)]` (`VerifyOtpRequest.Name` is the example that
actually broke) fails validation on `""` where it used to pass on a blank form field. The fix is on
the client: send `null`, not `""`, for a blank optional value — `data.Name = data.Name || null;`
before the `App.api.post`.

`_LoginCard` and `_OtpAuthForm` are shared across all three portals' Login/OTP pages, so their JS
cannot live in any one page's `@section Scripts` (see the partials table below) — `wireLoginForm`
and `wireOtpAuthForm` in `global.js` do it centrally, off `data-login-form` and `data-otp-auth` /
`data-otp-request-form` / `data-otp-verify-form` / `data-otp-phone-field` / `data-otp-resend` /
`data-otp-change-number`. The OTP card renders both steps in the DOM at once and toggles
`hidden` — there is no server round trip between entering a phone number and entering the code.

---

## C# — refresh tokens, session refresh, and rate limiting

`IRefreshTokenService` (`Services/RefreshTokenService.cs`) is the only place a refresh token is
issued, redeemed or revoked — call it, never touch the `RefreshTokens` table directly.

| Method | Purpose |
|---|---|
| `IssueAsync(userId, deviceLabel, ipAddress)` → `(RawToken, ExpiresAtUtc)` | New token for a fresh login/register/OTP verify. Called from `AuthService.BuildResponseAsync` — every auth success gets one automatically, nothing to remember at each call site. |
| `RedeemAsync(rawToken, deviceLabel, ipAddress)` → `RefreshTokenResult` | The one entry point for "turn a refresh token into a new session." **Revokes the token it was given and issues a new one in the same call** — a refresh token is single-use, so redeeming a captured-and-replayed copy after the real device already redeemed its own fails immediately. |
| `RevokeByRawTokenAsync(rawToken)` | Logout. Revokes one exact token — call with whatever the `gigapp_refresh` cookie holds before deleting it. |
| `ListActiveAsync(userId, currentRawToken)` → `DeviceSessionDto[]` | Manage devices' list — non-revoked, unexpired rows only, newest-used first, with `IsCurrent` set by matching the caller's own token. |
| `RevokeAsync(userId, refreshTokenId)` / `RevokeAllExceptAsync(userId, currentRawToken)` | Sign out one device, or every device but this one. Both scoped to `userId` — a token id alone is never enough to revoke something. |

`DeviceLabel.FromUserAgent(userAgent)` (`Services/DeviceLabel.cs`) turns a User-Agent string into
something like `"Chrome on Windows"` — a short heuristic, not a library, so it only recognises the
common browser/OS combinations and falls back to `"Unknown device"`. Reuse it anywhere a session
needs a human label; don't write a second parser.

**Cookies:** `AuthCookie.Name` (`gigapp_token`, short-lived) and `AuthCookie.RefreshName`
(`gigapp_refresh`, 30 days) are both set together by `PortalControllerBase.IssueAuthCookie` and
cleared together by `ClearAuthCookieAsync` — never issue or clear one without the other. The shared
`POST /{portal}/refresh-session` action redeems the refresh cookie and reissues both; `global.js`'s
`wireSessionRefresh()` calls it every 20 minutes on any portal page (`/customer`, `/provider`,
`/admin`) so a session outlives the short access token silently. A page that is not a portal page
(the storefront) never starts this timer — check `location.pathname` the same way if you add another
portal-scoped background call.

**Rate limiting:** `RateLimiterPolicies.Auth` and `.OtpRequest` (`Program.cs`) are the two policies;
apply with `[EnableRateLimiting(RateLimiterPolicies.X)]` on any new endpoint that takes
unauthenticated, attacker-reachable input (register, login, OTP, refresh). Both are keyed by client
IP with a fixed window — `Auth` is 10/minute, `OtpRequest` is 3/5 minutes. A rejection is handled by
one shared `OnRejected` in `Program.cs` that writes the same `{title, status}` shape every other
error uses, so the client needs no special handling for a `429`.

---

## Real-time (SignalR) — server broadcast and client subscribe

`IRealtimeNotifier` (`Services/Realtime/RealtimeNotifier.cs`) is the only way any service pushes a
live update — never inject `IHubContext<AppHub>` directly into a feature service.

| Method | Sends | Use it when |
|---|---|---|
| `NotifyUserAsync(userId, topic)` | `"refresh"` + topic, to `user-{userId}` | One specific person's own list needs to re-fetch — e.g. a customer's own task, a partner's own bid outcome. |
| `NotifyCategoryPartnersAsync(categoryId, topic)` | `"refresh"` + topic, to `category-{categoryId}-partners` | The open-jobs board for one skill needs to change for every partner watching it — a new bidding task, a job leaving the board once it is claimed. |
| `NotifyAdminsAsync(topic)` | `"refresh"` + topic, to `admins` | `/admin/tasks` (or any future admin list) needs to reflect the change too — admin sees everything, so this is fired alongside almost every other call. |
| `PushNotificationAsync(userId, title, body, link)` | `"notification"`, to `user-{userId}` | Never call directly — this is what `SignalRNotificationChannel` calls automatically for every `INotificationService.PushAsync`. Raising a notification is enough; the live push is already wired in. |

**The topic is a string, not a payload — the client already knows how to fetch its own data.**
`"tasks"` is the only topic in use today; add a new one only when an existing page's `load()` cannot
answer the update (e.g. a page with no such function yet). Adding a broadcast to a new code path is
always two lines: pick the right `NotifyXxxAsync` call(s) for who needs to see the change, and add
them right after the state change is saved — see `BidService.AcceptAsync`/`AwardAsync` for the
pattern of notifying more than one audience from a single action.

**Client side, one line per page.** `App.realtime.on('tasks', load)` (`global.js`) right after that
page's own initial `load()` call is the entire integration — see `Provider/Index.cshtml`,
`_ProfileTasks.cshtml`, `Admin/Tasks.cshtml`. `wireRealtime()` builds the one shared
`signalR.HubConnectionBuilder().withUrl('/hubs/app')` connection per page (portal pages only, same
`location.pathname` gate as `wireSessionRefresh`), so a page never opens its own connection.
`refreshNotificationBell()` is called automatically on every `"notification"` event — no page needs
to wire that one itself.

**Auth works identically for a browser and a future mobile client.** The hub is reached at
`/hubs/app` under standard JWT bearer auth; `JwtBearerEvents.OnMessageReceived` in `Program.cs`
reads `access_token` off the query string specifically for `/hubs/*` paths, since a WebSocket
handshake cannot carry a custom header. A Flutter client authenticates with
`.withUrl('/hubs/app', options => options.accessTokenFactory = () => bearerToken)` — no server
change needed when that day comes.

The vendored client library is `wwwroot/lib/signalr/signalr.min.js` (from cdnjs, matching the
CDN-free convention) — loaded by both `_Layout` and `_AdminLayout` right before `global.js`.

---

## C# — cart

`ICartService` (`Services/Storefront/`) is the only place a cart is read or written — session for a
guest, mirrored to `CartItem` once signed in as a customer. `CartController` (`api/cart`,
`[AllowAnonymous]`) is a thin wrapper: `GET` returns the priced `CartViewDto`, `POST items` adds a
line, `PUT items/{id}` sets a quantity (0 removes it, same as the service method), `DELETE items/{id}`
removes it outright. Every action returns the fresh priced cart so the client never has to guess
what changed — see `wireCartControls`/`applyCartToPage` below for how the page uses that.

---

## C# — file uploads

`IFileStorageService` (`Services/Files/`) handles every upload. Never write to disk directly.

```csharp
var saved = await _storage.SaveAsync(file, FileCategory.KycDocument, ct);
if (!saved.Succeeded) return BadRequest(saved.Error);
entity.SelfieFileName = saved.FileName;      // store the name only, never a path
await _context.SaveChangesAsync(ct);
_storage.Delete(oldName, FileCategory.KycDocument);   // only AFTER the save succeeds
```

| Category | Stored in | Reachable by |
|---|---|---|
| `FileCategory.ProfileImage` | `wwwroot/uploads/profile` | anyone — `/uploads/profile/{name}` |
| `FileCategory.KycDocument` | `App_Data/kyc` (outside wwwroot) | `/api/files/kyc/{name}`, admin or owner only |

Every upload is renamed to `{guid:N}{ext}`, capped at 5 MB, restricted to
jpg/jpeg/png/webp, and checked against the real file signature — extension and
content type are both attacker-controlled, so neither is trusted alone.

Forms carrying a file need `enctype="multipart/form-data"`; API actions need
`[Consumes("multipart/form-data")]` and `[FromForm]`.

---

## Razor partials — `Views/Shared/`

| Partial | Model | Purpose |
|---|---|---|
| `_Flash` | — | `TempData["Success"]` / `["Error"]` alerts. **Already in both layouts** — do not repeat it in a view. |
| `_ValidationSummary` | — | Lists `ModelState` errors. Put it above every form. |
| `_Pagination` | `PagerModel` | Bootstrap pager plus a "Showing X–Y of Z" line. |
| `_LoginCard` | `LoginViewModel` | Shared sign-in card for all three portals. Heading, subtitle and footer come from `ViewData`; `wireLoginForm` (`global.js`) wires the submit, since a partial cannot declare its own `@section Scripts`. |
| `_KycModal` | — | Static, id-based admin review modal (Review/History tabs, Approve/Reject). Include it once per page with `<partial name="_KycModal" />`; `Admin.openKycModal(partner, history, onDecision)` (`global.js`) fills it in and wires the buttons. Used by `Partners.cshtml` and `Approvals.cshtml`. |
| `_AccountModal` | — | Static, id-based super-admin account modal (password reset, activate/deactivate). Include it once per page, guarded by `@if (ViewData["IsSuperAdmin"] is true)`; `Admin.openAccountModal(user, onSaved)` (`global.js`) fills it in and wires both forms. Used by `Users.cshtml` and `Partners.cshtml`. |
| `_AddressForm` | — | Static, fixed-id address form (`#address-form` and friends). No `@model` — embed once via `<partial name="_AddressForm" />` inside `_AddAddressModal`, never standalone. |
| `_AddAddressModal` | — | Static shell (`#address-modal`) wrapping `_AddressForm`. Include it once per page; `Admin.openAddressModal(address, onSaved)` (`global.js`) fills the form and wires its submit. Used by `_ProfileAddresses` and `_ProfilePostTask`. |
| `_UserMenu` | `UserMenuViewModel` | Navbar avatar with a Profile / Sign out box, plus an optional notification bell. Used by both layouts. |
| `_Icon` | `string` icon name | Inline SVG icon (`cart`, `user`, `bell`, `log-out`, `lock`), `currentColor` stroke, 18×18. Add a `case` in the partial for a new one — do not paste raw `<svg>` markup at a call site. |
| `_OtpAuthForm` | `OtpAuthViewModel` | The phone → code screen used by both the customer and partner OTP login flow. Both steps render at once and `wireOtpAuthForm` (`global.js`) toggles them with `hidden` — no server round trip between steps. The portal-specific wrapper view just sets `PortalSlug`/`PortalLabel`. |
| `_ImageUpload` | `ImageUploadModel` | **Every image field goes through this.** Drag-and-drop, paste, camera capture, live preview and client-side resizing. See below. |
| `_StarInput` | `string` group id | Five radio buttons posting as `Stars`, styled as clickable stars. The group id must be unique on the page. Pair it with a textarea named `Feedback`. |
| `_Stars` | `int?` | Read-only star row. Renders "Not rated yet" for null or zero. |
| `_ProfileBody` | `ProfilePageViewModel` | Profile shell: left menu plus the section named by `Model.Section`. Add a new section by adding to `ProfileSections`, the menu list here, a `case` in its switch, and a `GET` on `PortalControllerBase`. Gate it by role in `SectionApplies`. |
| `_ProfileDetails` / `_ProfileBank` / `_ProfileSettings` / `_ProfileOrders` / `_ProfileKyc` / `_ProfileEarnings` / `_ProfileServiceArea` / `_ProfileAddresses` / `_ProfilePostTask` / `_ProfileTasks` / `_ProfileDevices` | `ProfilePageViewModel` (unused — thin shell) | Every profile section is now the API+JS pattern: static markup plus an inline `<script>` fetching its own data — `api/profile` (+`/history`, `/photo`, `/password`, `/bank`, `/orders`, `/orders/{id}/help`), the Provider-only `api/partners/me/...` endpoints, `api/addresses` / `api/gigtasks` / `api/bids` / `api/partners/{id}/public` for the Customer sections, or `api/devices` for Manage devices (every role). Included the same way from `_ProfileBody`'s switch — the difference is invisible at the call site. Remember the `DOMContentLoaded` rule above; a plain IIFE breaks here. |

### Images — `_ImageUpload`

**Never write a bare `<input type="file">` again.** Every image field on every portal renders this
partial, so one fix reaches all of them.

```razor
<partial name="_ImageUpload" model="@(new ImageUploadModel {
    Name = nameof(Model.Selfie),
    Label = "Your selfie",
    Hint = "A clear photo of your face.",
    CurrentUrl = Model.User.ProfileImageUrl,
    Shape = ImageUploadShape.Circle,
    AllowCamera = true,
    Required = true,
})" />
```

| Property | Meaning |
|---|---|
| `Name` | The form field name. Binds to an `IFormFile` exactly as before. |
| `Id` | Only when two boxes on one page share a `Name`. Defaults to `Name`. |
| `Label` / `Hint` | Label above the box, help text below it. The hint is restored after an error. |
| `CurrentUrl` | Shows the stored image inside the box on an edit form. |
| `Required` | Renders the HTML `required`. Once a file is attached it is dropped, so a replacement satisfies a field whose original is gone. |
| `Shape` | `ImageUploadShape.Wide` (16:9, default), `.Square` (4:3, documents) or `.Circle` (avatars). |
| `AllowCamera` | Adds `capture="environment"`, so a phone opens the camera instead of the gallery. |
| `MaxPixels` | Longest edge after resizing. Default 1600. |
| `MaxBytes` | Client-side limit. Keep it equal to `FileStorage:MaxBytes`. |

**The form still needs `enctype="multipart/form-data"`** — this is a normal file input underneath, and
the controller keeps taking `IFormFile`. Nothing about the server side changes.

What `global.js` adds on top, with no per-page JavaScript:

- **Resizing before upload.** Anything over `MaxPixels` is drawn to a canvas and re-encoded as JPEG at
  0.85, then swapped back into the input through a `DataTransfer`. A 10 MB phone photo becomes about
  70 KB. A file that is already small is left completely untouched.
- **EXIF orientation** is applied while resizing, so a sideways phone photo uploads upright.
- The stored name gains a `.jpg` extension when it is re-encoded, so the extension always matches the
  bytes the server checks.
- **Drag and drop, clipboard paste, keyboard (Enter/Space), click to browse** and a remove button.
- Wrong type or still-too-large is refused on the spot, **the input is emptied** so it cannot be
  posted, and the reason appears both under the box and as a toast.
- If the browser cannot resize, the original file uploads untouched. Nothing is ever blocked by it.

There is deliberately **no background upload or progress bar**. The file leaves the browser at roughly
70 KB, so the ordinary form post is fast enough, and keeping it means no upload endpoint, no temporary
files and no orphan cleanup. Add one only if a genuinely large upload appears.

### Which partner DTO to use

| DTO | Contains | Give it to |
|---|---|---|
| `PartnerDto` | Aadhaar number, KYC image names | The partner themselves, and admins |
| `PartnerPublicDto` | Name, phone, skill, jobs completed | Customers |

`GET /api/partners/{id}/public` returns the public one and refuses unless the
caller is an admin, that partner, or a customer who shares a task with them —
otherwise anyone signed in could walk the ids and harvest phone numbers.

### Commission plans and tax — `Services/Earnings/PlanService.cs`, `TaxService.cs`

```csharp
await _plans.GetPlansAsync(activeOnly, ct)
await _plans.SavePlanAsync(id, request, ct)                       // id null inserts
await _plans.AssignPlanAsync(partnerId, request, byId, byName, ct)
await _plans.ResolveActivePlanAsync(partnerId, ct)               // falls back to the default plan
await _plans.GetPartnerPlanAsync(partnerId, ct)

await _taxes.GetRulesAsync(countryCode, activeOnly, ct)
await _taxes.SaveRuleAsync(id, request, ct)
await _taxes.RulesInForceAsync(countryCode, moment, ct)          // date-filtered
_taxes.Compute(rules, gross, commission, subscriptionFee)        // pure, returns TaxCharge rows
```

`Compute` is deliberately pure — no database, no clock — so the deduction for any combination can be
checked without posting anything. `RulesInForceAsync` takes the **moment the job settled**, never
`DateTime.UtcNow` at read time.

### Partner earnings — `Services/Earnings/EarningsService.cs`

Nothing writes to `LedgerEntries` or `PartnerWallets` directly. Every posting goes through here, in
a transaction, with an idempotency key.

```csharp
await _earnings.PostJobEarningAsync(task, ct)                       // call after a task is completed
await _earnings.PostPayoutAsync(partnerId, request, byId, byName, ct)
await _earnings.PostAdjustmentAsync(partnerId, request, byId, byName, ct)
await _earnings.GetSummaryAsync(partnerId, ct)                      // balance and lifetime figures
await _earnings.GetEntriesAsync(partnerId, paging, entryType, ct)   // paged statement
await _earnings.GetBalancesAsync(ct)                                // what the platform owes everyone
```

`PostJobEarningAsync` is safe to call twice — the second call returns `WasAlreadyPosted` instead of
crediting again. Call it after `SaveChangesAsync`, once the task is actually `completed`.

### Error log — `Services/Errors/ErrorLogService.cs`

```csharp
var reference = await _errorLog.LogAsync(exception, module, ct);
```

You will rarely call this: `GlobalExceptionFilter` is registered globally and catches everything
that escapes an action. Call it directly only when swallowing an exception on purpose and the user
still needs something to quote.

### Admin menu — `Services/Menus/MenuService.cs`

The admin sidebar is data. Nothing about it is hard-coded in `_AdminLayout` except how a row is
drawn.

```csharp
await _menus.GetSidebarAsync(isSuperAdmin, ct)   // two-level tree, active rows only
await _menus.GetAllAsync(ct)                     // flat list for the master screen, groups first
await _menus.GetParentOptionsAsync(excludingId, ct)
await _menus.SaveAsync(id, request, ct)          // id null inserts
await _menus.DeleteAsync(id, ct)                 // refuses a group that still has children
_menus.ActionExists(controller, action)          // guards against a dead link
```

`SaveAsync` validates before it writes: the action must exist, a row cannot carry both a route and
a URL, the tree cannot go deeper than two levels, and `BadgeKey` must be a known counter.

### KYC history — `Services/Kyc/KycHistoryService.cs`

There is **no separate KYC history table**. The history is a read over `TrackingLogs`, filtered to
`FormType = "Partner"` with the partner id as `DocNo`.

```csharp
await _kycHistory.ForPartnerAsync(partnerId, ct)            // one partner
await _kycHistory.ForPartnersAsync(partnerIds, ct)          // a whole page, one query
```

Each row becomes a `KycHistoryEntryDto` with `Action`, `Detail`, `By`, `Remark` and the `Status` the
event produced, so the caller renders it without parsing anything.

A new action only appears in the history if it is tagged `[TrackForm(KycHistoryService.FormType)]`
and its `DocNo` resolves to the **partner** id — a route `{id}`, a returned `PartnerDto`, or an
explicit `TrackDoc(partner.Id, …)`.

### Bank account — `Services/Banking/BankAccountService.cs`

One account per user, `BankAccounts.UserId` unique. `GetAsync` returns a `BankAccountDto` whose
account number is already masked; the full number never leaves the service.

```csharp
await BankAccounts.GetAsync(userId, ct)
await BankAccounts.SaveAsync(userId, request, ct)           // inserts or updates, whichever applies
```

### KYC transitions — `Models/PartnerKyc.cs`

Every change to a partner's KYC state goes through this class. Nothing else may assign `KycStatus`,
`KycRejectionReason`, `KycReviewedAt` or `KycReviewNote` directly.

```csharp
PartnerKyc.ChangeSkill(partner, categoryId, fromName, toName)  // true when it went back for review
PartnerKyc.SubmitDocuments(partner)                            // resubmission -> pending, reason cleared
PartnerKyc.Review(partner, approved, rejectionReason)          // admin decision, clears the note
PartnerKyc.SkillChangeWarning                                  // the text for the SweetAlert confirm
```

`ChangeSkill` returns false when the category did not actually change, or when the partner was not
approved to begin with. Category names come from `ICategoryLookup.GetNameAsync`.

### KYC status — `Models/KycStatus.cs`

Never write the status strings by hand and never re-derive the badge colour in a
view. One helper class owns all of it.

```csharp
KycStatus.Approved            // "approved" — also NotSubmitted / Pending / Rejected
KycStatus.IsValid(value)      // guard, for when a status ever arrives from a request
KycStatus.Label(value)        // "Verified" / "Pending review" / "Rejected" / "Not submitted"
KycStatus.BadgeClass(value)   // "text-bg-success" / "text-bg-warning" / ...
```

`PartnerDto` surfaces the same thing ready for a view, so a Razor page needs no
`if` chain at all:

```html
<span class="badge @partner.KycBadgeClass">@partner.KycLabel</span>
@if (partner.IsRejected) { <div class="small text-danger">@partner.KycRejectionReason</div> }
```

`IsVerified` / `IsRejected` / `IsAwaitingReview` on `PartnerDto` are computed from
`KycStatus` — use them instead of comparing strings.

---

## Standard form format

Keep the same structure for every form:

```cshtml
<form method="post" asp-action="Save" class="gig-form row g-3">
    <div class="col-md-6">
        <label asp-for="Name" class="form-label required"></label>   @* required = red star *@
        <input asp-for="Name" class="form-control" />
        <span asp-validation-for="Name" class="text-danger small"></span>
    </div>

    <div class="col-12">
        <label asp-for="Notes" class="form-label"></label>
        <textarea asp-for="Notes" class="form-control" rows="3"></textarea>
        <div class="form-text">Short helper text goes here.</div>
    </div>

    <div class="col-12 d-flex gap-2 pt-2">
        <button type="submit" class="btn btn-primary">Save</button>
        <a class="btn btn-outline-secondary" href="/back">Cancel</a>
    </div>
</form>
```

Rules: `row g-3` grid · `col-md-6` for a normal field, `col-12` for full width · `.required` on the
label for the red star · wrap in `.form-card` (720px) or `.form-card-narrow` (420px).

---

## Layouts and CSS

| File | When to use |
|---|---|
| `_Layout.cshtml` | Public, customer and provider pages. Top navbar. |
| `_AdminLayout.cshtml` | `/admin` only. Sidebar shell, active state from the path, pending-KYC badge. |

Bootstrap 5.3 is vendored locally in `wwwroot/lib/` (no CDN, so it works offline).
`site.css` is only a thin layer: the sidebar, making the jQuery UI autocomplete look like a Bootstrap
dropdown, and the `.badge-<status>` colours. **Look for a Bootstrap utility first** before writing CSS.

Responsive: below 992px the sidebar becomes a drawer (CSS-only checkbox, no JavaScript).
Always wrap tables in `.table-responsive`.

---

## Flutter — `provider_app`'s clean architecture, copy its shape

`provider_app/lib` is layered `core / domain / data / presentation`, state managed with Riverpod,
networking through one shared Dio client. This is the reference shape for `customer_app` and for
every later slice of `provider_app` itself — read this before adding a new feature rather than
re-deciding the pattern.

- **`core/network`** — `buildApiClient()` (`api_client.dart`) returns one `Dio` per app, its only
  interceptor being `AuthInterceptor`. That interceptor attaches `Authorization: Bearer` from
  `TokenStorage` on every request and, on a `401` that is not itself the refresh call, redeems the
  refresh token via a **plain, non-intercepted** `Dio` (so the retry cannot recurse), saves the new
  pair, and retries the original request once. Concurrent 401s share one in-flight refresh via a
  memoised `Future<String?>`. A refresh failure clears storage and calls `onSessionExpired`, which
  `presentation/providers.dart` wires to `SessionController.forceSignedOut()`.
- **`core/storage/token_storage.dart`** wraps `flutter_secure_storage` — Keychain/Keystore on a real
  device, so this is genuinely secure there. **`TokenStorage.save()` writes its four keys one at a
  time, on purpose — never `Future.wait` them.** `flutter_secure_storage`'s own web backend
  (`flutter_secure_storage_web`) creates its AES-GCM wrapping key lazily on first write with a
  check-then-create on `localStorage` that has no locking: `if (!localStorage.containsKey(keyName))
  { generate a new key; write it }`. Fire that from concurrent writes and more than one of them can
  see "no key yet" at once, each generate a *different* key, and each write its own to
  `localStorage` — whichever write lands last wins, silently leaving the values written by the
  others permanently encrypted under a key that is no longer there. Decrypting those values then
  throws a WebCrypto `OperationError` **forever**, not just once — no amount of retrying `read()`
  fixes already-mismatched ciphertext, because the corruption happened at write time. Sequential
  `await`s mean only the very first write ever hits the "no key yet" branch; every write after that,
  in this call and every later one, finds the key already in `localStorage` and reuses it. This bug
  is web-only — native Keychain/Keystore has no such race — but real enough on web that it can look
  like a fresh sign-in silently signed you out one API call later, and the fix is required for the
  web target to be usable for demos or Chrome-based testing.
- **`domain/repositories`** are interfaces only (`AuthRepository`, `PartnerRepository`);
  `data/repositories/*_impl.dart` implement them over a `data/datasources/*_remote_data_source.dart`
  (raw Dio calls) and `data/models/*_model.dart` (`fromJson`, extending the matching `domain/entities`
  class so a model *is* the entity plus parsing — no separate mapping step). There is deliberately
  **no usecases layer** — each repository method is already one action, and adding a one-line wrapper
  class per method for a 3–4 method interface is the kind of indirection CLAUDE.md's own "no premature
  abstraction" rule warns against. Add one only if a real usecase starts combining more than one
  repository call.
- **`presentation/providers.dart`** is the single DI wiring file — every `Provider`/`FutureProvider`
  that builds a datasource, repository or the Dio client itself lives here, so "what does X depend
  on" has one place to look. A screen-local flow (`presentation/auth/login_flow_controller.dart`) is
  its own `StateNotifierProvider.autoDispose` next to the screen that owns it, not in this file.
- **Sign-in is one screen with steps, not one route per step** — `LoginScreen` switches on
  `LoginFlowState.step` inside an `AnimatedSwitcher`, mirroring the web's own single-page OTP form
  ([_OtpAuthForm.cshtml](../GigApp.Api/GigApp.Api/Views/Shared/_OtpAuthForm.cshtml)) rather than
  inventing a different flow for mobile. "More options" is a `showModalBottomSheet`, the same
  "choose another way to sign in" pattern as the web's own modal — add a new sign-in method to both
  places together, never one without the other, or the two clients drift. The two apps' steps differ
  because the underlying business rule differs, same as on web: `provider_app` has
  `phone → code → password | registrationRequired`, where an unknown number's `registrationRequired`
  step is a real form (`presentation/registration/widgets/partner_registration_form.dart`) — name,
  email, a skill category `DropdownButtonFormField` fed by `GET api/skillcategories` (now
  `[AllowAnonymous]` — see below), three `ImagePickerTile`s (`presentation/registration/widgets/`,
  circle for the selfie, square for both Aadhaar sides, each opening a bottom sheet for
  camera-vs-gallery via `image_picker`), and an Aadhaar number field, posting multipart to
  `api/auth/register/partner` with `phoneVerifiedViaOtp: true`. `customer_app` has only
  `phone → code → password` — no registration step, because `OtpService` auto-registers an unknown
  *customer* number inline; its `code` step instead carries an always-visible, optional "Your name"
  field (`CodeStep`, hint: "Only needed the first time, to set up a new account."), passed straight
  through as `verifyOtp(code, name: ...)`.
- **No router yet, on purpose.** Root `ProviderApp`/`CustomerApp` (`app.dart`) watches
  `sessionControllerProvider` and picks `SplashScreen` (loading) / `LoginScreen` (no user) /
  the signed-in screen (`DashboardScreen` for provider, `MyTasksScreen` for customer) directly — a
  handful of destinations do not earn `go_router`'s redirect-guard machinery. Reach for it when a
  real navigation stack (job list → job detail, tabs) actually needs it, not before.
- **A partner registration re-verifies the phone server-side, the same way the web does.**
  `AuthController.RegisterPartner` takes `[FromForm] bool phoneVerifiedViaOtp` — the mobile-facing
  twin of `ProviderController.Register`'s identical form field — and, when true, marks
  `IsPhoneVerified`/`PhoneVerifiedAt` on the new user in an `ExecuteUpdateAsync` right after
  `RegisterPartnerAsync` returns. That update runs *after* the `AuthResponse` was built, so the
  `user.isPhoneVerified` a mobile client gets back from the registration call itself still reads
  `false` — the same staleness the web flow already accepted; the next `GET api/auth/me` or session
  restore shows the correct value. Don't try to make the register response's flag correct; that
  would mean restructuring the transaction for a cosmetic first-paint value nobody reads.
- **Mobile auth against `api/auth`, never the portal MVC controllers.** `api/auth/otp/request`,
  `otp/verify`, `login`, `refresh` and `me` are the only endpoints a Flutter client calls for
  identity — they take an explicit `Role` and return the same `AuthResponse` JSON (token +
  refreshToken) every time, with no cookie involved. The portal controllers
  (`ProviderController`/`CustomerController`) exist only for the browser and must never be pointed
  at from mobile — they set an HttpOnly cookie mobile cannot use and return `{redirectTo}`, not a
  token.
- **Every mutation on a data-heavy screen goes through one `*ActionsController`
  (`presentation/dashboard/dashboard_actions_controller.dart`)**, not a method per button on the
  screen's own state. `DashboardActionsController.run(Future<void> Function())` sets a busy
  `AsyncValue`, awaits the call, and on success calls `ref.invalidate(dashboardProvider)` — the one
  line that makes the list re-fetch and re-render itself, the mobile equivalent of the web calling
  its own `load()` again after a `App.api` mutation resolves. `OrdersScreen` (and `HomeScreen` for
  the duty toggle) watch this controller's `.isLoading` to disable every action button at once during
  a call (never per-button spinners — one call in flight is enough reason to pause the whole screen)
  and `ref.listen` it to show a mutation's error in a red `SnackBar`; a success toast is a separate
  green `SnackBar` the calling method fires itself, since the message differs per action and the
  controller does not know which one just ran. Copy this shape for any future screen with more than
  one or two mutating buttons (KYC resubmission, a future "post a task" on `customer_app`) rather
  than wiring loading state per button.
- **A confirm-only action (no extra fields) uses the shared `showConfirmDialog`
  (`presentation/common/widgets/confirm_dialog.dart`)**; a prompt that collects fields is its own
  small `showXxxDialog` function returning a typed result or `null` on cancel
  (`bid_dialog.dart`/`complete_job_dialog.dart`/`cancel_job_dialog.dart`, all under
  `presentation/dashboard/widgets/`, plus `edit_profile_dialog.dart`/`bank_account_dialog.dart`/
  `change_password_dialog.dart` under `presentation/profile/widgets/`) — a plain function returning
  a `Future<T?>`, not a stateful widget the caller has to instantiate and manage. This is the Flutter
  analogue of the web's `App.confirmAction` vs. a named Bootstrap modal: same two shapes, same reason
  for the split. A form dialog that itself calls the network (bank account, password, profile edit)
  owns its own `isSaving`/`error` state and pops with the typed result only on success, rather than
  routing through `*ActionsController` — that controller exists for a screen with several buttons
  sharing one busy flag, not a one-off modal form.
- **Mobile connects to the same `/hubs/app` SignalR hub the web portals use, via
  `core/realtime/realtime_client.dart`'s `RealtimeClient`.** It authenticates with
  `HttpConnectionOptions(accessTokenFactory: ...)` reading the stored access token — no separate
  mobile auth path, since `AppHub`/`Program.cs` already reads `access_token` off the query string
  for any `/hubs` path (WebSocket upgrades can't carry a custom header). `RealtimeClient` exposes two
  plain callbacks, `onRefresh(topic)` and `onNotification(title, body)`, mirroring `global.js`'s
  `App.realtime.on(topic, handler)` / the bell's toast — wire `onRefresh` to `ref.invalidate(...)`
  the relevant provider (topic `"tasks"` → `dashboardProvider`) and `onNotification` to whatever UI
  needs to show a toast, the same two events every future screen needing live data should listen for
  rather than inventing a new one. `realtimeClientProvider` (`presentation/providers.dart`) is a
  plain `Provider.autoDispose` — `AppShell` calls `.connect()` once in `initState` and `ref.watch`es
  it just to keep it alive for the session; being `autoDispose` means signing out (which unmounts
  `AppShell`) disconnects it for free via `ref.onDispose`, no explicit teardown call needed anywhere
  else. `customer_app` should connect the same way once it has anything worth pushing live.
- **A multi-section app is one `IndexedStack` with two ways to change its index, not per-screen
  `Navigator` pushes.** `presentation/shell/app_shell.dart`'s `AppShell` holds one
  `shellSectionProvider` (`StateProvider.autoDispose<int>`) and switches `IndexedStack`'s `index` —
  every section's widget tree and scroll position survives switching away and back, since
  `IndexedStack` keeps every child mounted rather than rebuilding on selection. A bottom
  `NavigationBar` drives the sections opened constantly (Home, Orders); `AppDrawer`
  (`presentation/shell/widgets/app_drawer.dart`) drives the rest (Wallet, Profile) plus sign-out —
  both write the exact same `shellSectionProvider`, so which control moved the index is invisible to
  every screen. `AppDrawer` itself is pure display: a list of `_DrawerItem`s, driven entirely by the
  `selectedIndex`/`onSelect` it's handed — it holds no navigation logic of its own. A child screen
  that needs to jump to a *specific* tab of another section (Home's "available work" card into
  Orders' Available tab) does it by writing a second, narrower `StateProvider` for that one piece of
  state (`ordersInitialTabProvider`) rather than threading a callback through the shell — `AppShell`
  just watches it and passes it down as that screen's `initialTab`.
- **A paged list past its first screen uses one shared generic controller, not a bespoke one per
  screen.** `presentation/common/paged_list_controller.dart`'s `PagedListController<T>` holds
  `items`/`page`/`hasNext`/`isLoading`/`error` in `PagedState<T>`, and `loadMore()` fetches the next
  page and appends rather than replacing. Wallet's `ledgerControllerProvider` and Notifications'
  `notificationsControllerProvider` (`presentation/providers.dart`) are both just
  `StateNotifierProvider.autoDispose<PagedListController<X>, PagedState<X>>((ref) =>
  PagedListController<X>(({required page}) => ref.read(xRepositoryProvider).getXxx(page: page)))` —
  the repository method is the only thing that changes. Every screen renders `state.items` plus a
  trailing "Load more" (or spinner) driven by `hasNext`/`isLoading`, and **must also render
  `state.error` as its own branch, distinct from a genuinely empty list** — see the gotcha below for
  why silently treating an error as "empty" hid a real bug for an entire testing pass. `refresh()`
  resets to `PagedState<T>.initial()` and reloads page 1, wired to the screen's `RefreshIndicator`.
  Copy this shape (`domain/entities/paged.dart`'s `Paged<T>` is the matching repository return type)
  for the next paginated list — order history, KYC history — rather than writing a new controller.
- **Never give a generic class's `List<T>` field a bare `const []` default.** `PagedState<T>`'s
  constructor tried `this.items = const []`; Dart types that default as `List<Never>`, not `List<T>`
  — the default value expression is evaluated once, per-class, with `T` unbound, so it falls back to
  the bottom type. `flutter analyze` does not catch this. The Dart VM (mobile debug/release builds)
  is often lax enough to let a `List<Never>` slide through generic assignment, but DDC (the `flutter
  run -d chrome` web compiler) enforces the mismatch strictly at runtime, throwing inside the very
  next `copyWith` — silently, if it's inside a `try/catch` that maps exceptions to a state field nothing
  renders (exactly what happened here — the API returned real data, but every list screen showed its
  empty state). Give a generic class with collection fields a private constructor plus a
  `factory X.initial()` that builds the empty collection at a call site where `T` is already bound
  (`PagedState._({required this.items, ...})` / `factory PagedState.initial() => PagedState._(items:
  <T>[], ...)`), never a bare literal default.
- **A paged screen writes its own controller instead of `PagedListController<T>` only when an item
  in the list needs to change from an action taken on that same screen.**
  `customer_app/lib/presentation/orders/order_history_controller.dart` is the example: rating a
  completed order or cancelling a pending one has to update that one row in place, and the generic
  controller's `PagedState<T>` has nowhere to hold the extra `ratings`/`enquiries` maps this needs.
  `OrderHistoryController` is a small hand-written `orders`/`page`/`hasNext`/`ratings`/`enquiries`
  state with `replaceTask`/`putRating`/`putEnquiry` mutators the UI calls straight after a successful
  action, instead of refetching the whole page. Default to the generic controller; reach for this
  shape only when a screen genuinely needs per-item state the generic one has no field for.
- **A screen built for one portal's Flutter app can usually be copied to the other unchanged.**
  `customer_app` copied `provider_app`'s `AppNotification`/`DeviceSession` data layers,
  `NotificationsScreen`, `DevicesScreen`, `RealtimeClient`, `paged_list_controller.dart`, and the
  `bank_account_dialog.dart`/`change_password_dialog.dart`/`edit_profile_dialog.dart` trio verbatim —
  none of that is partner-specific. Check `provider_app` for an existing screen before writing a new
  one for `customer_app`, the same instinct as checking an existing API controller before writing a
  new one.
- **A Razor page that already builds the right ViewModel gets a JSON sibling, not a rewrite.**
  `ShopController.Index`/`Category`/`SearchAsync` each moved their body into a private
  `BuildXxxAsync(...)` returning the existing `StorefrontXxxViewModel`; the original action becomes
  `return View(await BuildXxxAsync(...))` and a new `[HttpGet("/api/xxx")]` action becomes `return
  Ok(await BuildXxxAsync(...))`. Works cleanly whenever the ViewModel is already DTOs all the way
  down (no raw entities, no circular refs) — check that before assuming this shape applies.
- **`core/utils/image_url.dart`'s `resolveImageUrl(String? path)` turns the API's relative
  `/uploads/...` path into `${Env.apiBaseUrl}$path`** for any `_Model.fromJson` that carries an
  image (`ServiceItemModel`, `CatalogCategoryModel`, `CatalogBannerModel`, `CartLineModel`) — the API
  never returns a full URL since it does not know its own public host. Call it once, in the model's
  `fromJson`, not at the widget layer.
- **One shared cart controller, not a quantity field re-fetched per screen.**
  `presentation/cart/cart_controller.dart`'s `CartController` (`StateNotifierProvider.autoDispose<
  CartController, AsyncValue<Cart>>`) is read by every add-to-cart stepper
  (`AddToCartControl`, keyed off `serviceItemId`) and the AppBar's `CartButton` badge alike, so a
  change from any one of them is instantly visible everywhere else — same "one shared mutable state,
  many read sites" shape as `PagedListController<T>`, just not paginated. Its mutation methods
  (`add`/`setQuantity`/`remove`) let a thrown `ApiException` propagate rather than writing it into
  `state` — the calling widget's own try/catch shows the SnackBar, and the last good cart stays on
  screen instead of being replaced by an error page.

`customer-ui.css` holds the customer-facing button look — `.btn-uc-primary`, `.btn-uc-outline`,
`.btn-uc-pill` — layered on top of Bootstrap's `btn` class (keep `btn` for focus/disabled/sizing,
add one of these instead of `btn-primary`/`btn-outline-secondary`/etc for the colour). Linked from
both `_Layout.cshtml` and `_ShopLayout.cshtml`; only use these classes on customer-facing markup
(storefront, `_ProfilePostTask`, `_ProfileTasks`) — never on provider or admin views.

```html
<button type="submit" class="btn btn-uc-primary">Post task</button>
<a class="btn btn-sm btn-uc-outline btn-uc-pill" href="/cart">Cart</a>
```

`PortalControllerBase.SignInAsync(attempt, viewName, model, onSuccess?)` takes an optional
`Func<AuthResponse, Task> onSuccess`, awaited right after the cookie is issued — use it for anything
that needs the just-authenticated user's id and role in the same request (the cookie itself only
takes effect on the *next* request, so `User` is still anonymous here). `CustomerController.Login`
uses it to merge the guest cart into the account: `auth => _cart.MergeIntoAccountAsync(auth.User.Id, ct)`.
