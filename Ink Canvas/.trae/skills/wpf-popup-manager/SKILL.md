---
name: "wpf-popup-manager"
description: "Manages WPF Popup z-order using owner-owned window relationship instead of HWND_TOPMOST to prevent z-order fighting. Invoke when Popup steals topmost from toolbar/canvas/whiteboard, or when Popup and MainWindow compete for z-order."
---

# WPF Popup Manager

This skill provides the correct approach to managing WPF Popup z-order, based on real-world debugging of **z-order fighting** between Popup and MainWindow HWNDs.

## Core Principle: Never Use HWND_TOPMOST for Popup

### The Problem

When both MainWindow and Popup independently call `SetWindowPos(HWND_TOPMOST)`, they **fight for the same TOPMOST layer**:

```
MainWindow (HWND_TOPMOST, 500ms maintenance timer)
    ↕ z-order fighting → flickering, focus stealing
Popup (HWND_TOPMOST, ~160ms maintenance via PopupManagerHelper)
```

Symptoms:
- Popup covers floating toolbar, whiteboard, ink canvas
- Popup and MainWindow alternate being on top
- Other UI elements (floating bar, canvas) get pushed behind Popup

### The Solution: Owner-Owned Window Relationship

WPF Popup is automatically an **owned window** of MainWindow. Windows OS guarantees:
- Owned windows **always appear above their owner**
- When owner is TOPMOST → owned windows are automatically in TOPMOST layer
- No need for Popup to independently set `HWND_TOPMOST`

