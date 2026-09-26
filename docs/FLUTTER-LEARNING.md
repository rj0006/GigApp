# Learning Flutter — my own notes

This file is for me, not for Claude. It explains the mobile app (`provider_app` and
`customer_app`) in simple language, so I can read it, understand what has been built, and make
small changes myself — even on a day Claude is not available.

**How this file grows:** whenever I ask Claude to save something here ("isko note kar do", "yeh
likh do"), it gets added under "Notes I asked Claude to save", at the end. Everything else in this
file was written once, as a starting point, to explain the basics.

---

## 1. What is Flutter

Flutter is a toolkit made by Google. I write the app once, and it runs on Android, iPhone, and web,
from the same code. That is why this project has `provider_app` and `customer_app` as separate
folders — one app for partners, one for customers — but both are built the exact same way.

## 2. What language is the code in

The language is **Dart**. It looks a lot like JavaScript or Java:

- `class`, `if`, `else`, `for`, `while` — same as most languages
- `final name = 'Shivam';` — `final` means this value will not change again
- `async` / `await` — used whenever the app talks to the server (network calls take time, so the
  app waits without freezing)
- Every visible thing on screen — a button, a text, a whole page — is written as a **widget**, which
  is just a Dart class. Widgets are nested inside each other, like Russian dolls, to build a screen.

A very small example, so the shape is familiar:

```dart
Text('Hello, Shivam')          // shows text on screen
ElevatedButton(                 // a button
  onPressed: () { print('tapped'); },
  child: Text('Sign in'),
)
```

## 3. What app (IDE) to use, and how to open the project

**VS Code is already installed on this machine**, and it already has the Dart and Flutter
extensions added. Nothing more to install.

To open the provider app:

1. Open VS Code
2. `File` → `Open Folder`
3. Choose `C:\Users\Administrator\Desktop\GigApp\provider_app` (or `customer_app` for the
   customer side)

To run it and see it on screen (Chrome, since a phone build needs the Android SDK to work, and that
has its own issue on this machine right now — see the implementation log for the exact reason):

```bash
flutter run -d chrome
```

To make a change and see it immediately:

1. Edit a `.dart` file
2. Save it (`Ctrl+S`)
3. In the terminal where `flutter run` is running, press **`r`** — this is called **hot reload**,
   the app updates in under a second without restarting

## 4. Where things live in this project

```
lib/
├── presentation/   ← Screens, buttons, colors, everything visible.
│                      This is where I will make MOST changes — text, layout, colors.
├── data/           ← Talks to the GigApp server (the ASP.NET Core backend).
│                      Turns the server's JSON reply into something the app can use.
├── domain/         ← Plain descriptions of things — what a "GigTask" or "Bid" IS,
│                      with no logic in them. Just the shape of the data.
└── core/           ← Setup code: which server to talk to, how login tokens are stored,
                       what colors the app theme uses.
```

**Rule of thumb:** if I want to change how something *looks* (a color, a label, spacing, a button),
the file is almost always under `presentation/`. If I want to change what data comes from the
server, that is `data/`. I should rarely need to touch `core/`.

### Finding the right file

Every screen has its own file, named after what it shows:

- `presentation/auth/screens/login_screen.dart` — the sign-in screen
- `presentation/dashboard/dashboard_screen.dart` — the partner's home screen (available work, my
  bids, my jobs)
- `presentation/tasks/my_tasks_screen.dart` — the customer's task list (in `customer_app`)

Small reusable pieces live in a `widgets/` folder next to the screen that uses them — for example
`presentation/dashboard/widgets/my_job_card.dart` is the little card shown for each job.

## 5. A few Dart words I will keep seeing

| Word | What it means |
|---|---|
| `widget` | Anything drawn on screen — a button, a row of text, a whole page |
| `State` / `setState` | A widget's own memory of something that can change (like text typed into a box) |
| `async` / `await` | "Wait for this to finish before moving to the next line" — used for anything talking to the server |
| `null` | Means "nothing here yet". A `String?` (with a `?`) is allowed to be `null`; a `String` (no `?`) is never allowed to be `null` |
| `provider` (small p, Riverpod) | A place the app stores shared information, like "who is logged in right now", so any screen can read it |

