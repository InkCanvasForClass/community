---
name: "wpf-extract-shared-popup"
description: "Extracts duplicated WPF Popup/Panel XAML into a reusable UserControl, or converts Border/Panel menus to Popup with proper z-order and animation. Invoke when deduplicating Popup/panel structures, converting negative-Margin panels to Popup, or when two+ places share nearly identical XAML UI blocks."
---

# WPF Extract Shared Popup

This skill guides you through two main patterns:
1. **Extracting duplicated XAML Popup/Panel content** into a reusable `UserControl`
2. **Converting Border/Panel menus to Popup** (replacing negative-Margin positioning hacks)

Both patterns maintain full backward compatibility with existing code-behind references and proper z-order management.

## When to Use

- Two or more `<Popup>` elements in `MainWindow.xaml` share nearly identical inner content
- A panel/control block is copy-pasted across multiple locations with minor variations
- You want to unify visual style of duplicate panels (e.g., old-style vs new-style popup)
- Need to reduce XAML duplication without breaking existing C# code that references named elements
- A `Border` or `Panel` is positioned via `Grid Width="0"` + negative `Margin` hack — should be converted to a proper `Popup`
- Popup z-order conflicts with main window or other UI elements

---

## Pattern A: Border/Panel → Popup Conversion

Use this pattern when a menu panel is implemented as a `Border` inside a `Grid Width="0"` with negative `Margin` positioning. This hack causes z-order issues (panel hidden behind other elements) and should be replaced with a proper WPF `Popup`.

### A1. Identify the Target Structure

Look for this pattern in `MainWindow.xaml`:

```xml
<Grid Width="0">
    <Border x:Name="SomePanel" Background="{DynamicResource FloatBarBackground}"
            Margin="-170,-140,-147,37" ...>
        <!-- menu content -->
    </Border>
</Grid>
```

Key indicators:
- `Grid Width="0"` container (zero-width to avoid layout impact)
- Negative `Margin` on the `Border` (positions it visually outside the grid)
- `x:Name` attribute used in code-behind

### A2. Replace with Popup in XAML

Remove the `Grid Width="0" > Border` and add a `Popup` alongside existing Popups (e.g., next to `BorderTools`):

```xml
<Popup x:Name="SomePanel"
       Placement="Custom"
       AllowsTransparency="True"
       StaysOpen="True"
       IsOpen="False">
    <Border CornerRadius="8" Background="{DynamicResource ToolsPopupBackground}"
            BorderBrush="#3b82f6" BorderThickness="2">
        <!-- menu content, same as before but WITHOUT negative Margin -->
    </Border>
</Popup>
```

**Critical XAML changes:**
- Remove `Margin` from the inner `Border` (Popup handles positioning)
- Remove `Visibility="Visible"` / `Opacity="1"` (Popup uses `IsOpen` instead)
- Match the visual style with existing Popup UserControls (e.g., `ToolsPopupContent`)

### A3. Use PopupTitleBar and Standard Popup Shell

When converting a menu to Popup, use the shared `PopupTitleBar` UserControl and the standard popup shell pattern. All popup UserControls live in `Controls/Popups/` (main project) or `InkCanvas.Controls/Popups/` (shared library).

**PopupTitleBar** (`InkCanvas.Controls/Popups/PopupTitleBar.xaml`) is a reusable title bar with:
- `Title` DP — bind to i18n key, e.g. `Title="{i18n:I18n Key=Board_Shape}"`
- `CloseFontIcon` property — exposes the close icon for event wiring in code-behind

**Standard Popup Shell** pattern:

```xml
<!-- In the UserControl XAML (e.g., Controls/Popups/ShapeDrawPopupContent.xaml) -->
<Border CornerRadius="8" Background="{DynamicResource ToolsPopupBackground}"
        BorderBrush="#3b82f6" BorderThickness="2">
    <ikw:SimpleStackPanel Margin="-1">
        <controls:PopupTitleBar x:Name="TitleBar" Title="{i18n:I18n Key=Board_Shape}" />
        <Border Margin="6,0,6,6" BorderBrush="{DynamicResource ToolsPopupInnerBorderBrush}"
                Background="{DynamicResource ToolsPopupInnerBackground}" BorderThickness="1"
                CornerRadius="4">
            <ikw:SimpleStackPanel Margin="2" Spacing="1">
                <!-- content buttons -->
            </ikw:SimpleStackPanel>
        </Border>
    </ikw:SimpleStackPanel>
</Border>
```

