# Transparent desktop input regression

The withdrawn DWM backend used constant HWND alpha plus `HTTRANSPARENT` on a
full-screen window. Windows only searches the same thread for that hit-test
result; it does not make an overlay transparent to other applications.
See [WM_NCHITTEST](https://learn.microsoft.com/windows/win32/inputdev/wm-nchittest).

The host now consistently uses WPF per-pixel transparency, irrespective of the
saved experimental renderer preference. No mouse hooks, style-change hooks,
constant HWND alpha, or extra compositor windows are introduced. Public methods
remain available to existing callers. The unsafe setting is hidden. The PDF
plugin supports transparent hosts without recommending the unsafe renderer.

Validation is deliberately split:

- `SettingsPersistenceTests --desktop-contract-only`: verifies both saved
  preference values, transparent HWND configuration, stable HWND lifetime and
  2,000 tool changes without replacing per-pixel alpha. Eight checks passed.
- `SettingsPersistenceTests --desktop-surface-only`: checks rendered alpha-zero
  background and visible toolbar pixels. Requires a working rendering desktop.
- `SettingsPersistenceTests --desktop-input-only`, or launch `DesktopInputTests.exe`:
  creates its own external-process click target and overlay. Sends real input to
  the external process and toolbar, switches drawing/mouse modes repeatedly,
  and checks retained ink. Never substitute `WM_NCHITTEST` or `WindowFromPoint`
  assertions for this test. The standalone runner writes `desktop-input-result.txt`
  beside its executable. Run only on an unlocked, interactive desktop.

At preparation time the environment denied `GetCursorPos`/`SetCursorPos` and the
computer-use launcher also reported access denied. Render snapshots were empty,
including existing drawing/ink fixtures. Therefore the surface and physical
input tests are **not passed**; those failures are not waived or reported as
successful verification. Word logic (306), Word navigation/settings (18) and PDF
reading/window controls (216) passed. Main Debug build had zero warnings/errors;
Release x64 self-contained publish succeeded. No speedup percentage is claimed.

PDF on this transparent host uses fallback window controls for now. Native
captions remain supported on non-transparent hosts. Maximize and fullscreen stay
separate; toolbar centering is retained.