## 6. If I get stuck

- Read the error message in the terminal first — Dart errors usually say exactly which file and
  line number the problem is on.
- `flutter analyze` (run in the terminal, inside `provider_app` or `customer_app`) checks the whole
  project for mistakes without running the app — good to run after any change.
- Ask Claude to explain a specific file or error rather than rewrite something — reading the answer
  is how I learn to do it myself next time.

---

## Notes I asked Claude to save

### 2026-09-15 — Building the app for my real phone (why WSL, and the exact commands)

**Why this is needed at all:** building an Android app normally happens directly on Windows, but
this machine hit a genuine Windows bug (Java's networking code fails in a very specific way on this
Windows build) that stops a normal Android build from completing. The fix that works is to build
the app inside **WSL (Windows Subsystem for Linux)** instead — a real Linux environment running
inside Windows — since that bug does not exist on Linux. Everything else (writing code, editing
files) still happens normally on the Windows side, in `C:\Users\Administrator\Desktop\GigApp`. Only
the final "turn my code into an app file (APK)" step happens inside WSL.

**One-time setup already done** (nothing to redo): WSL2 is installed, and inside it, at
`~/gigapp/provider_app`, a full Flutter + Android SDK setup already exists.

**Every time I make a change and want to test it on my phone, these are the exact steps:**

1. **Copy the latest code from Windows into WSL.** Open a terminal (PowerShell or the Terminal app)
   and run:
   ```bash
   wsl -d Ubuntu -e bash -c "rsync -a --delete --exclude 'build' --exclude '.dart_tool' /mnt/c/Users/Administrator/Desktop/GigApp/provider_app/ ~/gigapp/provider_app/"
   ```
   This copies every changed file from the Windows folder into the Linux copy. It always copies
   fresh, so I never need to worry about old files being left behind.

2. **Build the APK inside WSL.** Still in the same terminal:
   ```bash
   wsl -d Ubuntu -e bash -c "export ANDROID_HOME=\$HOME/android-sdk; export PATH=\$HOME/flutter/bin:\$ANDROID_HOME/cmdline-tools/latest/bin:\$ANDROID_HOME/platform-tools:\$PATH; cd ~/gigapp/provider_app && flutter build apk --debug"
   ```
   The first build after a big change can take a minute or two; a normal small change usually takes
   under a minute. It ends with a line saying `✓ Built build/app/outputs/flutter-apk/app-debug.apk`
   when it has worked.

3. **Copy the finished APK back out to Windows**, so `adb` (the Android tool on the Windows side)
   can reach it:
   ```bash
   wsl -d Ubuntu -e bash -c "cp ~/gigapp/provider_app/build/app/outputs/flutter-apk/app-debug.apk /mnt/c/Users/Administrator/Desktop/GigApp/provider_app-debug.apk"
   ```

4. **Install it on the phone.** Phone must be plugged in by USB with USB debugging turned on
   (Settings → About phone → tap "Build number" 7 times → Developer options → USB debugging).
   ```bash
   adb devices
   ```
   should list the phone. Then:
   ```bash
   adb install -r "C:\Users\Administrator\Desktop\GigApp\provider_app-debug.apk"
   ```
   `-r` means "replace the old version" — safe to always include.

5. **Let the phone reach the backend server.** The backend runs on this computer at
   `localhost:5245`. The phone needs a bridge to reach that same address as if it were its own
   `localhost`:
   ```bash
   adb reverse tcp:5245 tcp:5245
   ```
   This has to be run again every time the phone is unplugged and replugged (it resets on
   reconnect). If the app shows "Could not reach the server", this is the first thing to check —
   also check the backend itself is actually running (`dotnet run` in the `GigApp.Api/GigApp.Api`
   folder).

6. **Open the app on the phone** (or run this to launch it straight from the terminal):
   ```bash
   adb shell am start -n com.example.provider_app/.MainActivity
   ```

**If something goes wrong:** run `flutter analyze` inside `provider_app` on the **Windows** side
first (`cd provider_app` then `flutter analyze`) — this catches most mistakes in seconds without
needing a full WSL rebuild, since analysis does not need the special Linux build step, only the
final APK does.