**In MainWindow.xaml**, the Popup simply contains the UserControl:

```xml
<Popup x:Name="BorderDrawShape" Placement="Custom" AllowsTransparency="True"
       StaysOpen="True" IsOpen="False">
    <localControls:ShapeDrawPopupContent x:Name="ShapeDrawPopupContent" />
</Popup>
```

**Key rules:**
- NO inline event bindings in the UserControl XAML — wire events in code-behind via `WireUp*Events()` methods
- Use `controls:PopupTitleBar` instead of hand-writing the title Grid every time
- Access close icon via `content.CloseFontIcon` for event wiring

### A4. Set PlacementTarget in Code-Behind

If the trigger button is dynamically created (via `ToolbarRegistry`), set `PlacementTarget` in the `Attach*` method:

```csharp
internal void AttachSomeBtn(ToolbarImageButton btn)
{
    SomeFloatingBarBtn = btn;
    SomePanel.PlacementTarget = btn;
}
```

If the button is defined in XAML, set it in XAML:
```xml
<Popup x:Name="SomePanel" PlacementTarget="{Binding ElementName=SomeButton}" ...>
```

### A5. Add CustomPopupPlacementCallback

In `MainWindow.xaml.cs` constructor, add a placement callback (same pattern as `BorderTools`):

```csharp
SomePanel.CustomPopupPlacementCallback =
    (popupSize, targetSize, offset) => new[]
    {
        new CustomPopupPlacement(
            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
            PopupPrimaryAxis.Vertical)
    };
```

This centers the Popup horizontally above the trigger button.

### A6. Update Code-Behind References

Search ALL `.cs` files for references to the panel name. Update each reference:

| Old Pattern | New Pattern | Where |
|-------------|-------------|-------|
| `SomePanel.Visibility == Visibility.Visible` | `SomePanel.IsOpen` | Toggle check |
| `SomePanel.Visibility = Visibility.Collapsed` | `SomePanel.IsOpen = false` | Immediate hide |
| `AnimationsHelper.ShowWithSlideFromBottomAndFade(SomePanel)` | `AnimationsHelper.ShowPopupWithSlideAndFade(SomePanel)` | Animated show |
| `AnimationsHelper.HideWithSlideAndFade(SomePanel)` | `AnimationsHelper.HidePopupWithSlideAndFade(SomePanel)` | Animated hide |
| `UpdateSubPanelPosition(btn, SomePanel, width)` | *(remove — Popup positions itself)* | Position update |

### A6.5. Use currentMode to Show Only the Correct Popup

**CRITICAL**: When a menu has both a desktop version (`BorderDrawShape`) and a board version (`BoardBorderDrawShape`), never show both simultaneously. Follow the pattern used by `SymbolIconTools_MouseUp`:

```csharp
internal void ImageDrawShape_MouseUp(object sender, MouseButtonEventArgs e)
{
    // Check EITHER popup (not just one)
    if (BorderDrawShape.IsOpen || BoardBorderDrawShape.IsOpen)
    {
        // Close both (hide whichever is open)
        AnimationsHelper.HidePopupWithSlideAndFade(BorderDrawShape);
        AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderDrawShape);
    }
    else
    {
        HideSubPanels();
        // Show only ONE based on currentMode
        if (currentMode == 0)
        {
            AnimationsHelper.ShowPopupWithSlideAndFade(BorderDrawShape);
            _popupManager?.BringToFront(BorderDrawShape);
        }
        else
        {
            AnimationsHelper.ShowPopupWithSlideAndFade(BoardBorderDrawShape);
            _popupManager?.BringToFront(BoardBorderDrawShape);
        }
    }
}
```

