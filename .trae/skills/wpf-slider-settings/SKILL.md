---
name: "wpf-slider-settings"
description: "Provides standard implementation pattern for WPF Slider controls with TextBlock display in Settings pages. Invoke when adding/modifying slider controls in XAML Settings pages, or when slider text is not displaying correctly."
---

# WPF Slider Settings Control

This skill provides a **reliable, battle-tested pattern** for implementing Slider controls with text display in WPF/XAML Settings pages.

## When to Use This Skill

✅ **Use this pattern when:**
- Adding new slider controls to any Settings page
- Fixing slider TextBlock that doesn't display text
- Ensuring consistent UI across all settings pages
- Working with `ui:SettingsExpander`, `ui:SettingsCard`, or `ikw:SimpleStackPanel`

❌ **Do NOT use:**
- Simple standalone sliders without text display (use default WPF binding)
- Sliders in non-Settings contexts (e.g., dialog boxes, toolbars)

## The Problem with Standard Binding

**WPF's `ElementName` Binding fails inside certain container controls** like:
- `ui:SettingsExpander.Items`
- `ikw:SimpleStackPanel` (when nested deeply)
- Custom template controls

**Symptoms:**
- TextBlock displays in VS debug mode but not in normal startup
- Text appears/disappears intermittently
- No error messages, just blank TextBlock

## The Solution: Code-Behind Direct Assignment

### 1️⃣ XAML Template (NO Binding, NO Width)

```xml
<ui:SettingsCard Header="{i18n:I18n Key=YourSettingKey}">
    <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock x:Name="YourSliderText"
                   VerticalAlignment="Center"
                   FontFamily="Consolas"
                   TextAlignment="Right"/>
        <Slider x:Name="YourSlider"
                Width="200"
                Minimum="0"
                Maximum="100"
                TickFrequency="1"
                IsSnapToTickEnabled="True"
                ValueChanged="YourSlider_ValueChanged"/>
    </ikw:SimpleStackPanel>
</ui:SettingsCard>
```

**Key Points:**
- ✅ Use `x:Name` instead of `Text="{Binding...}"`
- ❌ **NEVER add fixed `Width`** to TextBlock (let it auto-size)
- ✅ Always include `FontFamily="Consolas"` (monospace for alignment)
- ✅ Add `TextAlignment="Right"` (works without fixed width)

### 2️⃣ C# Code-Behind Pattern

```csharp
using System;
using System.Windows;
using System.Windows.Controls;

namespace YourNamespace.SettingsViews.Pages
{
    public partial class YourPage : Page
    {
        private bool _isLoaded = false;

        public YourPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
        }

        #region Slider Text Management

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(YourSlider, YourSliderText, "{0:0}");
            // Add more sliders here...
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        #endregion

        #region Slider Event Handlers

        private void YourSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // ALWAYS update text first (before _isLoaded check)
            UpdateSliderText(YourSlider, YourSliderText, "{0:0}");

            if (!_isLoaded) return;

            // Save your settings here
            SettingsManager.Settings.YourSection.YourProperty = e.NewValue;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            
            if (settings?.YourSection != null)
            {
                YourSlider.Value = settings.YourSection.YourProperty;
            }
        }
    }
}
```

### 3️⃣ Common StringFormat Patterns

| Format | Example Output | Use Case |
|--------|---------------|----------|
| `"{0:0}"` | `0`, `42`, `255` | Integer values |
| `"{0:F0}"` | `-500`, `0`, `200` | Position offsets |
| `"{0:F1}"` | `2.5`, `0.8` | Decimal values (1 decimal) |
| `"{0:F2}"` | `5.00`, `1.50` | Precise decimals (2 decimals) |
| `"{0:P0}"` | `100%`, `50%`, `10%` | Percentages (auto ×100) |
| `"{0:0}ms"` | `3000ms`, `100ms` | Time in milliseconds |
| `"{0:0}秒"` | `10秒`, `30秒` | Time in seconds |
| `"{0:F2}x)"` | `1.00x)`, `1.50x)` | Scale factors |

## Complete Example: Multiple Sliders

### XAML:

