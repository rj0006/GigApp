# Implementation log

Business rules that were decided in conversation and are **not obvious from the code**.
The Flutter apps have to reproduce every rule here, so this file is the contract between the
web portals and mobile. Newest entry first.

Each entry says: what was asked, the rule as implemented, and where it lives so a mobile
developer can call the same thing.

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