Key rules:
- **Toggle check**: Use `||` to check both popups: `if (BorderDrawShape.IsOpen || BoardBorderDrawShape.IsOpen)`
- **Show**: Only show the one matching `currentMode` (0 = desktop, other = board)
- **Hide (close)**: Always close both (safe, unknown which is open)
- **Follow existing patterns**: Reference `SymbolIconTools_MouseUp` for the canonical implementation

### A7. Register with PopupManagerHelper and BringToFront

**CRITICAL**: Without these two steps, the Popup will NOT appear on top of other UI elements.

1. Register in `InitializePopupManager()`:
```csharp
_popupManager.RegisterPopup(SomePanel);
```

2. Call `BringToFront` after showing:
```csharp
AnimationsHelper.ShowPopupWithSlideAndFade(SomePanel);
_popupManager?.BringToFront(SomePanel);
```

**If you forget `BringToFront`**, the Popup will appear behind the floating bar and other UI elements.

### A8. ToolbarRegistry Compatibility

`ToolbarRegistry.ApplyMenuVisibility` already handles both `Popup` and `FrameworkElement`:

```csharp
var menuElement = host.Window.FindName(item.MenuPanelName);
if (menuElement is Popup popup)
    popup.IsOpen = visible;
else if (menuElement is FrameworkElement fe)
    fe.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
```

So `MenuPanelName => "SomePanel"` works for both Border and Popup — **no change needed** in `ShapeDrawToolItem.cs` or similar files.

### A9. Remove UpdateSubPanelPosition Calls

The `UpdateSubPanelPosition` method uses `TransformToAncestor` + `Margin` manipulation to position panels. This is unnecessary for Popups — `CustomPopupPlacementCallback` handles positioning automatically.

Remove the method body (keep the empty method to avoid breaking call sites):
```csharp
private void UpdateSomePanelPosition()
{
}
```

---

## Pattern B: Extract Duplicated Popup Content into UserControl

Use this pattern when two or more `<Popup>` elements share nearly identical inner content.

### 1. Analyze Both Copies

Read both locations in `MainWindow.xaml`. Document differences:

| Aspect | Copy A (old) | Copy B (new/target) |
|--------|-------------|---------------------|
| Outer Border style | ... | ... |
| Title bar | ... | ... |
| Button naming prefix | e.g., `BoardXxxBtn` | e.g., `XxxBtn` |
| Event bindings | inline or absent | inline or absent |
| Visibility differences | Collapsed items? | All visible? |

**Key decision**: Choose the **newer/better-styled** version as the template for the UserControl.

### Merging Two Different Menus with IsBoardMode

When two menus have the **same base buttons** but one has **extra buttons** (e.g., board mode has coordinate axes, hyperbola, parabola), use a single UserControl with `IsBoardMode` DP:

1. **Add all buttons to the UserControl** — base buttons always visible, extra buttons in rows with `x:Name` and `Visibility="Collapsed"`
2. **Toggle extra rows via `IsBoardMode` DP** — in the `OnIsBoardModeChanged` callback, set `Visibility` on the extra rows
3. **Use `GeometryButton` for all buttons** — even if the original used `<Image>` controls. `GeometryButton.IconSource` accepts `ImageSource`, which is the base class of both `BitmapImage` (GeoIcon*) and `DrawingImage` (DrawShapeImageSource*)
4. **Create two instances** — `<localControls:ShapeDrawPopupContent IsBoardMode="True" />` for board, `<localControls:ShapeDrawPopupContent />` for floating bar
5. **Wire events separately** — each instance gets its own `WireUp*Events()` method with anti-duplicate guard
6. **Property mappings** — create both `BoardImageDrawLine` (→ floating bar instance) and `ImageDrawLine` (→ board instance) accessors in `MW_Toolbar.cs`

**Key insight**: `DrawShapeImageSource.*` resources are `DrawingImage` objects that work directly as `GeometryButton.IconSource` — no need to create new PNG icons.

### 2. Search Code-Behind References

Before creating the UserControl, search ALL `.cs` files for every named element (`x:Name`) inside the duplicate regions:

```
Search for: BoardTimerToolBtn, TimerToolBtn, BoardSaveToolBtn, SaveToolBtn, etc.
```

Document:
- Which files reference each name
- What operations are performed (event subscription, property access, visibility toggle)
- Whether names differ by prefix only (e.g., `Board*` vs non-prefixed)

