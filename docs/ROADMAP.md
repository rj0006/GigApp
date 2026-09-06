# Roadmap

Web and API first, then Flutter. The reasoning: schema and business rules are the
expensive things to get wrong, and this project has already changed its schema
eight times. Every one of those would have meant reworking mobile too if both
were being built in parallel.

**The catch to watch.** The Razor portals call the C# services directly, not over
HTTP, so API endpoints get less exercise than portal pages do. Before Flutter
starts, every endpoint mobile needs gets tested as a mobile client would — over
HTTP, with a token, without touching a portal page. Otherwise the first week of
the Flutter phase is spent fixing API bugs instead of building screens.

**Design mobile's tables during the web phase.** Wallet, ratings, addresses and
notification preferences all need tables. Designing them now means mobile is
only UI work later.

---

## Done

| # | Item |
|---|---|
| 1 | JWT auth, one identity table, phone-first with email optional |
| 2 | Skill category master, admin-managed, referenced by FK |
| 3 | Service items under categories — the unit prices attach to |
| 4 | Partner KYC: selfie plus both Aadhaar sides, admin review modal |
| 5 | Bidding with counter-offers, atomic award |
| 6 | Task urgency, and a booking mode column ready for instant booking |
| 7 | TrackingLog audit trail, automatic on every write |
| 8 | Profile pages for all three roles, photo and password |
| 9 | Price insights — median and quartiles from settled amounts |
| 10 | Server-side pagination, Bootstrap shell, reusable autocomplete |

---

## Next

Ordered by dependency, not by how visible each one is.

### Phase A — location and addresses

Addresses and location are the same job. "Manage addresses" is where latitude and
longitude actually come from, and nothing can be auto-assigned without distance.

| Item | Notes |
|---|---|
| `Address` table | Per user, many per user, one default. Label, line, city, pincode, lat, lon |
| Task uses a saved address | Replaces the free-text address field |
| Partner service area | Base location plus a radius they will travel |
| Distance query | Postgres `earthdistance`, or store lat/lon and compute in SQL |

### Phase B — ratings

Not just a display feature. Rating is an input to the assignment score, so
instant booking cannot be built properly before it exists.

| Item | Notes |
|---|---|
| `Rating` table | Per completed task, both directions — customer rates partner and vice versa |
| Aggregates on partner | Average and count, so the assignment query does not aggregate per request |
| "My rating" screen | For both roles |

### Phase C — instant booking

Needs Phase A and B. This is the Urban Company model: fixed price, system picks
the partner.

| Item | Notes |
|---|---|
| Assignment scoring | Distance, rating, current load, acceptance rate, fairness |
| Dispatch | Broadcast to the nearest few, first accept wins; or sequential with a timeout |
| Slot and capacity | A partner cannot hold two jobs in the same slot |

### Phase D — money

| Item | Notes |
|---|---|
| Partner payout ledger | What a partner earned per job, and what is owed. See the note below |
| Customer wallet | Balance, credits, refunds. See the note below |
| Payment methods | Deferred — never store card data ourselves, use a gateway token |

### Phase E — engagement and support

| Item | Notes |
|---|---|
| `NotificationPreference` | Per user per channel: WhatsApp, push, email, SMS, voice |
| Notification sending | Needs a provider. Preferences without it are a settings screen that does nothing |
| `SupportTicket` | Help and support, with the task attached |
| Profile completeness | The "Incomplete profile" prompt — derived, not stored |

### Phase F — customer booking history

| Item | Notes |
|---|---|
| "My bookings" split | Upcoming, completed, cancelled |
| Per-task feedback | Rating plus attachment after completion |
| Repeat booking | Book the same service again in one tap |

---

## Infrastructure — the six pieces that are not written yet

Everything above is application code. These are the outside services and libraries the
application needs before it can run for real customers. Nothing here is optional at launch
except where marked.

Ordered by **lead time first, then dependency** — two of these need paperwork from somebody
else, so they start before the code that uses them.

### 1. SMS and OTP — start this week, it is the longest pole

| | |
|---|---|
| **What** | MSG91 or Twilio, plus **DLT registration with an Indian telecom operator** |
| **Why now** | DLT approval of the entity, the sender id and every template takes **one to two weeks**, and it is paperwork, not code. Nothing can be coded around it |
| **Blocks** | Phone verification, partner assignment alerts, booking reminders — nearly every notification |
| **Ready for it** | `User.IsPhoneVerified` and `PhoneVerifiedAt` exist and are enforced nowhere. A challenge table and the provider client are what is missing |

Register the templates for OTP, task assigned, task completed and payout sent at the same
time. Each one is approved separately, so submitting them together saves a second wait.

### 2. Payments and partner payouts — Razorpay

