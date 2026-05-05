# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

School bus logistics management app (Israeli school project). Three user roles:
- **Public** (students/parents, no sign-in) — follow specific bus routes on a live map
- **Driver** — manage assigned routes, broadcast live GPS position
- **Admin** — manage users and bus routes

Backend: Firebase Auth + Firestore. Language: C# / Xamarin.Android.

## Build & Run

- **IDE**: Visual Studio 2022 (Windows) with Xamarin Android workload
- **Build**: Open `TimeToSchool.sln` → Build → Build Solution (or `msbuild TimeToSchool.csproj`)
- **Deploy**: Connect Android device or start emulator, then Deploy from Visual Studio
- **Release build**: `msbuild /p:Configuration=Release` — produces `.aab` (Android App Bundle) for Play Store
- **Debug credentials**: `yoav@gmail.com` / `123456` (hardcoded in `SignInActivity.cs` when `ProManager.DebugMode = true`)

## Architecture

**Xamarin.Android + Firebase** app with three user flows:
1. **Public** (`MainActivity`) — search bus routes by School → Town → Bus Line (no auth required)
2. **Admin** (`AdminMainActivity`) — manage users and bus routes (fragment-based tabbed UI)
3. **Driver** (`DriverActivity`) — select routes, start/stop trips, broadcast GPS location every 15s

**Layer structure:**
- `Service/` — data access layer; `UsersRepository`, `BusesRepository` wrap all Firestore ops; `FireBaseHelper` initializes Firebase from `Assets/googleservices.json`; `PreferenceService` persists session via SharedPreferences (JSON-serialized)
- `Model/` — plain data classes: `User`, `BusRoute`, `ActiveBus`
- `BusinessLogic/ProManager.cs` — global state singleton (CurrentUser, TAG for logging, DebugMode flag)
- `Fragments/` — UI fragments used inside `AdminMainActivity` (users list, buses management with ViewPager2 tabs)
- `Adapter/` — RecyclerView and ViewPager2 adapters
- `Helpers/` — `UIHelper` (keyboard, dialogs), `SelectionValidator` (cascading dropdown logic)

**Firestore collections:**
- `BusRoutes` — static route definitions (school, town, busLine)
- `users` — user profiles; `isAdmin` flag for admins, `status` field for driver approval (`pending` / `approved`)
- `ActiveTrips` — live trip tracking; written by drivers, read by public view
- `BusStops` *(planned)* — ordered stop coordinates per route, linked to `BusRoutes` by ID; used for the privacy guard (bus only appears on map after reaching the first stop)

## Project File Registration

Xamarin Android requires every file to be explicitly listed in `TimeToSchool/TimeToSchool.csproj`. After creating any new file, add the appropriate entry:

| File location | Entry type |
|---|---|
| `Resources/**/*.xml`, `Resources/**/*.png` | `<AndroidResource Include="..." />` |
| `Assets/**/*` | `<AndroidAsset Include="..." />` |
| `**/*.cs` | `<Compile Include="..." />` |
| Other (templates, docs) | `<None Include="..." />` |

Files on disk that are missing from the `.csproj` are invisible to Visual Studio and break the build.

## Key Patterns

- **Repository pattern**: Always go through `UsersRepository`/`BusesRepository` for Firestore reads/writes — never call Firestore APIs directly from Activities or Fragments.
- **Real-time sync**: Use `IEventListener` + `IListenerRegistration` (not one-shot reads) for any collection that must stay live.
- **Cascading dropdowns**: `SelectionValidator` drives the School → Town → Bus Line chain; reuse it rather than re-implementing selection logic.
- **Session persistence**: `PreferenceService` serializes/deserializes the `User` object with `Newtonsoft.Json`; used for "Remember Me" and role-based routing on app launch.
- **Logging**: `Android.Util.Log` with tag `ProManager.TAG` (`"YoavApp"`).
- **Hebrew strings**: The app is Israeli-facing; UI string literals are in Hebrew.

## Planned Features (Not Yet Built)

### Public Live Map
- Google Maps SDK for Android (Xamarin binding) — not yet added as NuGet dependency
- Bus location shown only after it passes the first official stop (privacy guard using `BusStops` collection)
- Smart header bar: ETA via Google Maps Directions API, status messages ("Bus already passed", "No active buses")

### Driver Approval Workflow
- New drivers register normally but get `status: "pending"` in Firestore
- On sign-in, pending drivers reach a "waiting for approval" screen instead of `DriverActivity`
- Admin approves/rejects from the Users management panel, which updates `status` to `"approved"` or `"rejected"`

### Admin Interface Improvements
- Better UI design for user and bus management screens