### 3. Create the UserControl

Create two files in the project's Controls directory:

#### XAML File (`Controls/<Name>.xaml`)

```xml
<UserControl x:Class="Ink_Canvas.Controls.<Name>"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             <!-- Add ALL namespaces used in original XAML -->
             xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
             xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
             xmlns:controls="clr-namespace:Ink_Canvas.Controls;assembly=InkCanvas.Controls"
             xmlns:i18n="clr-namespace:Ink_Canvas.MarkupExtensions"
             xmlns:helpers="clr-namespace:Ink_Canvas.Helpers"
             mc:Ignorable="d">
    <UserControl.Resources>
        <!-- CRITICAL: Inline any Style referenced via BasedOn here!
             Style.BasedOn does NOT support DynamicResource.
             If the original uses BasedOn="{StaticResource SomeStyle}"
             and SomeStyle is defined in MainWindow.xaml's local resources,
             you MUST copy the full Style definition here. -->
        <Style x:Key="SomeStyle" TargetType="Label">
            <!-- copy all Setters from original -->
        </Style>
    </UserControl.Resources>

    <!-- Use the NEWER version's visual structure -->
    <Border CornerRadius="8" Background="{DynamicResource SomeResource}" ...>
        ...
    </Border>
</UserControl>
```

**CRITICAL RULES for XAML:**

1. **NO inline event bindings** — Do NOT write `ButtonMouseUp="SomeHandler"` in XAML. Events will be wired in code-behind.
2. **NO `{StaticResource}` for cross-file resources** — Use `{DynamicResource}` for theme brushes/colors defined in external ResourceDictionaries (Light.xaml/Dark.xaml). Only use `{StaticResource}` for styles defined within this same file's `<UserControl.Resources>`.
3. **`Style.BasedOn` limitation** — WPF does NOT allow `DynamicResource` on `BasedOn`. Always define the base style locally or remove the `BasedOn` chain.
4. **Keep all `x:Name` attributes** — Every control that was referenced in code-behind must retain its name.

#### Code-Behind File (`Controls/<Name>.xaml.cs`)

```csharp
using System.Windows;
using System.Windows.Controls;
// Add using for any non-standard types (e.g., FontIcon)

namespace Ink_Canvas.Controls
{
    public partial class <Name> : UserControl
    {
        // --- Mode toggling DP (if copies have behavioral differences) ---
        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(<Name>),
            new PropertyMetadata(false, OnIsBoardModeChanged));

        private static void OnIsBoardModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (<Name>)d;
            if ((bool)e.NewValue)
            {
                // Apply differences for this mode (e.g., hide certain buttons)
                control.SomeButton.Visibility = Visibility.Collapsed;
            }
        }

        public bool IsBoardMode
        {
            get => (bool)GetValue(IsBoardModeProperty);
            set => SetValue(IsBoardModeProperty, value);
        }

        // --- Property accessors for backward compatibility ---
        // These allow external code to access inner controls by their original names
        public ToolMenuButton TimerBtn => TimerToolBtn;
        public ToolMenuButton SaveBtn => SaveToolBtn;
        // ... one per named control

        public FontIcon CloseFontIcon => CloseIcon;

        public <Name>()
        {
            InitializeComponent();
        }
    }
}
```

### 4. Replace Original XAML in MainWindow.xaml

Replace BOTH duplicate Popup contents with the new UserControl:

```xml
<!-- Copy A (e.g., board mode) -->
<Popup x:Name="BoardBorderToolsPopup" ...>
    <localControls:<Name> x:Name="BoardToolsPopupContent" IsBoardMode="True" />
</Popup>

<!-- Copy B (e.g., normal mode) -->
<Popup x:Name="BorderTools" ...>
    <localControls:<Name> x:Name="MainToolsPopupContent" />
</Popup>
```

### 5. Add Property Mapping in MainWindow.xaml.cs

Add computed properties that delegate to the UserControl instances, so existing code doesn't need to change:

