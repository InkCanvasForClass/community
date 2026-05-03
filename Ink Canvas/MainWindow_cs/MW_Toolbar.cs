using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar;
using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Ink_Canvas
{
    public partial class MainWindow
    {
        internal ToolbarImageButton SymbolIconDelete { get; private set; }
        internal ToolbarImageButton Eraser_Icon { get; private set; }
        internal ToolbarImageButton EraserByStrokes_Icon { get; private set; }
        internal ToolbarImageButton SymbolIconSelect { get; private set; }
        internal ToolbarImageButton ShapeDrawFloatingBarBtn { get; private set; }
        internal ToolbarImageButton SymbolIconUndo { get; private set; }
        internal ToolbarImageButton SymbolIconRedo { get; private set; }
        internal ToolbarImageButton CursorWithDelFloatingBarBtn { get; private set; }
        internal ToolbarImageButton WhiteboardFloatingBarBtn { get; private set; }
        internal ToolbarImageButton ToolsFloatingBarBtn { get; private set; }
        internal ToolbarImageButton Fold_Icon { get; private set; }
        internal ToolbarImageButton Freeze_Icon { get; private set; }

        internal Panel FloatingBarRootPanel => BorderFloatingBarMoveControls?.Parent as Panel;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchDrawShapeBorderAutoHide { get; } =
            new iNKORE.UI.WPF.Modern.Controls.ToggleSwitch { IsOn = true };

        internal GeometryButton ImageDrawLine => ShapeDrawPopupContent?.DrawLineBtn;
        internal GeometryButton ImageDrawDashedLine => ShapeDrawPopupContent?.DrawDashedLineBtn;
        internal GeometryButton ImageDrawDotLine => ShapeDrawPopupContent?.DrawDotLineBtn;
        internal GeometryButton ImageDrawArrow => ShapeDrawPopupContent?.DrawArrowBtn;
        internal GeometryButton ImageDrawParallelLine => ShapeDrawPopupContent?.DrawParallelLineBtn;

        internal GeometryButton BoardImageDrawLine => BoardShapeDrawPopupContent?.DrawLineBtn;
        internal GeometryButton BoardImageDrawDashedLine => BoardShapeDrawPopupContent?.DrawDashedLineBtn;
        internal GeometryButton BoardImageDrawDotLine => BoardShapeDrawPopupContent?.DrawDotLineBtn;
        internal GeometryButton BoardImageDrawArrow => BoardShapeDrawPopupContent?.DrawArrowBtn;
        internal GeometryButton BoardImageDrawParallelLine => BoardShapeDrawPopupContent?.DrawParallelLineBtn;

        internal void AttachCursorIconView(ToolbarImageButton btn) => Cursor_Icon = btn;
        internal void AttachPenIconView(ToolbarImageButton btn) => Pen_Icon = btn;
        internal void AttachSymbolIconDelete(ToolbarImageButton btn) => SymbolIconDelete = btn;
        internal void AttachEraserIcon(ToolbarImageButton btn) { Eraser_Icon = btn; EraserSizePanel.PlacementTarget = btn; }
        internal void AttachEraserByStrokesIcon(ToolbarImageButton btn) => EraserByStrokes_Icon = btn;
        internal void AttachSymbolIconSelect(ToolbarImageButton btn) => SymbolIconSelect = btn;
        internal void AttachShapeDrawBtn(ToolbarImageButton btn)
        {
            ShapeDrawFloatingBarBtn = btn;
            BorderDrawShape.PlacementTarget = btn;
        }
        internal void AttachSymbolIconUndo(ToolbarImageButton btn) => SymbolIconUndo = btn;
        internal void AttachSymbolIconRedo(ToolbarImageButton btn) => SymbolIconRedo = btn;
        internal void AttachCursorWithDelBtn(ToolbarImageButton btn) => CursorWithDelFloatingBarBtn = btn;
        internal void AttachWhiteboardBtn(ToolbarImageButton btn) => WhiteboardFloatingBarBtn = btn;
        internal void AttachToolsBtn(ToolbarImageButton btn)
        {
            ToolsFloatingBarBtn = btn;
            BorderTools.PlacementTarget = btn;
        }
        internal void AttachFoldIcon(ToolbarImageButton btn) => Fold_Icon = btn;

        #region PenPalette property mappings
        internal ComboBox ComboBoxPenStyle => PenPalettePopupContent?.PenStyleComboBox ?? BoardPenPalettePopupContent?.PenStyleComboBox;
        internal ComboBox BoardComboBoxPenStyle => BoardPenPalettePopupContent?.PenStyleComboBox;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchEnableNibMode => PenPalettePopupContent?.NibModeToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch BoardToggleSwitchEnableNibMode => BoardPenPalettePopupContent?.NibModeToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch FloatingBarToggleSwitchEnableInkToShape => PenPalettePopupContent?.InkToShapeToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch BoardToggleSwitchEnableInkToShape => BoardPenPalettePopupContent?.InkToShapeToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchInkFadeInPanel => PenPalettePopupContent?.InkFadeToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchInkFadeInPanel2 => PenPalettePopupContent?.InkFadeToggle2;
        internal Slider InkWidthSlider => PenPalettePopupContent?.InkWidthSlider;
        internal Slider BoardInkWidthSlider => BoardPenPalettePopupContent?.InkWidthSlider;
        internal Slider InkAlphaSlider => PenPalettePopupContent?.InkAlphaSlider;
        internal Slider BoardInkAlphaSlider => BoardPenPalettePopupContent?.InkAlphaSlider;
        internal Slider HighlighterWidthSlider => PenPalettePopupContent?.HighlighterWidthSlider;
        internal Slider BoardHighlighterWidthSlider => BoardPenPalettePopupContent?.HighlighterWidthSlider;
        internal Button BoardBrushModeButton => PenPalettePopupContent?.BrushModeBtn;
        internal System.Windows.Shapes.Path BoardBrushModeIcon => PenPalettePopupContent?.BrushModeIcon;

        internal Border DefaultPenTabButton => PenPalettePopupContent?.DefaultPenTab ?? BoardPenPalettePopupContent?.DefaultPenTab;
        internal Border HighlightPenTabButton => PenPalettePopupContent?.HighlightPenTab ?? BoardPenPalettePopupContent?.HighlightPenTab;
        internal FrameworkElement DefaultPenTabButtonIndicator => PenPalettePopupContent?.DefaultPenTabIndicator ?? BoardPenPalettePopupContent?.DefaultPenTabIndicator;
        internal FrameworkElement HighlightPenTabButtonIndicator => PenPalettePopupContent?.HighlightPenTabIndicator ?? BoardPenPalettePopupContent?.HighlightPenTabIndicator;
        internal TextBlock DefaultPenTabButtonText => PenPalettePopupContent?.DefaultPenTabText ?? BoardPenPalettePopupContent?.DefaultPenTabText;
        internal TextBlock HighlightPenTabButtonText => PenPalettePopupContent?.HighlightPenTabText ?? BoardPenPalettePopupContent?.HighlightPenTabText;

        internal Viewbox DefaultPenPropsPanel => PenPalettePopupContent?.DefaultPenPropsPanel ?? BoardPenPalettePopupContent?.DefaultPenPropsPanel;
        internal Viewbox HighlighterPenPropsPanel => PenPalettePopupContent?.HighlighterPenPropsPanel ?? BoardPenPalettePopupContent?.HighlighterPenPropsPanel;
        internal Viewbox DefaultPenColorsPanel => PenPalettePopupContent?.DefaultPenColorsPanel ?? BoardPenPalettePopupContent?.DefaultPenColorsPanel;
        internal Viewbox HighlighterPenColorsPanel => PenPalettePopupContent?.HighlighterPenColorsPanel ?? BoardPenPalettePopupContent?.HighlighterPenColorsPanel;

        internal Viewbox BoardDefaultPenPropsPanel => BoardPenPalettePopupContent?.DefaultPenPropsPanel;
        internal Viewbox BoardHighlighterPenPropsPanel => BoardPenPalettePopupContent?.HighlighterPenPropsPanel;
        internal Viewbox BoardDefaultPenColorsPanel => BoardPenPalettePopupContent?.DefaultPenColorsPanel;
        internal Viewbox BoardHighlighterPenColorsPanel => BoardPenPalettePopupContent?.HighlighterPenColorsPanel;

        internal Border BoardDefaultPenTabButton => BoardPenPalettePopupContent?.DefaultPenTab;
        internal Border BoardHighlightPenTabButton => BoardPenPalettePopupContent?.HighlightPenTab;
        internal FrameworkElement BoardDefaultPenTabButtonIndicator => BoardPenPalettePopupContent?.DefaultPenTabIndicator;
        internal FrameworkElement BoardHighlightPenTabButtonIndicator => BoardPenPalettePopupContent?.HighlightPenTabIndicator;
        internal TextBlock BoardDefaultPenTabButtonText => BoardPenPalettePopupContent?.DefaultPenTabText;
        internal TextBlock BoardHighlightPenTabButtonText => BoardPenPalettePopupContent?.HighlightPenTabText;

        internal PenColorButton BorderPenColorBlack => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorBlack;
        internal PenColorButton BorderPenColorWhite => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorWhite;
        internal PenColorButton BorderPenColorRed => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorRed;
        internal PenColorButton BorderPenColorYellow => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorYellow;
        internal PenColorButton BorderPenColorGreen => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorGreen;
        internal PenColorButton BorderPenColorBlue => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorBlue;
        internal PenColorButton BorderPenColorPink => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorPink;
        internal PenColorButton BorderPenColorTeal => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorTeal;
        internal PenColorButton BorderPenColorOrange => (PenPalettePopupContent ?? BoardPenPalettePopupContent)?.DefaultPenColorOrange;

        internal PenColorButton BoardBorderPenColorBlack => BoardPenPalettePopupContent?.DefaultPenColorBlack;
        internal PenColorButton BoardBorderPenColorWhite => BoardPenPalettePopupContent?.DefaultPenColorWhite;
        internal PenColorButton BoardBorderPenColorRed => BoardPenPalettePopupContent?.DefaultPenColorRed;
        internal PenColorButton BoardBorderPenColorYellow => BoardPenPalettePopupContent?.DefaultPenColorYellow;
        internal PenColorButton BoardBorderPenColorGreen => BoardPenPalettePopupContent?.DefaultPenColorGreen;
        internal PenColorButton BoardBorderPenColorBlue => BoardPenPalettePopupContent?.DefaultPenColorBlue;
        internal PenColorButton BoardBorderPenColorPink => BoardPenPalettePopupContent?.DefaultPenColorPink;
        internal PenColorButton BoardBorderPenColorTeal => BoardPenPalettePopupContent?.DefaultPenColorTeal;
        internal PenColorButton BoardBorderPenColorOrange => BoardPenPalettePopupContent?.DefaultPenColorOrange;

        internal PenColorButton HighlighterPenColorBlack => PenPalettePopupContent?.HighlighterPenColorBlack ?? BoardPenPalettePopupContent?.HighlighterPenColorBlack;
        internal PenColorButton HighlighterPenColorWhite => PenPalettePopupContent?.HighlighterPenColorWhite ?? BoardPenPalettePopupContent?.HighlighterPenColorWhite;
        internal PenColorButton HighlighterPenColorRed => PenPalettePopupContent?.HighlighterPenColorRed ?? BoardPenPalettePopupContent?.HighlighterPenColorRed;
        internal PenColorButton HighlighterPenColorYellow => PenPalettePopupContent?.HighlighterPenColorYellow ?? BoardPenPalettePopupContent?.HighlighterPenColorYellow;
        internal PenColorButton HighlighterPenColorGreen => PenPalettePopupContent?.HighlighterPenColorGreen ?? BoardPenPalettePopupContent?.HighlighterPenColorGreen;
        internal PenColorButton HighlighterPenColorZinc => PenPalettePopupContent?.HighlighterPenColorZinc ?? BoardPenPalettePopupContent?.HighlighterPenColorZinc;
        internal PenColorButton HighlighterPenColorBlue => PenPalettePopupContent?.HighlighterPenColorBlue ?? BoardPenPalettePopupContent?.HighlighterPenColorBlue;
        internal PenColorButton HighlighterPenPenColorPurple => PenPalettePopupContent?.HighlighterPenColorPurple ?? BoardPenPalettePopupContent?.HighlighterPenColorPurple;
        internal PenColorButton HighlighterPenColorTeal => PenPalettePopupContent?.HighlighterPenColorTeal ?? BoardPenPalettePopupContent?.HighlighterPenColorTeal;
        internal PenColorButton HighlighterPenColorOrange => PenPalettePopupContent?.HighlighterPenColorOrange ?? BoardPenPalettePopupContent?.HighlighterPenColorOrange;

        internal PenColorButton BoardHighlighterPenColorBlack => BoardPenPalettePopupContent?.HighlighterPenColorBlack;
        internal PenColorButton BoardHighlighterPenColorWhite => BoardPenPalettePopupContent?.HighlighterPenColorWhite;
        internal PenColorButton BoardHighlighterPenColorRed => BoardPenPalettePopupContent?.HighlighterPenColorRed;
        internal PenColorButton BoardHighlighterPenColorYellow => BoardPenPalettePopupContent?.HighlighterPenColorYellow;
        internal PenColorButton BoardHighlighterPenColorGreen => BoardPenPalettePopupContent?.HighlighterPenColorGreen;
        internal PenColorButton BoardHighlighterPenColorZinc => BoardPenPalettePopupContent?.HighlighterPenColorZinc;
        internal PenColorButton BoardHighlighterPenColorBlue => BoardPenPalettePopupContent?.HighlighterPenColorBlue;
        internal PenColorButton BoardHighlighterPenPenColorPurple => BoardPenPalettePopupContent?.HighlighterPenColorPurple;
        internal PenColorButton BoardHighlighterPenColorTeal => BoardPenPalettePopupContent?.HighlighterPenColorTeal;
        internal PenColorButton BoardHighlighterPenColorOrange => BoardPenPalettePopupContent?.HighlighterPenColorOrange;

        internal Border ColorThemeSwitch => PenPalettePopupContent?.ColorThemeSwitch ?? BoardPenPalettePopupContent?.ColorThemeSwitch;
        internal Image ColorThemeSwitchIcon => PenPalettePopupContent?.ColorThemeSwitchIcon ?? BoardPenPalettePopupContent?.ColorThemeSwitchIcon;
        internal TextBlock ColorThemeSwitchTextBlock => PenPalettePopupContent?.ColorThemeSwitchText ?? BoardPenPalettePopupContent?.ColorThemeSwitchText;
        internal Border BoardColorThemeSwitch => BoardPenPalettePopupContent?.ColorThemeSwitch;
        internal Image BoardColorThemeSwitchIcon => BoardPenPalettePopupContent?.ColorThemeSwitchIcon;
        internal TextBlock BoardColorThemeSwitchTextBlock => BoardPenPalettePopupContent?.ColorThemeSwitchText;

        internal FrameworkElement NibModeSimpleStackPanel => PenPalettePopupContent?.NibModePanel ?? BoardPenPalettePopupContent?.NibModePanel;
        internal FrameworkElement BoardNibModeSimpleStackPanel => BoardPenPalettePopupContent?.NibModePanel;
        internal FrameworkElement InkFadeControlPanel1 => PenPalettePopupContent?.InkFadeControlPanel ?? BoardPenPalettePopupContent?.InkFadeControlPanel;
        internal FrameworkElement InkFadeControlPanel2 => PenPalettePopupContent?.InkFadeControlPanel2 ?? BoardPenPalettePopupContent?.InkFadeControlPanel2;
        #endregion

        #region Eraser property mappings
        internal ComboBox ComboBoxEraserSizeFloatingBar => EraserPopupContent?.EraserSizeComboBox ?? BoardEraserPopupContent?.EraserSizeComboBox;
        internal ComboBox BoardComboBoxEraserSize => BoardEraserPopupContent?.EraserSizeComboBox;
        internal Border CircleEraserTabButton => EraserPopupContent?.CircleTab ?? BoardEraserPopupContent?.CircleTab;
        internal Border RectangleEraserTabButton => EraserPopupContent?.RectangleTab ?? BoardEraserPopupContent?.RectangleTab;
        internal FrameworkElement CircleEraserTabButtonIndicator => EraserPopupContent?.CircleTabIndicator ?? BoardEraserPopupContent?.CircleTabIndicator;
        internal FrameworkElement RectangleEraserTabButtonIndicator => EraserPopupContent?.RectangleTabIndicator ?? BoardEraserPopupContent?.RectangleTabIndicator;
        internal TextBlock CircleEraserTabButtonText => EraserPopupContent?.CircleTabText ?? BoardEraserPopupContent?.CircleTabText;
        internal TextBlock RectangleEraserTabButtonText => EraserPopupContent?.RectangleTabText ?? BoardEraserPopupContent?.RectangleTabText;
        internal Border BoardCircleEraserTabButton => BoardEraserPopupContent?.CircleTab;
        internal Border BoardRectangleEraserTabButton => BoardEraserPopupContent?.RectangleTab;
        internal FrameworkElement BoardCircleEraserTabButtonIndicator => BoardEraserPopupContent?.CircleTabIndicator;
        internal FrameworkElement BoardRectangleEraserTabButtonIndicator => BoardEraserPopupContent?.RectangleTabIndicator;
        internal TextBlock BoardCircleEraserTabButtonText => BoardEraserPopupContent?.CircleTabText;
        internal TextBlock BoardRectangleEraserTabButtonText => BoardEraserPopupContent?.RectangleTabText;
        #endregion

        #region Gesture property mappings
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchEnableMultiTouchMode => FloatingBarGesturePopupContent?.MultiTouchToggle ?? BoardGesturePopupContent?.MultiTouchToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch BoardToggleSwitchEnableMultiTouchMode => BoardGesturePopupContent?.MultiTouchToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchEnableTwoFingerTranslate => FloatingBarGesturePopupContent?.TwoFingerTranslateToggle ?? BoardGesturePopupContent?.TwoFingerTranslateToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch BoardToggleSwitchEnableTwoFingerTranslate => BoardGesturePopupContent?.TwoFingerTranslateToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchEnableTwoFingerZoom => FloatingBarGesturePopupContent?.TwoFingerZoomToggle ?? BoardGesturePopupContent?.TwoFingerZoomToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch BoardToggleSwitchEnableTwoFingerZoom => BoardGesturePopupContent?.TwoFingerZoomToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchEnableTwoFingerRotation => FloatingBarGesturePopupContent?.TwoFingerRotationToggle ?? BoardGesturePopupContent?.TwoFingerRotationToggle;
        internal iNKORE.UI.WPF.Modern.Controls.ToggleSwitch BoardToggleSwitchEnableTwoFingerRotation => BoardGesturePopupContent?.TwoFingerRotationToggle;
        internal FrameworkElement TwoFingerGestureSimpleStackPanel => FloatingBarGesturePopupContent?.TwoFingerGestureSimpleStackPanel;
        #endregion

        #region BackgroundPalette property mappings
        internal Slider BackgroundRSlider => BackgroundPalettePopupContent?.RSlider;
        internal Slider BackgroundGSlider => BackgroundPalettePopupContent?.GSlider;
        internal Slider BackgroundBSlider => BackgroundPalettePopupContent?.BSlider;
        internal TextBlock BackgroundRValue => BackgroundPalettePopupContent?.RValue;
        internal TextBlock BackgroundGValue => BackgroundPalettePopupContent?.GValue;
        internal TextBlock BackgroundBValue => BackgroundPalettePopupContent?.BValue;
        internal Border BackgroundColorPreview => BackgroundPalettePopupContent?.ColorPreview;
        internal Button ApplyBackgroundColorBtn => BackgroundPalettePopupContent?.ApplyBtn;
        internal Border WhiteboardModeBtn => BackgroundPalettePopupContent?.WhiteboardBtn;
        internal Border BlackboardModeBtn => BackgroundPalettePopupContent?.BlackboardBtn;
        #endregion

        internal void InitializeToolbarPlugins()
        {
            LogHelper.WriteLogToFile("MW_Toolbar: InitializeToolbarPlugins 开始", LogHelper.LogType.Info);
            try
            {
                ToolbarHost = new ToolbarHost(this);
                var slots = new Dictionary<ToolbarSlot, Panel>
                {
                    { ToolbarSlot.FloatingBarMain, StackPanelFloatingBar },
                    { ToolbarSlot.FloatingBarCanvasControls, StackPanelCanvasControls },
                    { ToolbarSlot.FloatingBarEnd, StackPanelFloatingBarEnd },
                    { ToolbarSlot.BlackboardLeft, BlackboardLeftSide },
                    { ToolbarSlot.BlackboardRight, BlackboardRightSide }
                };
                ToolbarRegistry.Populate(ToolbarHost, slots, Settings?.Toolbar);
                LogHelper.WriteLogToFile("MW_Toolbar: InitializeToolbarPlugins 完成", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_Toolbar: InitializeToolbarPlugins 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        internal void RebuildToolbar()
        {
            LogHelper.WriteLogToFile("MW_Toolbar: RebuildToolbar 开始", LogHelper.LogType.Info);
            try
            {
                ToolbarRegistry.ClearInjected(StackPanelFloatingBar);
                ToolbarRegistry.ClearInjected(StackPanelCanvasControls);
                ToolbarRegistry.ClearInjected(StackPanelFloatingBarEnd);
                ToolbarRegistry.ClearInjected(BlackboardLeftSide);
                ToolbarRegistry.ClearInjected(BlackboardRightSide);
                InitializeToolbarPlugins();
                LogHelper.WriteLogToFile("MW_Toolbar: RebuildToolbar 完成", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_Toolbar: RebuildToolbar 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }
    }
}
