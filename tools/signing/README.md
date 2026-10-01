# Public preview signing

`horizon-preview.keystore` is an intentionally public **QA-only** key. Its store
and alias password are `public-horizon-preview`, alias `horizon-preview`. It is
committed so independent preview builds keep one signer and can update without
uninstalling. Never use this public identity as a production trust boundary.

Preview identifier: `com.liangliangliao.horizon.preview`. It installs alongside
older prototype packages. Android separates their data: preserve the older app;
use Settings → backup/import when that version supports export. A certificate
cannot recover a previous private signing key or read another app's private data.

`-horizonChannel release` selects `com.liangliangliao.horizon` and **requires** a
separate private key via `HORIZON_RELEASE_KEYSTORE`, `HORIZON_RELEASE_STORE_PASS`,
`HORIZON_RELEASE_ALIAS`, `HORIZON_RELEASE_KEY_PASS`. Do not commit those values or
that key. Store the production key outside the project and back it up securely.
Release signing cannot fall back to the preview key. Store distribution is not
configured or authorized by this preview workflow.

Each CI build uses monotonically increasing Actions `run_number` as versionCode.
The APK check validates both ABIs, the preview identifier and the exact committed
certificate. A release needs its own review and device acceptance.