```csharp
// Place near other field declarations (after Cursor_Icon / Pen_Icon pattern)

// Board-prefixed: delegate to BoardToolsPopupContent
internal ToolMenuButton BoardTimerToolBtn => BoardToolsPopupContent?.TimerBtn;
internal ToolMenuButton BoardSaveToolBtn => BoardToolsPopupContent?.SaveBtn;
// ... all 9 buttons

// Non-board: delegate to MainToolsPopupContent
internal ToolMenuButton TimerToolBtn => MainToolsPopupContent?.TimerBtn;
internal ToolMenuButton SaveToolBtn => MainToolsPopupContent?.SaveBtn;
// ... all 9 buttons
```

### 6. Wire Up Events Programmatically (Anti-Duplicate Pattern)

Add a method in MainWindow's constructor region and call it after `InitializeComponent()`. **Use the anti-duplicate pattern** to prevent memory leaks from repeated binding:

```csharp
private bool _toolsPopupEventsWired;

private void WireUp<Name>Events()
{
    if (_toolsPopupEventsWired) return;
    _toolsPopupEventsWired = true;

    WireUpSingle<Name>(BoardToolsPopupContent);
    WireUpSingle<Name>(MainToolsPopupContent);
}

private void WireUpSingle<Name>(<Name> content)
{
    if (content == null) return;

    content.TimerBtn.ButtonMouseUp += ImageCountdownTimer_MouseUp;
    content.SaveBtn.ButtonMouseDown += Border_MouseDown;
    content.SaveBtn.ButtonMouseUp += SymbolIconSaveStrokes_MouseUp;
    content.OpenBtn.ButtonMouseDown += Border_MouseDown;
    content.OpenBtn.ButtonMouseUp += SymbolIconOpenStrokes_MouseUp;
    content.ReplayBtn.ButtonMouseUp += GridInkReplayButton_MouseUp;
    content.ScreenshotBtn.ButtonMouseUp += SymbolIconScreenshot_MouseUp;
    content.ManualBtn.ButtonMouseUp += OperatingGuideWindowIcon_MouseUp;
    content.SettingsBtn.ButtonMouseUp += SymbolIconSettings_Click;
    content.CloseFontIcon.MouseDown += Border_MouseDown;
    content.CloseFontIcon.MouseUp += CloseBordertools_MouseUp;
}
```

Call it in constructor:
```csharp
public MainWindow()
{
    InitializeComponent();
    WireUp<Name>Events();  // <-- add this line
    // ... rest of constructor
}
```

**Why the anti-duplicate pattern matters:**
- Without `_toolsPopupEventsWired` guard, if the method is called again, each event handler gets registered **again** → every click fires the handler twice → memory leak
- Extracting `WireUpSingle<Name>()` eliminates copy-paste duplication for the two instances

### 7. Manage Popup Z-Order (Critical for Topmost Windows)

When the main window uses `Topmost="True"` and `PopupManagerHelper` manages Popup z-order, **Popup must NOT use `HWND_TOPMOST` independently**. This causes z-order fighting between the main window and Popup HWNDs.

#### The Problem

```
MainWindow (HWND_TOPMOST, 500ms maintenance)
    ↕ z-order fighting
Popup (HWND_TOPMOST, ~160ms maintenance via PopupManagerHelper)
```

Both HWNDs independently compete for the TOPMOST layer → visual flickering, Popup stealing focus from toolbar/canvas.

#### The Solution: Owner-Owned Window Relationship

WPF Popup is automatically an **owned window** of MainWindow. Windows OS guarantees owned windows always appear above their owner. Therefore:

- **When MainWindow is TOPMOST** → Popup (as owned window) is automatically in the TOPMOST layer too. No need for `SetWindowPos(HWND_TOPMOST)`.
- **When MainWindow is NOT TOPMOST** → Popup should also NOT be TOPMOST. Use `HWND_NOTOPMOST` + remove `WS_EX_TOPMOST` style bit.

#### PopupManagerHelper Implementation

