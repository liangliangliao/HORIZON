# v0.4 playable product iteration

Historical iteration. The current playable thirty-day campaign and expanded predictions are documented in [v0.5](COMPLETE_GAME_V05.md).

This iteration keeps the v0.3 game loop and addresses repeat play, onboarding,
readable reinforcement, storage reliability and continuous preview updates.

- First life: contextual day 1–3 guidance explains the deadline, where to release
  a card and why a planted echo arrives while another action can be chosen.
- Goal button reports **currently prepared** gates, not a guarantee about D12.
  A newly prepared gate is acknowledged in the durable action receipt.
- Catalog 5: seeded independent six-day decks for each intent from life 2 onward.
  First life and catalogs 1–4 keep their exact hands. Recovery always remains
  playable. Six additional music, book, collaboration, work and recovery actions
  enter life 3 alongside three additional probabilistic environment situations.
- Stored seed and catalog version reproduce choices, forecasts, ghosts and past
  lives without rerolling. Version 7 snapshots can still restore earlier rules.
- Growth, support, difficult echoes and multi-node cascades have different
  feedback intensity. Negative consequences do not trigger a success shower.
  Per-page presentation and deadline reward presentation are saved and idempotent.
- Settings: effects, ambience, haptics, reduced motion and battery saver. Battery
  saver uses 30 fps and disables real-time shadows/bloom. Reduced motion suppresses
  flashes, camera shake and most particles while retaining readable consequences.
- Android Back closes the current inspection or pauses at a readable point.
  Future-station dialogue waits while paused. Background/quit saves the real life.
- Checksummed, flushed temporary-file writes rotate a verified prior backup.
  Loading tries primary, backup and legacy PlayerPrefs; unreadable originals are
  preserved. A newer unsupported archive blocks writes instead of overwriting it.
- Settings exports a complete JSON backup to clipboard and a local file. Import
  validates first, previews life count/day/currency, requires an explicit in-game
  confirmation and keeps a separate pre-import rollback copy. No account required.
- Independent preview package and committed **public QA-only** signer support
  future APK updates and coexist with earlier prototypes. Production signing is
  separate and fails closed without a private release key. See
  [preview signing](../tools/signing/README.md).

## Verification

The earlier 73 checks remain frozen to catalog 4 where they use historical cards;
new checks cover catalog 5 variation, resume/observation consistency, 96 seeds with
always-available recovery, and multiple reachable three-gate routes over 12 seeds.
Additional storage checks exercise truncation recovery, tampering, unsupported
versions, import rollback, legacy migration and persisted receipt/preferences.
The playable flow exercises real Unity settings, clipboard, Back and restore.
CI captures 25 actual portrait PNGs and decodes the existing ten-second GIF.
`artifacts/balance-v5.csv` contains the route search results. Automated reachability
checks are useful regression evidence and do not replace player balancing sessions.

## Acceptance still needed before release

- Physical Android phones: continuous upgrade, clipboard round trip, audio,
  vibration, background/resume, notches, external sharing and thermal/frame pacing.
- First-time players: identify the target without explanation, understand the
  returned echo and deadline, and voluntarily start a second life. Measure real
  time, confusion and abandonment; do not claim a 15-minute first life from scripts.
- Further authored character acting, music, more station responses and scenarios.
  The 3D diorama is procedural; assets/animation are not final production art.
- Thirty days remains an observation/simulation extension rather than a separate
  fully playable campaign. Release-store signing and distribution remain separate.