| | |
|---|---|
| **What** | Razorpay Checkout for customers, Razorpay Route or Payouts for paying partners |
| **Why Razorpay** | UPI, cards and netbanking in one integration, and payouts straight to a bank account. In India the alternative is stitching a PSP to a separate payout rail |
| **Lead time** | KYC of the business entity. Days, not weeks, but it is not instant |
| **Ready for it** | `BankAccounts` holds account number, IFSC and UPI id per user |
| **Depends on** | The ledger described under **Wallet is a money system** below. Do not connect a payment gateway to a balance column |

Webhooks are the part people get wrong: every payment event has to be **idempotent**, because
Razorpay retries. One `PaymentEvent` table keyed on the provider's event id, checked before
anything is applied.

### 3. Background jobs — Hangfire

| | |
|---|---|
| **What** | Hangfire, backed by the same Postgres database |
| **Why** | Payout scheduling, reminder sends, auto-cancelling stale tasks and retrying failed webhooks all need to happen **outside a web request**. Right now the application has nowhere to put them |
| **Free win** | Hangfire's dashboard shows failed jobs and retries them, which is the whole reason not to hand-roll a timer |

This one has no external dependency, so it can be built the moment it is needed. It is listed
third because payments and SMS are what create the jobs.

### 4. Push notifications — Firebase Cloud Messaging

| | |
|---|---|
| **What** | FCM for both Flutter apps, plus a `DeviceToken` table and a `Notification` table |
| **Why** | A partner who is not told about a new task in their category will not bid on it. This is the difference between a marketplace and a listings site |
| **Depends on** | The Flutter apps existing, so it lands with the mobile phase |
| **Note** | The **notification preferences** screen has no meaning until this exists — see the section below |

### 5. Geo matching — PostGIS — **done**

| | |
|---|---|
| **What** | PostGIS enabled; `GigTasks.Location` and `Partners.BaseLocation` are `geography(Point, 4326)` with GIST indexes, backfilled from the existing coordinates |
| **Why** | "Nearest partner" is the assignment algorithm. Computing distance in C# over every partner does not survive a few thousand rows |
| **What it drives** | The partner's board filters by their own radius and sorts by distance; `/admin/tasks` offers a shortlist ranked on distance and rating |
| **Still open** | Automatic assignment. The ranking exists but a human picks, because switching it on needs a notification channel and an acceptance window for when the best partner does not answer — item 1 and item 4 below |

### 6. File storage — object storage instead of local disk

| | |
|---|---|
| **What** | Cloudflare R2 or Amazon S3 behind the existing `IFileStorageService` |
| **Why** | KYC documents currently sit on one server's disk. That disk is a single point of failure holding Aadhaar images, and it cannot be shared once there is more than one application server |
| **Effort** | Small — `IFileStorageService` already hides the storage detail, so this is one new implementation and a config switch |
| **Urgency** | Before the second server, or before the first real partner uploads a real Aadhaar card. Whichever comes first |

---

### Suggested order

1. **Start DLT registration now** — it runs in the background while everything else is built
2. ~~Ratings~~ — **done**, and feeding the match score
3. ~~PostGIS~~ — **done**, and feeding the match score
4. The **money ledger** — done; Razorpay on top of it is next
5. Hangfire, once there are scheduled payouts to run
6. Object storage, before real KYC documents arrive
7. FCM and the notification screens, with the Flutter phase

Both inputs to assignment now exist. What still blocks automatic assignment is not the algorithm —
it is having somewhere to send the offer (FCM or SMS) and a rule for what happens when nobody
answers. Build the channel first; the auto-assign is small once it is there.

---

## Two things to settle before building them

### Wallet is a money system, not a balance column

A wallet holding customer money needs more care than a `decimal Balance`:

- **A ledger, not a balance.** Every credit and debit is a row; the balance is
  their sum. A single mutable balance column has no audit trail and cannot be
  reconciled when something goes wrong.
- **Idempotency.** A retried refund must not credit twice. Every transaction
  needs a caller-supplied key that is unique.
- **Never floating point.** `numeric`/`decimal` only, which is what the schema
  already uses.
- **Regulation.** Holding customer balances in India touches prepaid instrument
  rules. Worth confirming before storing real money, even if a closed-loop
  wallet is lighter-touch.

Recommendation: build the **partner payout ledger first**. It is the same
double-entry shape, it is money we owe rather than money we hold, and it carries
none of the regulatory questions. The customer wallet can reuse the same design
once that is settled.

### Notification preferences without a sender

The preference table is trivial, and OTP will need an SMS provider anyway. But
building the toggles first means shipping a settings screen where nothing
happens. Either accept that openly, or wait until a provider is wired up.

Recommendation: build the table alongside OTP, so the first toggle that appears
is one that actually controls something.
