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
