# Claude's standing memory, backed up into the project

Claude Code keeps a personal memory of working-style rules and project context outside this repo,
at `C:\Users\Administrator\.claude\projects\...\memory\`. That location is tied to this machine's
user profile, not to this project, so it does not survive a Windows reinstall or a move to a new
machine. This file is a copy of that memory's content, checked into git, so it survives either way
and can be read back and re-applied in a fresh session.

Each entry below is a point-in-time note, not a guarantee about current code — check the file paths
and behaviour it mentions against the live code before treating it as fact, the same way the memory
system itself warns.

---

## Strict: no code comments

Zero comments in code — no `///` summary blocks, no block comments, no section-banner comments — in
C#, JavaScript, Razor, SQL, or Dart. If one line is genuinely necessary to explain something
non-obvious, that single line is the absolute ceiling. Never write the same comment more than once
across the codebase — if an explanation is needed in more than one place, it belongs in this
repository's `CLAUDE.md` or `docs/REUSABLE.md`, not repeated inline.

**Why:** Already `CLAUDE.md` working-agreement rule 6, but the user called this out explicitly after
noticing drift — mid-session, multi-line XML `<summary>` doc comments got added to new C# code before
being self-caught and trimmed down. The user does not want "caught after the fact" — this is to be
followed strictly from the first edit of a session, not applied only on review.

**How to apply:** Before writing any comment, check: is this the one most-necessary line, not a
restatement of what good naming already says? Has this exact explanation been written anywhere else
in the codebase already — if so, point to `CLAUDE.md`/`docs/REUSABLE.md` instead of repeating it.
When extending a file that already has pre-existing multi-line doc comments (violations that predate
this rule), do not add more in that style even to match the surrounding code — new work follows the
strict rule regardless of what is already there.

---

## Be terse, no narration

Stop narrating the investigation (files checked, theories tried, tests run, step-by-step debugging)
in chat replies. Give the code change and a one or two line plain-English summary only. Reserve
longer explanation for cases that genuinely need it (breaking changes, ambiguous scope) — not for
walking through how a bug was found.

**Why:** Said multiple times across sessions on this project; explaining every diagnostic step burns
tokens and reading time the user does not want. Also `CLAUDE.md` working-agreement rule 1.

**How to apply:** After making a code change, check whether the reply explains what was done to find
the bug, or just what the bug/fix was. Cut the former.

---

## Share one pagination API across web and mobile

Reuse the same paginated endpoint (and the same "Load more" / page-pageSize pattern) for both the web
portal and the Flutter apps whenever one API can serve both — never write a second, platform-specific
pagination endpoint for the same underlying data.

**Why:** The user asked for this as a standing rule: "jaise mobile me 'show more' ka option pagination
ke liye diya wahi web me bhi use karo agar ek hi api se dono jagah kam ho jata hai to alag-alag
pagination ke api ki jarurat nahi hai code banane ki."

**How to apply:** Before adding a new paginated list anywhere, check whether the other platform
already has, or will need, the same data. If so, build one shared endpoint (`PagedResult<T>` via
`PagingExtensions.ToPagedResultAsync`) and consume it from both sides. On the Flutter side the current
shared shape is `presentation/common/paged_list_controller.dart`'s generic `PagedListController<T>`
(a `StateNotifier` that appends pages) — an earlier, narrower `LedgerController` this note originally
pointed at has since been replaced by that generic controller. When retrofitting an existing web
page's own separate pagination toward this shared shape, treat it as its own task rather than folding
it silently into unrelated mobile work — confirm scope with the user first, since it touches shipped
web UI.

---

## Phone/OTP was the planned direction (now largely built)

On 2026-08-16 the user stated that customer and partner verification would move to mobile
number/OTP, and asked that login/signup be built accordingly from the start rather than retrofitted
later. The auth layer was built phone-first ahead of that: `User.Phone` is the required primary
identifier, `User.Email` is nullable, login takes an `Identifier` (phone or email).

**Status as of this backup:** OTP sign-in has since actually landed for the customer and partner
portals and mobile — see this repository's `CLAUDE.md` under "Mobile OTP is the default sign-in for
Customer and Partner" for the current, authoritative shape. This memory entry is kept for the
historical reasoning (why phone was made primary and email nullable up front, before OTP itself was
built) rather than as a live to-do.

---

## The Razor admin portal is permanent; admin_panel/ is not

The user agreed on 2026-08-16 that the server-rendered Razor `/admin` portal is the permanent admin
panel, and that `admin_panel/` (an untouched Flutter template) should be deleted rather than built
out, since no work had been invested in it yet.

**Status as of this backup:** this repository's `CLAUDE.md` now documents that the **customer** web
portal is also permanent (an SEO/acquisition channel — an app cannot be indexed by search engines),
which is a refinement of this memory's original framing that "the `/customer` and `/provider` Razor
portals are temporary harnesses." Only the **partner** portal remains replaceable by the app. Treat
`CLAUDE.md`'s current wording as authoritative; this entry is kept for the original decision date and
reasoning.
