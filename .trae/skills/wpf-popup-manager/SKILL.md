---
name: "wpf-popup-manager"
description: "Manages WPF Popup z-order (topmost) and drag-follow behavior. Invoke when using Popup controls that need to stay on top of other UI elements, follow a draggable parent container, or when converting Border/Panel menus to Popup."
---

# WPF Popup Manager

This skill provides a reusable solution for managing WPF Popup controls with two critical features:

## Features

### 1. **Topmost Management**
- Keeps Popup windows above all other UI elements (floating toolbars, canvas, etc.)
- Uses Win32 API `SetWindowPos` with proper z-order strategy
- **IMPORTANT**: Uses `HWND_TOP` + `SWP_NOOWNERZORDER` instead of `HWND_TOPMOST` to avoid z-order fighting with the main window
- Multiple strategies: initial show, animation completion, periodic maintenance

### 2. **Drag-Follow System**
- Makes Popup follow its parent container when dragged
- Uses CompositionTarget.Rendering for smooth 60fps+ synchronization
- Offset-based position updates (no window recreation)
- Zero flicker, zero performance impact

## When to Use This Skill

**Invoke this skill when:**
- Converting Border/Panel menus to Popup controls
- Popup is being covered by other UI elements
- Popup needs to follow a draggable toolbar/container
- Implementing floating tool palettes or context menus
- Any scenario requiring persistent topmost Popups
- After showing a Popup, it doesn't appear on top (forgot `BringToFront`)

## Usage

### Basic Setup

```csharp
// 1. Create manager instance
var popupManager = new PopupManagerHelper();

// 2. Configure topmost condition (bind to settings)
popupManager.ShouldBeTopmost = () => Settings.Advanced.IsAlwaysOnTop;

// 3. Register Popup(s) you want to manage
popupManager.RegisterPopup(myPopup);

// 4. Initialize in Window_Loaded
popupManager.Initialize();
```

### Show Popup with BringToFront

**CRITICAL**: Always call `BringToFront` after showing a Popup, otherwise it may appear behind other UI elements:

```csharp
AnimationsHelper.ShowPopupWithSlideAndFade(myPopup);
_popupManager?.BringToFront(myPopup);
```

### Immediate Hide

```csharp
myPopup.IsOpen = false;
```

### Animated Hide

```csharp
AnimationsHelper.HidePopupWithSlideAndFade(myPopup);
```

## Architecture

```
┌─────────────────────────────────────┐
│         PopupManagerHelper          │
│  (Centralized Management Class)     │
├─────────────────────────────────────┤
│                                     │
│  ┌───────────────┐ ┌──────────────┐│
│  │ Topmost Engine │ │Follow Engine ││
│  ├───────────────┤ ├──────────────┤│
│  │ • Win32 API   │ │ • Rendering   ││
│  │ • HWND_TOP    │ │   Sync        ││
│  │ • Owner-Owned │ │ • Offset      ││
│  │   Strategy    │ │   Updates     ││
│  └───────────────┘ └──────────────┘│
│                                     │
│  ┌───────────────┐                  │
│  │ Config & State│                  │
│  ├───────────────┤                  │
│  │ • Intervals   │                  │
│  │ • Toggle flags│                  │
│  │ • Registered  │                  │
│  │   popups list │                  │
│  └───────────────┘                  │
└─────────────────────────────────────┘
```

## Key Methods

| Method | Purpose | Performance |
|--------|---------|-------------|
| `Initialize()` | Subscribe to Rendering event | One-time setup |
| `RegisterPopup()` | Add Popup to management | O(1) |
| `BringToFront()` | Set topmost via Win32 | ~0.5ms async |
| `UpdatePosition()` | Offset-based reposition | <0.1ms sync |
| `OnRendering()` | Per-frame callback handler | Automatic |

## Z-Order Strategy: Dual TOPMOST with Owner Below

### The Problem with External Topmost Timers

Some applications use a periodic `DispatcherTimer` that calls `SetWindowPos(HWND_TOPMOST)` on the main window (e.g., every 500ms in `WindowSettingsHelper._topmostMaintenanceTimer`). This **overwrites** any z-order fix applied by `PopupManagerHelper`, causing the Popup to fall behind the main window after the timer fires.

