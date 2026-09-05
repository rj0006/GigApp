# Reusable helpers

Check this file before writing anything new. If a helper already exists here, **use it — do not write
it again**. When you add a new reusable piece, add one line for it here.

---

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

The confirm handler cancels the first submit, waits for the dialog, then re-submits — carrying the
clicked button name and value, so a form with two named submit buttons keeps working.

```html
<form method="post" asp-action="UpdateSkill"
      data-confirm-title="Change your skill?"
      data-confirm="@PartnerKyc.SkillChangeWarning"
      data-confirm-ok="Yes, change my skill"
      data-confirm-changed="skillCategoryId">
```

---

## C# — masters

A single endpoint serves every master: **`GET /api/masters/{key}?term=&limit=`**
The response is always `{ success, data: [{ id, name, hint }] }`.

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
| `_LoginCard` | `LoginViewModel` | Shared sign-in card for all three portals. Heading, subtitle and footer come from `ViewData`. |
| `_PartnerTable` | `AdminPartnersViewModel` | Partner list with an eye button per row and a pager. Fill `Accounts` to add a super-admin "Manage" column. |
| `_KycModal` | `KycModalViewModel` | Admin review modal — Review and History tabs, Approve/Reject. |
| `_KycReview` | `PartnerDto` | Just the three KYC images plus the Aadhaar number. |
| `_PartnerInfoModal` | `PartnerPublicDto` | Customer-facing partner card. **Never pass a PartnerDto here.** |
| `_UserAccountModal` | `UserDto` | Super-admin password reset and activate/deactivate. Render it only when `ViewData["IsSuperAdmin"] is true`. |
| `_KycHistory` | `IReadOnlyList<KycHistoryEntryDto>` | Read-only KYC timeline, newest first. |
| `_AddAddressModal` | `string` portal slug | The one add-address dialog. Renders `_AddressForm` in a modal and returns to the page it was opened from. Use it anywhere an address is needed. |
| `_UserMenu` | `UserMenuViewModel` | Navbar avatar with a Profile / Sign out box, plus an optional notification bell. Used by both layouts. |
| `_LedgerTable` | `PagedResult<LedgerEntryDto>` | Money statement with the task behind each row. Shared by the partner and admin screens. |
| `_MyJobs` | `ProviderDashboardViewModel` | Partner job table with Start / Complete / Cancel. Flags any job whose category no longer matches the partner's skill. |
| `_ProfileBody` | `ProfilePageViewModel` | Profile shell: left menu plus the section named by `Model.Section`. |
| `_ProfileDetails` / `_ProfileBank` / `_ProfileAddresses` / `_ProfileKyc` / `_ProfileSettings` | `ProfilePageViewModel` | The profile sections. Add a new one by adding to `ProfileSections`, the menu list in `_ProfileBody`, and a `GET` on `PortalControllerBase`. |

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