```xml
<Page x:Class="Ink_Canvas.Windows.SettingsViews.Pages.ExamplePage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="clr-namespace:iNKORE.UI.WPF.Modern.Controls;assembly=iNKORE.UI.WPF.Modern.Controls"
      xmlns:ikw="clr-namespace:iNKORE.UI.WPF.Helpers;assembly=iNKORE.UI.WPF.Helpers">

    <StackPanel>
        
        <!-- Slider 1: Integer value -->
        <ui:SettingsCard Header="Threshold">
            <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock x:Name="ThresholdText"
                           VerticalAlignment="Center"
                           FontFamily="Consolas"
                           TextAlignment="Right"/>
                <Slider x:Name="ThresholdSlider"
                        Width="200" Minimum="30" Maximum="300"
                        TickFrequency="30" IsSnapToTickEnabled="True"
                        ValueChanged="ThresholdSlider_ValueChanged"/>
            </ikw:SimpleStackPanel>
        </ui:SettingsCard>

        <!-- Slider 2: Percentage -->
        <ui:SettingsCard Header="Opacity">
            <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock x:Name="OpacityText"
                           VerticalAlignment="Center"
                           FontFamily="Consolas"
                           TextAlignment="Right"/>
                <Slider x:Name="OpacitySlider"
                        Width="200" Minimum="0.1" Maximum="1.0"
                        TickFrequency="0.1" IsSnapToTickEnabled="True"
                        ValueChanged="OpacitySlider_ValueChanged"/>
            </ikw:SimpleStackPanel>
        </ui:SettingsCard>

        <!-- Slider 3: Time with unit -->
        <ui:SettingsCard Header="Delay">
            <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock x:Name="DelayText"
                           VerticalAlignment="Center"
                           FontFamily="Consolas"
                           TextAlignment="Right"/>
                <Slider x:Name="DelaySlider"
                        Width="200" Minimum="100" Maximum="1000"
                        TickFrequency="50" IsSnapToTickEnabled="True"
                        ValueChanged="DelaySlider_ValueChanged"/>
            </ikw:SimpleStackPanel>
        </ui:SettingsCard>

    </StackPanel>
</Page>
```

### C#:

```csharp
public partial class ExamplePage : Page
{
    private bool _isLoaded = false;

    public ExamplePage()
    {
        InitializeComponent();
        Loaded += ExamplePage_Loaded;
    }

    private void ExamplePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        _isLoaded = true;
        UpdateAllSliderTexts();
    }

    private void UpdateAllSliderTexts()
    {
        UpdateSliderText(ThresholdSlider, ThresholdText, "{0:0}");
        UpdateSliderText(OpacitySlider, OpacityText, "{0:P0}");
        UpdateSliderText(DelaySlider, DelayText, "{0:0} ms");
    }

    private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
    {
        if (slider == null || textBlock == null) return;
        textBlock.Text = string.Format(format, slider.Value);
    }

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderText(ThresholdSlider, ThresholdText, "{0:0}");
        if (!_isLoaded) return;

        SettingsManager.Settings.Example.Threshold = (int)e.NewValue;
        SettingsManager.SaveSettingsToFile();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderText(OpacitySlider, OpacityText, "{0:P0}");
        if (!_isLoaded) return;

        SettingsManager.Settings.Example.Opacity = e.NewValue;
        SettingsManager.SaveSettingsToFile();
    }

    private void DelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderText(DelaySlider, DelayText, "{0:0} ms");
        if (!_isLoaded) return;

        SettingsManager.Settings.Example.Delay = (int)e.NewValue;
        SettingsManager.SaveSettingsToFile();
    }

    private void LoadSettings()
    {
        var settings = SettingsManager.Settings;

        if (settings?.Example != null)
        {
            ThresholdSlider.Value = settings.Example.Threshold;
            OpacitySlider.Value = settings.Example.Opacity;
            DelaySlider.Value = settings.Example.Delay;
        }
    }
}
```

## Best Practices Checklist

- [x] **Always use `x:Name` on TextBlock** - never rely on ElementName binding
- [x] **Never set fixed Width on TextBlock** - let it auto-size to content
- [x] **Call `UpdateSliderText()` BEFORE `_isLoaded` check** in ValueChanged
- [x] **Call `UpdateAllSliderTexts()` AFTER `LoadSettings()`** in Loaded event
- [x] **Use Consolas font** for monospace alignment of numbers
- [x] **Include meaningful units in format string** (ms, %, px, etc.)
- [x] **Set `IsSnapToTickEnabled="True"` for discrete values**
- [x] **Handle null checks** in `UpdateSliderText()` method

## Troubleshooting

### Issue: Text still doesn't display

**Solution:** Verify the order in ValueChanged:
```csharp
private void Slider_ValueChanged(...)
{
    UpdateSliderText(slider, textBlock, format);  // ← MUST be FIRST
    if (!_isLoaded) return;                         // ← Then check flag
}
```

### Issue: Text shows wrong initial value

**Solution:** Ensure `UpdateAllSliderTexts()` is called AFTER `LoadSettings()`:
```csharp
private void Page_Loaded(...)
{
    LoadSettings();           // Sets Slider.Value from settings
    _isLoaded = true;
    UpdateAllSliderTexts();   // Reads current Slider.Value → sets TextBlock.Text
}
```

### Issue: Need to update text from external code

**Solution:** Call the helper directly:
```csharp
UpdateSliderText(MySlider, MySliderText, "{0:0}");
```

## Real-World Usage Examples

This pattern has been successfully implemented in **7 production pages** with **30+ sliders**:

1. **PowerPointPage.xaml** - 8 sliders (position + opacity)
2. **CanvasPage.xaml** - 4 sliders (ink fade, brush restore)
3. **AppearancePage.xaml** - 5 text blocks (scale, opacity)
4. **AdvancedPage.xaml** - 3 sliders (touch multiplier, bounds)
5. **InkRecognitionPage.xaml** - 4 sliders (threshold, sensitivity)
6. **RandomDrawPage.xaml** - 6 sliders (latency, volume, ML params)
7. **AutomationPage.xaml** - 1 slider (no text needed)

All pages now show slider values reliably in both debug and release builds! 🚀