```
MainWindow._topmostMaintenanceTimer (every 500ms)
    → SetWindowPos(mainWindowHwnd, HWND_TOPMOST)
    → Pushes main window ABOVE all Popups
    ↕ z-order fighting
PopupManagerHelper.OnRendering (every ~250ms)
    → SetWindowPos(popupHwnd, HWND_TOPMOST) 
    → Pushes Popup back above main window
```

Result: Popup and main window flicker/fight for z-order at different intervals.

### The Solution: Dual TOPMOST + Notification

Place **both** the Popup and MainWindow in the TOPMOST layer, but position the Popup **above** MainWindow using the insert-after parameter:

```csharp
// Step 1: Make Popup TOPMOST
SetWindowPos(popupHwnd, HWND_TOPMOST, 0, 0, 0, 0,
    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

// Step 2: Position MainWindow just BELOW the Popup in the TOPMOST z-order
if (_ownerHwnd != IntPtr.Zero)
{
    SetWindowPos(_ownerHwnd, popupHwnd, 0, 0, 0, 0,
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}
```

The `SWP_NOOWNERZORDER` flag on step 1 prevents the Popup's call from changing MainWindow's position. Step 2 explicitly places MainWindow right below the Popup.

### NotifyTopmostMaintained: Cooperating with External Timers

When an external timer (like `WindowSettingsHelper._topmostMaintenanceTimer`) pushes MainWindow to TOPMOST, it must notify `PopupManagerHelper` to re-fix the z-order:

**In PopupManagerHelper:**
```csharp
// Static instance tracking
private static readonly List<PopupManagerHelper> _activeInstances = new List<PopupManagerHelper>();

// Called when Initialize runs
_activeInstances.Add(this);

// Called when Cleanup runs
_activeInstances.Remove(this);

// Static notification method
public static void NotifyTopmostMaintained()
{
    for (int i = 0; i < _activeInstances.Count; i++)
    {
        _activeInstances[i].OnOwnerActivated();
    }
}
```

**In WindowSettingsHelper (external timer):**
```csharp
private static void TopmostMaintenanceTimer_Tick(object sender, EventArgs e)
{
    // ... existing logic to push main window to TOPMOST ...
    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

    // NOTIFY: Let PopupManagerHelper know main window was pushed to TOPMOST
    PopupManagerHelper.NotifyTopmostMaintained();
}
```

This ensures that every time the external timer pushes MainWindow to TOPMOST, all open Popups are immediately re-positioned above MainWindow.

### Implementation (FixPopupZOrder)

```csharp
private void FixPopupZOrder(Popup popup)
{
    if (popup?.Child == null) return;
    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
    if (source?.Handle == null) return;
    var popupHwnd = source.Handle;
    var shouldBeTopmost = CheckShouldBeTopmost();

    if (shouldBeTopmost)
    {
        SetWindowPos(popupHwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

        if (_ownerHwnd != IntPtr.Zero)
        {
            SetWindowPos(_ownerHwnd, popupHwnd, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
    else
    {
        int exStyle = GetWindowLong(popupHwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOPMOST) != 0)
        {
            SetWindowLong(popupHwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
        }
        SetWindowPos(popupHwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
```

### Key Win32 Constants

| Constant | Value | Usage |
|----------|-------|-------|
| `HWND_TOPMOST` | `-1` | Set Popup to TOPMOST layer |
| `HWND_NOTOPMOST` | `-2` | Use when main window is not topmost |
| `SWP_NOOWNERZORDER` | `0x0200` | **Critical flag** — prevents changing owner's z-order when moving Popup |
| `SWP_NOACTIVATE` | `0x0010` | Prevents window activation when changing z-order |
| `WS_EX_TOPMOST` | `0x00000008` | WPF auto-sets this on Popup when `AllowsTransparency=True`; must clear when not topmost |
| `GWL_EXSTYLE` | `-20` | Index for extended window styles |

## Popup Conversion Checklist

When converting a Border/Panel to Popup, ensure ALL of these steps are done:

