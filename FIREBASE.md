# Firebase — ACEDRIVE online layer

The overlay runs fully offline without this setup: XP standings show the last
saved rank, car rarity stays at 1, and name reservation is skipped. The online
layer is additive, exactly like the LATESTDRIVEBS2 game.

## 1. Firebase project

Reuse the existing `latestdrivebs` project (same console as the Unity game).
Enable **Authentication → Sign-in method → Anonymous** and **Firestore**.

ACEDRIVE writes only the `acevo_*` collections. The Unity game keeps its
un-prefixed collections; `firestore.rules` in this repo contains both, so a
deploy cannot break the sibling game.

## 2. Client configuration

Create `%AppData%\AceDrive\firebase.json`:

```json
{
  "projectId": "latestdrivebs",
  "apiKey": "<Firebase web API key>",
  "storageBucket": "latestdrivebs.appspot.com"
}
```

The web API key is a public client key, but it is intentionally kept out of the
repository (AGENTS.md: no keys in source). If anonymous sign-in fails with
`API_KEY_ANDROID_APP_BLOCKED` / `REQUEST_DENIED`, the key copied from the Unity
`google-services.json` is application-restricted to Android: create a browser
key (or remove the application restriction on this key) and put that in
`firebase.json`. Environment overrides win over the file:

| Variable | Meaning |
|---|---|
| `EVO_FIREBASE_PROJECT` | project id |
| `EVO_FIREBASE_API_KEY` | web API key |
| `EVO_FIREBASE_BUCKET` | optional storage bucket |

Delete the file (or leave it absent) to force the offline null-objects.

The anonymous refresh token is stored at
`%LocalAppData%\AceDrive\firebase_session.json`. It is a credential and never
leaves the machine.

## 3. Deploy the rules

From the repository root:

```powershell
firebase deploy --only firestore:rules
```

`firestore.rules` includes the Unity collections too; do not replace it with a
file that only contains `acevo_*`.

The weekly valuation list can additionally use a Firestore TTL policy on
`acevo_enteredForValuation.expiresAt` (48 h), but the client-side Monday wipe
is the source of truth.

## 4. Collections

| Collection | Doc id | Fields |
|---|---|---|
| `acevo_players` | anonymous uid | `xp`, `displayName`, `longestZoneKm`, `highestShiftEarnings`, `highestShiftEarningsFirstEarnedAt`, `lastUpdatedAt` |
| `acevo_cars` | `carId` or `carId#preset` | `totalKmDriven`, `lastUpdatedAt` |
| `acevo_countries` | normalized country | `totalKmDriven`, `displayName`, `lastUpdatedAt` |
| `acevo_playerNames` | normalized name | `ownerUid`, `displayName`, `claimedAt` |
| `acevo_roadTestStandings` | `uid\|carId` | `uid`, `playerName`, `time`, `carId`, `v`, `lastUpdatedAt` |
| `acevo_roadTestBrakeStandings` | `uid\|carId` | same, `brakeMeters` |
| `acevo_enteredForValuation` | uid / `house-entry` | `uid`, `playerName`, `carId`, `jobsWithCar`, `engineBrokenPercent`, `score`, `enteredAt`, `expiresAt` |

## 5. What is wired

Live wiring:

- anonymous auth and session resume;
- XP world standings (`acevo_players.xp`) on every settled taxi/delivery;
- global car km → dynamic rarity rank used by the taxi XP formula;
- player name reservation on the opening screen;
- longest operating zone on expansion;
- best single-shift earnings on shift end;
- per-country km on every paid trip.

Backend-only today (no ACEDRIVE screen/caller yet, compiled and tested):
road-test time/brake boards, weekly valuation entries, cloud car sprites.

## 6. Rarity model

`CarRarityService` parity: `acevo_cars` is sorted by `totalKmDriven` descending,
rank 1 is the most-driven car and the last rank the rarest. A car missing from
the board answers `carCount + 1` (never driven); an empty key answers 1. While
the board is unavailable every car answers rarity 1. Unowned free-roam cars
earn fallback class 1 × rarity 1 and never increment the car board.
