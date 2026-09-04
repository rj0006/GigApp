# Implementation log

Business rules that were decided in conversation and are **not obvious from the code**.
The Flutter apps have to reproduce every rule here, so this file is the contract between the
web portals and mobile. Newest entry first.

Each entry says: what was asked, the rule as implemented, and where it lives so a mobile
developer can call the same thing.

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

It is derived, not stored — `GigTaskDto.PartnerChangedSkill` compares the two ids, so it disappears
by itself if the partner changes back. Only customers who actually have that partner assigned see
it; nobody else is told anything.

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
job.

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
