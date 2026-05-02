---
name: "wpf-popup-manager"
description: "Manages WPF Popup z-order (topmost) and drag-follow behavior. Invoke when using Popup controls that need to stay on top of other UI elements or follow a draggable parent container."
---

# WPF Popup Manager

This skill provides a reusable solution for managing WPF Popup controls with two critical features:

## Features

### 1. **Topmost Management**
- Keeps Popup windows above all other UI elements (floating toolbars, canvas, etc.)
- Uses Win32 API `SetWindowPos` with `HWND_TOPMOST` flag
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

## Usage

### Basic Setup

```csharp
// 1. Create manager instance
var popupManager = new PopupManagerHelper();

// 2. Initialize in Window_Loaded
popupManager.Initialize();

// 3. Register Popup(s) you want to manage
popupManager.RegisterPopup(myPopup);
```

### Advanced Configuration

```csharp
// Custom configuration
var config = new PopupManagerConfig
{
    TopmostCheckInterval = 30,        // Frames between topmost checks (~500ms at 60fps)
    UseRenderingSync = true,          // Enable smooth drag-follow
    InitialTopmostAttempts = 3,       // How many times to set topmost on show
};

var popupManager = new PopupManagerHelper(config);
```

### Manual Control

```csharp
// Force immediate topmost
popupManager.BringToFront(popup);

// Update position (call during drag)
popupManager.UpdatePosition(popup);

// Start/stop following mode
popupManager.StartFollowing();
popupManager.StopFollowing();
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
│  │ • Multi-phase │ │   Sync        ││
│  │ • Periodic    │ │ • Offset      ││
│  │   maintenance │ │   Updates     ││
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
| `BringToFront()` | Set TOPMOST via Win32 | ~0.5ms async |
| `UpdatePosition()` | Offset-based reposition | <0.1ms sync |
| `OnRendering()` | Per-frame callback handler | Automatic |

## Implementation Details

### Topmost Strategy

Uses **three-phase approach**:

1. **Initial Show**: 3 rapid attempts (Render + Normal + Background priority)
2. **Post-Animation**: Additional 3 attempts after Storyboard completes
3. **Periodic Maintenance**: Light single attempt every N frames (configurable)

### Drag-Follow Strategy

Uses **offset micro-adjustment** technique:

- Alternates between `+0.001` and `-0.001` pixel offsets
- Triggers WPF's placement recalculation without recreating window
- Preserves HWND stability (no flicker)
- Synchronized with monitor refresh rate

## Best Practices

✅ **DO:**
- Call `Initialize()` once in `Window_Loaded`
- Register all Popups that need management
- Use default config for most cases
- Let the manager handle everything automatically

❌ **DON'T:**
- Manually toggle `IsOpen` during drag (causes flicker)
- Call `SetWindowPos` directly (use helper methods)
- Forget to unregister Popups when done
- Use very short check intervals (<10 frames)

## Troubleshooting

**Issue**: Popup still gets covered
- **Solution**: Increase `InitialTopmostAttempts` to 5
- **Cause**: Other controls aggressively reclaiming z-order

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