**Therefore**: Popup should use `HWND_TOP` (top of owner's z-group) instead of `HWND_TOPMOST`.

## When to Use This Skill

- Popup steals z-order from floating toolbar / canvas / whiteboard
- Popup and MainWindow compete for topmost position
- Converting Border/Panel menus to Popup controls
- Popup needs to follow a draggable toolbar/container
- User reports "菜单抢置顶" (menu steals topmost)

## Implementation

### PopupManagerHelper.cs

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Helpers
{
    public class PopupManagerHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);   // AVOID for Popup
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2); // Use when not topmost
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;          // Use when owner is topmost
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;  // CRITICAL: don't change owner's z-order
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;

        #endregion

        #region Configuration

        public class Config
        {
            public int TopmostCheckInterval { get; set; } = 10;
            public bool UseRenderingSync { get; set; } = true;
            public int InitialTopmostAttempts { get; set; } = 3;
        }

        #endregion

        #region State

        private readonly List<Popup> _registeredPopups = new List<Popup>();
        private readonly Config _config;
        private bool _isInitialized = false;
        private bool _needsUpdate = false;
        private int _topmostCounter = 0;
        private bool _offsetToggle = true;

        #endregion

        #region Constructor

        public PopupManagerHelper() : this(new Config()) { }
        public PopupManagerHelper(Config config)
        {
            _config = config ?? new Config();
        }

        #endregion

        #region Conditional Topmost Callback

        /// <summary>
        /// When null (default): always topmost (backward compatible).
        /// When set: only topmost when callback returns true.
        /// Bind to Settings.Advanced.IsAlwaysOnTop for MainWindow integration.
        /// </summary>
        public Func<bool> ShouldBeTopmost { get; set; }

        private bool CheckShouldBeTopmost()
        {
            return ShouldBeTopmost == null || ShouldBeTopmost();
        }

        #endregion

        #region Initialize & Register

        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                if (_config.UseRenderingSync)
                {
                    CompositionTarget.Rendering += OnRendering;
                }
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] Initialize error: {ex.Message}");
            }
        }

        public void RegisterPopup(Popup popup)
        {
            if (popup == null || _registeredPopups.Contains(popup)) return;

            _registeredPopups.Add(popup);
            popup.Opened += OnPopupOpened;

            if (popup.IsOpen)
            {
                BringToFront(popup);
            }
        }

        public void UnregisterPopup(Popup popup)
        {
            if (popup == null) return;

            popup.Opened -= OnPopupOpened;
            _registeredPopups.Remove(popup);
        }

        private void OnPopupOpened(object sender, EventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null) return;

            // Apply z-order state after Popup HWND is fully created
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTopmostState(popup);
            }), DispatcherPriority.Loaded);
        }

        #endregion

        #region Public API

        public void MarkNeedsUpdate()
        {
            _needsUpdate = true;
        }

        public void BringToFront(Popup popup)
        {
            if (popup?.Child == null) return;

            Action bringToTopAction = () =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                    if (source?.Handle == null) return;

                    ApplyTopmostStateToHwnd(source.Handle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFront failed: {ex.Message}");
                }
            };

            for (int i = 0; i < _config.InitialTopmostAttempts; i++)
            {
                DispatcherPriority priority = i switch
                {
                    0 => DispatcherPriority.Render,
                    1 => DispatcherPriority.Normal,
                    _ => DispatcherPriority.Background
                };
                Application.Current.Dispatcher.BeginInvoke(bringToTopAction, priority);
            }
        }

        public void BringToFrontLight(Popup popup)
        {
            if (popup?.Child == null) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                    if (source?.Handle == null) return;

                    ApplyTopmostStateToHwnd(source.Handle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFrontLight failed: {ex.Message}");
                }
            }), DispatcherPriority.Render);
        }

        public void UpdatePosition(Popup popup)
        {
            if (popup == null || !popup.IsOpen || popup.PlacementTarget == null) return;

            try
            {
                var hOffset = popup.HorizontalOffset;
                var vOffset = popup.VerticalOffset;

                if (_offsetToggle)
                {
                    popup.HorizontalOffset = hOffset + 0.001;
                    popup.VerticalOffset = vOffset + 0.001;
                }
                else
                {
                    popup.HorizontalOffset = hOffset - 0.001;
                    popup.VerticalOffset = vOffset - 0.001;
                }

                _offsetToggle = !_offsetToggle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] UpdatePosition error: {ex.Message}");
            }
        }

        #endregion

        #region Rendering Callback

        private void OnRendering(object sender, EventArgs e)
        {
            try
            {
                if (_needsUpdate)
                {
                    UpdateAllPositions();
                    BringAllToFrontSync();
                    _needsUpdate = false;
                    return;
                }

                MaintainTopmostForAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] OnRendering error: {ex.Message}");
            }
        }

        private void UpdateAllPositions()
        {
            foreach (var popup in _registeredPopups)
            {
                UpdatePosition(popup);
            }
        }

        private void BringAllToFrontSync()
        {
            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen && popup.PlacementTarget != null)
                {
                    ApplyTopmostState(popup);
                }
            }
        }

        private void MaintainTopmostForAll()
        {
            _topmostCounter++;
            if (_topmostCounter < _config.TopmostCheckInterval) return;
            _topmostCounter = 0;

            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen && popup.PlacementTarget != null)
                {
                    ApplyTopmostState(popup);
                }
            }
        }

        #endregion

        #region Win32 Z-Order Operations (CORE LOGIC)

        private void ApplyTopmostState(Popup popup)
        {
            if (popup?.Child == null) return;

            try
            {
                var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                if (source?.Handle == null) return;

                ApplyTopmostStateToHwnd(source.Handle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] ApplyTopmostState failed: {ex.Message}");
            }
        }

        /// <summary>
        /// CORE: Apply correct z-order state to Popup HWND.
        /// 
        /// When owner (MainWindow) is TOPMOST:
        ///   - Popup is automatically in TOPMOST layer as an owned window
        ///   - Use HWND_TOP + SWP_NOOWNERZORDER to stay above owner without fighting
        ///   - DO NOT use HWND_TOPMOST (causes z-order fighting)
        /// 
        /// When owner is NOT TOPMOST:
        ///   - Remove WPF's automatic WS_EX_TOPMOST (set when AllowsTransparency=True)
        ///   - Use HWND_NOTOPMOST to ensure Popup doesn't cover other apps
        /// </summary>
        private void ApplyTopmostStateToHwnd(IntPtr hwnd)
        {
            var shouldBeTopmost = CheckShouldBeTopmost();

            try
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                if (shouldBeTopmost)
                {
                    // Owner is TOPMOST → Popup is automatically TOPMOST as owned window
                    // Only ensure Popup is at top of owner's z-group, NOT independently TOPMOST
                    if ((exStyle & WS_EX_TOPMOST) == 0)
                    {
                        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                    }
                }
                else
                {
                    // Owner is NOT TOPMOST → Remove all TOPMOST from Popup
                    // Step 1: Clear WS_EX_TOPMOST style bit (WPF auto-sets this)
                    if ((exStyle & WS_EX_TOPMOST) != 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
                    }

                    // Step 2: Move Popup out of TOPMOST z-group
                    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] ApplyTopmostStateToHwnd failed: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            if (!_isInitialized) return;

            try
            {
                CompositionTarget.Rendering -= OnRendering;
                foreach (var popup in _registeredPopups)
                {
                    popup.Opened -= OnPopupOpened;
                }
                _registeredPopups.Clear();
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] Cleanup error: {ex.Message}");
            }
        }

        #endregion
    }
}
```

### Integration in MainWindow

```csharp
// In MW_FloatingBarIcons.cs - InitializePopupManager()
internal void InitializePopupManager()
{
    try
    {
        _popupManager = new PopupManagerHelper();

        // CRITICAL: Bind Popup topmost to MainWindow's topmost setting
        _popupManager.ShouldBeTopmost = () => Settings.Advanced.IsAlwaysOnTop;

        _popupManager.RegisterPopup(BorderTools);
        _popupManager.RegisterPopup(BoardBorderToolsPopup);

        _popupManager.Initialize();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[PopupManager] Initialize error: {ex.Message}");
    }
}
```

## Win32 Constants Reference

| Constant | Value | Meaning | When to Use |
|----------|-------|---------|-------------|
| `HWND_TOPMOST` | `-1` | Top of all windows, even across processes | **AVOID for Popup** — causes z-order fighting with MainWindow |
| `HWND_NOTOPMOST` | `-2` | Top of non-topmost windows | When owner window is NOT topmost |
| `HWND_TOP` | `0` | Top of z-group (same owner level) | When owner IS topmost — Popup stays above owner without fighting |
| `SWP_NOOWNERZORDER` | `0x0200` | Don't change owner's z-order | **Always use with HWND_TOP** for Popup |
| `WS_EX_TOPMOST` | `0x00000008` | Window extended style bit | WPF auto-sets on Popup when `AllowsTransparency=True`; must clear when not topmost |
| `GWL_EXSTYLE` | `-20` | Get/set extended window styles | Used with `GetWindowLong`/`SetWindowLong` to modify `WS_EX_TOPMOST` |

## Z-Order Decision Tree

```
Is MainWindow TOPMOST?
├── YES (IsAlwaysOnTop = true)
│   ├── Popup has WS_EX_TOPMOST? → Leave it (WPF set it, it's correct)
│   └── Popup doesn't have WS_EX_TOPMOST? → SetWindowPos(HWND_TOP + SWP_NOOWNERZORDER)
│       └── Popup rises above MainWindow via owner-owned relationship
│       └── No HWND_TOPMOST needed → No z-order fighting
│
└── NO (IsAlwaysOnTop = false)
    ├── Popup has WS_EX_TOPMOST? → SetWindowLong(clear WS_EX_TOPMOST)
    │   └── WPF auto-set this because AllowsTransparency=True
    └── SetWindowPos(HWND_NOTOPMOST)
        └── Popup moves to normal z-group
        └── Won't cover other applications
```

## Why WPF Popup Is TOPMOST by Default

When `Popup.AllowsTransparency="True"` (required for rounded corners, shadows, etc.), WPF creates the Popup's HWND with `WS_EX_TOPMOST` style. This is **by design** in WPF framework — transparent Popups need their own HWND and WPF makes them TOPMOST to ensure they appear above the main window.

This is fine when MainWindow is also TOPMOST. But when MainWindow is NOT TOPMOST, the Popup's `WS_EX_TOPMOST` causes it to float above everything — including other apps. That's why we must actively clear this style bit when `ShouldBeTopmost` returns false.

## Drag-Follow System

Uses **offset micro-adjustment** technique:

- Alternates between `+0.001` and `-0.001` pixel offsets on `HorizontalOffset`/`VerticalOffset`
- Triggers WPF's placement recalculation without recreating window
- Preserves HWND stability (no flicker)
- Synchronized with monitor refresh rate via `CompositionTarget.Rendering`

## Common Pitfalls & Solutions

| Pitfall | Symptom | Root Cause | Solution |
|---------|---------|------------|----------|
| Popup steals z-order from toolbar/canvas | Popup covers floating bar, whiteboard | Popup uses `HWND_TOPMOST` independently | Use `HWND_TOP` + `SWP_NOOWNERZORDER` instead |
| Popup still TOPMOST when main window isn't | Popup covers other apps | WPF auto-sets `WS_EX_TOPMOST` on transparent Popup | Clear via `SetWindowLong` + `SetWindowPos(HWND_NOTOPMOST)` |
| Z-order flickering between Popup and MainWindow | Alternating which is on top | Both HWNDs compete for `HWND_TOPMOST` | Only MainWindow should use `HWND_TOPMOST`; Popup uses owner-owned relationship |
| Popup hidden behind MainWindow | Not visible when opened | `WS_EX_TOPMOST` was cleared but not restored | Check `ShouldBeTopmost` callback returns correct value |
| Choppy movement during drag | Stuttering Popup position | Not using `CompositionTarget.Rendering` sync | Set `UseRenderingSync = true` in config |
| High CPU usage | Excessive Win32 API calls | Too frequent `SetWindowPos` calls | Increase `TopmostCheckInterval` to 30+ |

## File Locations

| File | Purpose |
|------|---------|
| `Helpers/PopupManagerHelper.cs` | Core implementation |
| `MainWindow_cs/MW_FloatingBarIcons.cs` | Integration: `InitializePopupManager()` with `ShouldBeTopmost` binding |
| `Windows/SettingsViews/Helpers/WindowSettingsHelper.cs` | MainWindow topmost management (separate from Popup) |

## Dependencies

- `System.Windows.Controls.Primitives` (Popup class)
- `System.Windows.Interop` (HwndSource)
- `System.Runtime.InteropServices` (Win32 P/Invoke)
