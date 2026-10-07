# Desktop Baskets sharing core

Native Rust child process hosted by Desktop Baskets. There is no Tauri, WebView2,
Node.js, HTML frontend or separate PocketDrop application in the shipped runtime.
Only the shared basket starts this process. Closing the owning process closes its
stdin and terminates the listener; explicit stop/shutdown also closes it.

The LAN core derives from the same owner's PocketDrop 1.0.6, commit
`f20173b9788d13be838d2ca036c1c465e4008046`. Files `phone.rs`,
`phone/pairing.rs`, and `phone/peer.rs` retain the existing protocol, certificate
pinning, SRP pairing, DPAPI persistence, on-demand source relay, limits and
stream validation. Android protocol fields and the `_pocketdrop._tcp.local.`
service name are retained for compatibility. The project owner has not selected
a source license for either repository.

Host changes: inherited JSONL stdio instead of Tauri commands/events, event
deduplication before UI delivery, two Tokio worker threads, one bounded file hash
worker, repeated-drop deduplication, local-only source path lookup, Desktop Baskets
Room naming and download directory. The public LAN snapshot never contains
original full file paths. Only the owning local process receives those paths.

Build with Rust MSVC: `cargo build --release --locked`. The packaging script copies
`desktop-baskets-share.exe` to `DesktopBaskets.Share.exe` beside the main executable.
The helper requires `--data` and `--downloads`. Its inherited stdio accepts
request records with `id`, `op`, and arguments and returns matching response IDs.
It opens no additional local HTTP interface for the UI.

Tests: `cargo test --locked`. Tests use temporary data and include real TLS LAN
pairing, on-demand relay, Range, certificate binding, restart rediscovery and
revocation. Physical Android interoperability remains a separate manual check.