```csharp
public Func<bool> ShouldBeTopmost { get; set; }  // Bind to Settings.Advanced.IsAlwaysOnTop

private void ApplyTopmostStateToHwnd(IntPtr hwnd)
{
    var shouldBeTopmost = CheckShouldBeTopmost();

    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

    if (shouldBeTopmost)
    {
        // CRITICAL: WPF auto-sets WS_EX_TOPMOST when AllowsTransparency=True.
        // This causes z-order fighting with MainWindow's own TOPMOST.
        // Must clear it and use HWND_TOP + SWP_NOOWNERZORDER instead.
        if ((exStyle & WS_EX_TOPMOST) != 0)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
        }

        // Always call SetWindowPos(HWND_TOP) — places Popup at top of owner's z-group
        // without independent TOPMOST that fights with MainWindow
        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }
    else
    {
        // Remove WPF's automatic WS_EX_TOPMOST (set when AllowsTransparency=True)
        if ((exStyle & WS_EX_TOPMOST) != 0)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
        }
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
```

**CRITICAL BUG FIX**: The original implementation had `if ((exStyle & WS_EX_TOPMOST) == 0)` which meant when WPF auto-set `WS_EX_TOPMOST` (which happens when `AllowsTransparency=True`), `SetWindowPos(HWND_TOP)` was **never called**. The popup kept `WS_EX_TOPMOST` and fought with the main window for z-order. The fix is to **always** clear `WS_EX_TOPMOST` and **always** call `SetWindowPos(HWND_TOP)` when `shouldBeTopmost` is true.

#### Initialize with Condition Callback

```csharp
_popupManager = new PopupManagerHelper();
_popupManager.ShouldBeTopmost = () => Settings.Advanced.IsAlwaysOnTop;
_popupManager.RegisterPopup(BorderTools);
_popupManager.RegisterPopup(BoardBorderToolsPopup);
_popupManager.RegisterPopup(BorderDrawShape);  // Don't forget new Popups!
_popupManager.Initialize();
```

#### Key Win32 Constants

| Constant | Value | Usage |
|----------|-------|-------|
| `HWND_TOPMOST` | `-1` | **Avoid** for Popup — causes z-order fighting with main window |
| `HWND_NOTOPMOST` | `-2` | Use when main window is not topmost |
| `HWND_TOP` | `0` | Use when main window IS topmost — places Popup at top of owner's z-group |
| `SWP_NOOWNERZORDER` | `0x0200` | **Critical flag** — prevents changing owner's z-order when moving Popup |
| `WS_EX_TOPMOST` | `0x00000008` | WPF auto-sets this on Popup when `AllowsTransparency=True`; must clear when not topmost |

## Common Pitfalls & Solutions

| Pitfall | Symptom | Solution |
|---------|---------|----------|
| `StaticResource` not found at parse time | `XamlParseException: 无法找到名为"xxx"的资源` | Move resource definition into `<UserControl.Resources>` or change to `DynamicResource` |
| `DynamicResource` on `Style.BasedOn` | `不能在"Style"类型的"BasedOn"属性上设置"DynamicResourceExtension"` | Define the Style locally in UserControl.Resources; `BasedOn` only accepts `StaticResource` |
| Inline event handler in XAML | `CS1061: 未包含"xxx"方法的定义` | Remove inline `EventName="Handler"` from XAML; wire events in code-behind instead |
| exe locked during build | `MSB3027: 文件被另一个进程锁定` | Close running app instance before rebuilding |
| Null reference on property mapping | `NullReferenceException` when accessing `BoardTimerToolBtn.Icon.Geometry` | Use null-conditional `?.` operator in property mappings |
| Event duplicate binding | Handler fires multiple times per click | Use `_wired` flag guard + extract single-instance wiring method |
| Popup steals z-order from toolbar/canvas | Popup covers floating bar, whiteboard, ink canvas | Don't use `HWND_TOPMOST` for Popup; use `HWND_TOP` + `SWP_NOOWNERZORDER` instead |
| Popup still TOPMOST when main window isn't | Popup covers other apps even with topmost off | Clear `WS_EX_TOPMOST` style bit via `SetWindowLong` + `SetWindowPos(HWND_NOTOPMOST)` |
| Git merge conflicts after branch switch | `<<<<<<< HEAD` / `=======` / `>>>>>>> branch` markers in files | Resolve manually: keep correct version per conflict (local resources in UserControl, no extra wrappers in MainWindow) |
| **Popup not appearing on top** | Popup shows behind floating bar or other UI | Forgot to call `_popupManager?.BringToFront(popup)` after `ShowPopupWithSlideAndFade` |
| **Popup position wrong** | Popup appears at wrong location | Ensure `PlacementTarget` is set (in XAML or code-behind `Attach*` method) and `CustomPopupPlacementCallback` is configured |
| **Old panel still visible** | Both old Border and new Popup show | Remove the old `Grid Width="0" > Border` completely from XAML |
| **Both desktop and board Popups show at once** | Clicking button opens both menus | Use `currentMode` to show only the correct one (see A6.5); reference `SymbolIconTools_MouseUp` for the pattern |
| **i18n resources missing** | Popup shows resource key names instead of text | Add missing keys to `Strings.resx` and `Strings.en-US.resx` |
| **Popup briefly visible on startup** | All menus flash briefly when app opens | Set `Visibility="Collapsed"` on the panel/Popup child in XAML; add `d:Visibility="Visible"` for design-time preview |