- [ ] XAML: Replace `Grid Width="0">Border` with `<Popup Placement="Custom" AllowsTransparency="True" StaysOpen="True" IsOpen="False">`
- [ ] XAML: Remove negative `Margin` from inner Border (Popup handles positioning)
- [ ] XAML: Extract popup content into a UserControl in `Controls/Popups/` (e.g., `ShapeDrawPopupContent.xaml`)
- [ ] XAML: Use `<controls:PopupTitleBar>` for the title bar (from `InkCanvas.Controls/Popups/`)
- [ ] XAML: NO inline event bindings in the UserControl — wire in code-behind
- [ ] Code-behind: Set `PlacementTarget` (in XAML or `Attach*` method)
- [ ] Code-behind: Add `CustomPopupPlacementCallback` in constructor
- [ ] Code-behind: Change `Visibility == Visible` → `IsOpen`, `Visibility = Collapsed` → `IsOpen = false`
- [ ] Code-behind: Change `ShowWithSlideFromBottomAndFade` → `ShowPopupWithSlideAndFade`
- [ ] Code-behind: Change `HideWithSlideAndFade` → `HidePopupWithSlideAndFade`
- [ ] Code-behind: Add `WireUp*Events()` method with anti-duplicate guard for UserControl events
- [ ] Code-behind: Add property mappings for inner controls (e.g., `BoardImageDrawLine => ShapeDrawPopupContent?.DrawLineBtn`)
- [ ] Code-behind: Register with `_popupManager.RegisterPopup(popup)` in `InitializePopupManager()`
- [ ] Code-behind: Call `_popupManager?.BringToFront(popup)` after showing
- [ ] Code-behind: Remove `UpdateSubPanelPosition` calls (Popup positions itself)

## Best Practices

✅ **DO:**
- Call `Initialize()` once in `Window_Loaded`
- Register all Popups that need management
- Call `BringToFront()` after every `ShowPopupWithSlideAndFade()`
- Use `HWND_TOP` + `SWP_NOOWNERZORDER` instead of `HWND_TOPMOST`
- Use default config for most cases
- Let the manager handle everything automatically

❌ **DON'T:**
- Use `HWND_TOPMOST` for Popup without positioning MainWindow below — causes z-order fighting
- Use `HWND_TOP` alone for Popup when an external timer pushes MainWindow to TOPMOST
- Manually toggle `IsOpen` during drag (causes flicker)
- Call `SetWindowPos` directly (use helper methods)
- Forget to register new Popups with `PopupManagerHelper`
- Forget to call `BringToFront` after showing a Popup
- Use very short check intervals (<10 frames)
- Use alternating ±0.001 offset in `UpdatePosition` — causes visual flicker during drag

## Troubleshooting

**Issue**: Popup still gets covered
- **Solution**: Call `_popupManager?.BringToFront(popup)` after `ShowPopupWithSlideAndFade()`
- **Cause**: Forgot to bring Popup to front after opening

**Issue**: Popup and main window flicker/fight for z-order
- **Solution**: Use dual TOPMOST strategy: set Popup to `HWND_TOPMOST`, then position MainWindow just below it via `SetWindowPos(_ownerHwnd, popupHwnd, ...)`. Plus add `NotifyTopmostMaintained()` notification in the external timer.
- **Cause**: Both main window timer and PopupManagerHelper independently competing for TOPMOST

**Issue**: Popup gets covered by main window after MainWindow's periodic timer fires
- **Solution**: Add `NotifyTopmostMaintained()` to PopupManagerHelper; call it from the external timer (`WindowSettingsHelper.TopmostMaintenanceTimer_Tick`) after `SetWindowPos(HWND_TOPMOST)`.
- **Cause**: External timer pushes MainWindow to TOPMOST without notifying PopupManagerHelper to re-fix z-order

**Issue**: Popup covers other apps when main window isn't topmost
- **Solution**: Clear `WS_EX_TOPMOST` style bit via `SetWindowLong` + `SetWindowPos(HWND_NOTOPMOST)`
- **Cause**: WPF auto-sets `WS_EX_TOPMOST` when `AllowsTransparency=True`

**Issue**: Choppy movement during drag
- **Solution**: Ensure `UseRenderingSync = true`
- **Cause**: Not synchronized with render cycle

**Issue**: High CPU usage
- **Solution**: Increase `TopmostCheckInterval` to 60+
- **Cause**: Too frequent Win32 API calls

## Example Integration

See: `Ink_Canvas.Helpers.PopupManagerHelper` for full implementation
Usage example: `MainWindow_cs.MW_FloatingBarIcons.cs`

## Dependencies

- `System.Windows.Controls.Primitives` (Popup class)
- `System.Windows.Interop` (HwndSource)
- `System.Runtime.InteropServices` (Win32 P/Invoke)
