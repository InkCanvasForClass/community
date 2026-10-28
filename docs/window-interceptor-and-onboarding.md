# Window interception and first-run setup

## EasiNote teaching windows

The EasiNote 5 / 5C floating-window rules require the known WPF class prefix, process
name, exact style and 550 × 200 dimensions (physical pixels or the window's reported
DPI scale, with 2 px rounding tolerance). The previous aspect-ratio-only alternative
could accept an unrelated, much larger window and has been removed.

A separate safety policy rejects titled, child, taskbar application, captioned,
resizable, maximized, minimized and near-full-screen windows before matching a Seewo
whiteboard rule. Unknown dimensions or DPI fail closed. The old Seewo 3 signature
identifies a titled full-screen window and is consequently skipped: it does not
provide enough evidence to distinguish a floating control from a teaching surface.
New versions with different floating-window signatures may also be skipped until
their exact characteristics are verified.

Upload/QR dialogs are also protected by their modal state: disabled windows,
`WS_EX_DLGMODALFRAME`, and windows with any disabled HWND owner in their owner chain
are excluded. This covers an untitled modal upload dialog even when its size and
style exactly match the floating bar. An ordinary owned floating bar remains eligible
after the modal operation finishes. These checks identify modal behavior rather than
guessing a localized dialog title. A modeless, untitled dialog identical in process,
class, style and dimensions still requires its actual HWND characteristics to tell
it apart; these tests do not establish a blanket guarantee for every Seewo version.
Local inspection of the installed Seewo 5.2.4.11441 components identified a
full-screen photo-display path and a QR viewer that follows its owner's bounds.
That static inspection is not a live capture of a phone upload, its QR dialog or
all possible minimized/display modes. The upload-specific regressions deliberately
use an untitled 550 × 200 fixture, so protection is tested without assuming a
particular upload dialog title or dimensions.

Candidates are checked again immediately before interception. If a tracked EasiNote
HWND subsequently becomes a teaching window, the next scan restores it. PID, thread
and class identity are checked before restoring a tracked HWND, so stale handles are
not blindly shown. This is protection against ordinary handle reuse, not a claim
that same-process HWND reuse can be identified perfectly.

Hiding and restoring use `ShowWindowAsync`. The interceptor no longer force-minimizes
or moves another program off-screen, and restoration does not resize or activate it.
An accepted asynchronous request does not mean the target thread has processed it
yet. Rule disabling is serialized with scans, and child enumeration visits each
descendant once rather than recursively re-enumerating nested subtrees.
Update shutdown also restores this process's tracked windows; a replacement process
cannot inherit those HWND identities and cannot reliably restore abandoned hidden
windows. Restoration remains asynchronous and does not activate the target.

## Repeated welcome wizard

Previously, wizard completion requested a 150 ms coalesced save and immediately
reloaded settings. That reload could replace the completed settings with the old
disk/startup-cache copy, after which the delayed save persisted `hasShownOobe=false`
again. Reset-to-recommended used the same save-then-reload pattern.

The wizard now awaits the ordered atomic settings writer before closing successfully.
Both manual completion and presets persist the completion flag together with the
selected settings. Write failures keep the wizard open with its choices intact and
display an error. Canceling the wizard does not count as successful completion.
After completion, and after reset-to-recommended, the main window applies the current
settings object without re-reading it. Explicit file reloads read the actual file;
the startup cache is restricted to startup loading.

## Verification

- `build/ReliabilityTests`: safety policy cases across eight DPI values; exclusions
  for application, child, captioned, maximized, titled and full-screen windows.
- `build/SettingsPersistenceTests`: production settings manager, real WPF dispatcher,
  delayed-save/complete/restart ordering, cache state and an unwritable destination.
- The latter test also creates its own HWND fixtures and a cloaked helper process
  whose window thread deliberately stops pumping messages. Hide/restore must return
  without waiting for it. Repurposing its popup HWND as a titled teaching window must
  cause the interception state to be released and restoration requested.

These tests do not replace validation against an installed Seewo version and its
actual classroom display/driver configuration.