## File Checklist

After conversion/extraction, verify these files exist and compile:

- [ ] `InkCanvas.Controls/Popups/PopupTitleBar.xaml` — Shared title bar UserControl
- [ ] `InkCanvas.Controls/Popups/PopupTitleBar.xaml.cs` — Title DP + CloseFontIcon accessor
- [ ] `Controls/Popups/ToolsPopupContent.xaml` — Uses `<controls:PopupTitleBar>`
- [ ] `Controls/Popups/ShapeDrawPopupContent.xaml` — Uses `<controls:PopupTitleBar>`
- [ ] `MainWindow.xaml` — Old `Grid Width="0">Border` removed, Popup uses `<localControls:*PopupContent>`
- [ ] `MainWindow.xaml.cs` — `CustomPopupPlacementCallback` added, `WireUp*Events()` methods added
- [ ] `MW_FloatingBarIcons.cs` — `HideWithSlideAndFade` → `HidePopupWithSlideAndFade`, `InitializePopupManager` registers new Popup, `BringToFront` called after show
- [ ] `MW_Toolbar.cs` — `Attach*Btn` sets `PlacementTarget`, property mappings for inner controls (e.g., `BoardImageDrawLine => ShapeDrawPopupContent?.DrawLineBtn`)
- [ ] `MW_ShapeDrawing.cs` (or equivalent) — Toggle check uses `IsOpen`, `BringToFront` after show
- [ ] `MW_BoardIcons.cs` (or equivalent) — Uses `HidePopupWithSlideAndFade` for the converted panel
- [ ] `ShapeDrawToolItem.cs` (or equivalent) — `MenuPanelName` unchanged (ToolbarRegistry handles both types)
- [ ] Build passes with 0 errors

## Example Output Structure

```
InkCanvas.Controls/Popups/
├── PopupTitleBar.xaml           ← Shared title bar UserControl (shared library)
├── PopupTitleBar.xaml.cs        ← Title DP + CloseFontIcon accessor
Controls/Popups/
├── ToolsPopupContent.xaml       ← Tools popup UserControl (main project)
├── ToolsPopupContent.xaml.cs    ← IsBoardMode DP + button accessors
├── ShapeDrawPopupContent.xaml   ← Shape draw popup UserControl (main project)
├── ShapeDrawPopupContent.xaml.cs ← Button accessors + CloseFontIcon
Helpers/
├── PopupManagerHelper.cs        ← Modified: HWND_TOP + ShouldBeTopmost callback
└── ... (existing helpers)
MainWindow.xaml                  ← Modified: Popups use <localControls:*PopupContent>
MainWindow.xaml.cs               ← Modified: CustomPopupPlacementCallback + IsOpen + WireUp*Events
MainWindow_cs/
├── MW_FloatingBarIcons.cs       ← Modified: Popup animation APIs + BringToFront + RegisterPopup
├── MW_Toolbar.cs                ← Modified: PlacementTarget in Attach* method + property mappings
├── MW_ShapeDrawing.cs           ← Modified: IsOpen check + Popup animation + BringToFront
├── MW_BoardIcons.cs             ← Modified: HidePopupWithSlideAndFade
└── ... (existing partial classes)
```
