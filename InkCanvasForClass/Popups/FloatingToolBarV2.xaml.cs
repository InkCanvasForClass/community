using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Ink_Canvas.Popups.ColorPalette;
using static Ink_Canvas.Popups.SelectionPopup;
using static Ink_Canvas.Popups.ShapeDrawingPopup;

namespace Ink_Canvas
{

    public enum FloatingBarItemType {
        Button,
        StateButton,
        Separator
    }

    public class FloatingBarItem {
        public string Name { get; set; }
        public ImageSource IconSource { get; set; }
        public bool Selected { get; set; } = false;
        public bool IsVisible { get; set; } = true;
        public string IconSourceResourceKey { get; set; }
        public double IconHeight { get; set; } = 21;
        public Color? IconColor { get; set; } = null; // 该属性仅用于纯色图标用于在动态透明模式下恢复图标原来的自定义颜色
        public bool IsSemiTransparent { get; set; } = false;
        public Color? PressFeedbackColor { get; set; } = null;
        public FloatingBarItemType Type { get; set; }
        public MainWindow.ICCToolsEnum ToolType { get; set; }
        public SolidColorBrush _backgroundBrush {
            get {
                if (Selected) return new SolidColorBrush(Color.FromArgb((byte)(IsSemiTransparent ? 128 : 255) ,37, 99, 235));
                return new SolidColorBrush(Colors.Transparent);
            }
        }
        public Visibility _itemVisibility {
            get {
                if (!IsVisible) return Visibility.Collapsed;
                return Type != FloatingBarItemType.Separator ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        public Visibility _separatorVisibility {
            get {
                if (!IsVisible) return Visibility.Collapsed;
                return Type != FloatingBarItemType.Separator ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        public double _separatorOpacity {
            get => IsSemiTransparent ? 0.35 : 1;
        }
        public SolidColorBrush _pressFeedbackColorBrush {
            get {
                if (PressFeedbackColor == null) return new SolidColorBrush(Color.FromArgb((byte)(IsSemiTransparent ? 96 : 255),225, 225, 225));
                return new SolidColorBrush(Color.FromArgb((byte)(IsSemiTransparent ? 96 : 255),((Color)PressFeedbackColor).R, 
                    ((Color)PressFeedbackColor).G, ((Color)PressFeedbackColor).B));
            }
        }
    }

    /// <summary>
    /// FloatingToolBarV2.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingToolBarV2 : Window {

        public FloatingToolBarV2() {
            InitializeComponent();

            ToolBarItemsControl.ItemsSource = ToolbarItems;
            //var clonedDrawing = (FindResource("CursorIcon") as DrawingImage).Clone();
            //((clonedDrawing.Drawing as DrawingGroup).Children[0] as
            //    GeometryDrawing).Brush = new SolidColorBrush(Colors.Black);
            
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Cursor",
                IconSource = FindResource("CursorIcon") as DrawingImage,
                IconSourceResourceKey = "CursorIcon",
                IsVisible = true,
                Selected = false,
                IconHeight = 21.5,
                ToolType = MainWindow.ICCToolsEnum.CursorMode,
                Type = FloatingBarItemType.StateButton,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Pen",
                IconSource = FindResource("PenIcon") as DrawingImage,
                IconSourceResourceKey = "PenIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                ToolType = MainWindow.ICCToolsEnum.PenMode,
                Type = FloatingBarItemType.StateButton,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Clear",
                IconSource = FindResource("TrashBinIcon") as DrawingImage,
                IconSourceResourceKey = "TrashBinIcon",
                IsVisible = true,
                IconColor = Color.FromRgb(224, 27, 36),
                IconHeight = 22,
                Selected = false,
                PressFeedbackColor = Color.FromRgb(254, 226, 226),
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "SeparatorA",
                IsVisible = true,
                Type = FloatingBarItemType.Separator,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Eraser",
                IconSource = FindResource("EraserIcon") as DrawingImage,
                IconSourceResourceKey = "EraserIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                ToolType = MainWindow.ICCToolsEnum.EraseByGeometryMode,
                Type = FloatingBarItemType.StateButton,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "ShapeDrawing",
                IconSource = FindResource("ShapesIcon") as DrawingImage,
                IconSourceResourceKey = "ShapesIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Select",
                IconSource = FindResource("SelectIcon") as DrawingImage,
                IconSourceResourceKey = "SelectIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                ToolType = MainWindow.ICCToolsEnum.LassoMode,
                Type = FloatingBarItemType.StateButton,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "SeparatorB",
                IsVisible = true,
                Type = FloatingBarItemType.Separator,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Undo",
                IconSource = FindResource("UndoIcon") as DrawingImage,
                IconSourceResourceKey = "UndoIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Redo",
                IconSource = FindResource("RedoIcon") as DrawingImage,
                IconSourceResourceKey = "RedoIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "SeparatorC",
                IsVisible = true,
                Type = FloatingBarItemType.Separator,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Whiteboard",
                IconSource = FindResource("WhiteboardIcon") as DrawingImage,
                IconSourceResourceKey = "WhiteboardIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Gesture",
                IconSource = FindResource("GestureIcon") as DrawingImage,
                IconSourceResourceKey = "GestureIcon",
                IsVisible = true,
                IconHeight = 23,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Menu",
                IconSource = FindResource("MoreIcon") as DrawingImage,
                IconSourceResourceKey = "MoreIcon",
                IsVisible = true,
                IconHeight = 21.5,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });
            ToolbarItems.Add(new FloatingBarItem() {
                Name = "Fold",
                IconSource = FindResource("FoldIcon") as DrawingImage,
                IconSourceResourceKey = "FoldIcon",
                IsVisible = true,
                IconHeight = 22.5,
                Selected = false,
                Type = FloatingBarItemType.Button,
            });

            ReMeasureToolBar();
            UpdateToolBarSelectedTool(MainWindow.ICCToolsEnum.CursorMode);
            UpdateToolBarVariant(1);
            UpdateToolBarDynamicOpacityVariant(1);

            double widthInDevicePixels = GetSystemMetrics(0); // SM_CXSCREEN
            double widthInDIP = SystemParameters.WorkArea.Right; // Device independent pixels.
            double scalingFactor = widthInDIP/widthInDevicePixels;
            ScalingFactor = scalingFactor;

        }

        public void FloatingBarV2_Loaded(object sender, RoutedEventArgs e) {

        }

        public void HideAllPopups() {
            PenPaletteV2Popup.IsOpen = false;
            SelectionPopupV2.IsOpen = false;
            ShapeDrawingPopupV2.IsOpen = false;
        }

        #region PopupV2 事件转发和内部处理

        /// <summary>
        /// 绑定调色盘V2的事件，部分事件会在内部进行处理后转发到外部
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PenPaletteV2_Loaded(object sender, RoutedEventArgs e) {
            PenPaletteV2.ColorSelectionChanged += (o, args) => {
                PenPaletteV2_ColorSelectionChanged?.Invoke(o, args);
            };
            PenPaletteV2.CustomColorChanged += (o, args) => {
                PenPaletteV2_CustomColorChanged?.Invoke(o, args);
            };
            PenPaletteV2.PenModeChanged += (o, args) => {
                PenPaletteV2_PenModeChanged?.Invoke(o, args);
            };
            PenPaletteV2.InkRecognitionChanged += (o, args) => {
                PenPaletteV2_InkRecognitionChanged?.Invoke(o, args);
            };
            PenPaletteV2.PressureSimulationChanged += (o, args) => {
                PenPaletteV2_PressureSimulationChanged?.Invoke(o, args);
            };
            PenPaletteV2.ColorModeChanged += (o, args) => {
                PenPaletteV2_ColorModeChanged?.Invoke(o, args);
            };
            PenPaletteV2.QuickActionsVisibilityChanged += (o, args) => {
                PenPaletteV2_QuickActionsVisibilityChanged?.Invoke(o, args);
            };
            PenPaletteV2.PaletteShouldCloseEvent += (o, args) => {
                PenPaletteV2Popup.IsOpen = false;
                PenPaletteV2_PaletteShouldCloseEvent?.Invoke(o, args);
            };
        }

        public event EventHandler<ColorSelectionChangedEventArgs> PenPaletteV2_ColorSelectionChanged;
        public event EventHandler<CustomColorChangedEventArgs> PenPaletteV2_CustomColorChanged;
        public event EventHandler<PenModeChangedEventArgs> PenPaletteV2_PenModeChanged;
        public event EventHandler<InkRecognitionChangedEventArgs> PenPaletteV2_InkRecognitionChanged;
        public event EventHandler<PressureSimulationChangedEventArgs> PenPaletteV2_PressureSimulationChanged;
        public event EventHandler<ColorModeChangedEventArgs> PenPaletteV2_ColorModeChanged;
        public event EventHandler<QuickActionsVisibilityChangedEventsArgs> PenPaletteV2_QuickActionsVisibilityChanged;
        public event EventHandler<RoutedEventArgs> PenPaletteV2_PaletteShouldCloseEvent;

        /// <summary>
        /// 绑定选择弹窗V2的事件，部分事件会在内部进行处理后转发到外部
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectionV2_Loaded(object sender, RoutedEventArgs e) {
            SelectionV2.SelectionPopupShouldCloseEvent += (o, args) => {
                SelectionPopupV2.IsOpen = false;
                SelectionV2_SelectionPopupShouldCloseEvent?.Invoke(o, args);
            };
            SelectionV2.SelectAllEvent += (o, args) => {
                SelectionV2_SelectAllEvent?.Invoke(o, args);
            };
            SelectionV2.UnSelectEvent += (o, args) => {
                SelectionV2_UnSelectEvent?.Invoke(o, args);
            };
            SelectionV2.ReverseSelectEvent += (o, args) => {
                SelectionV2_ReverseSelectEvent?.Invoke(o, args);
            };
            SelectionV2.ApplyScaleToStylusTipChanged += (o, args) => {
                SelectionV2_ApplyScaleToStylusTipChanged?.Invoke(o, args);
            };
            SelectionV2.OnlyHitTestFullyContainedStrokesChanged += (o, args) => {
                SelectionV2_OnlyHitTestFullyContainedStrokesChanged?.Invoke(o, args);
            };
            SelectionV2.AllowClickToSelectLockedStrokeChanged += (o, args) => {
                SelectionV2_AllowClickToSelectLockedStrokeChanged?.Invoke(o, args);
            };
            SelectionV2.SelectionModeChanged += (o, args) => {
                SelectionV2_SelectionModeChanged?.Invoke(o, args);
            };
        }

        public event EventHandler<RoutedEventArgs> SelectionV2_SelectionPopupShouldCloseEvent;
        public event EventHandler<RoutedEventArgs> SelectionV2_SelectAllEvent;
        public event EventHandler<RoutedEventArgs> SelectionV2_UnSelectEvent;
        public event EventHandler<RoutedEventArgs> SelectionV2_ReverseSelectEvent;
        public event EventHandler<RoutedEventArgs> SelectionV2_ApplyScaleToStylusTipChanged;
        public event EventHandler<RoutedEventArgs> SelectionV2_OnlyHitTestFullyContainedStrokesChanged;
        public event EventHandler<RoutedEventArgs> SelectionV2_AllowClickToSelectLockedStrokeChanged;
        public event EventHandler<SelectionModeChangedEventArgs> SelectionV2_SelectionModeChanged;

        /// <summary>
        /// 绑定形状绘制弹窗V2的事件，部分事件会在内部进行处理后转发到外部
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShapeDrawingV2_Loaded(object sender, RoutedEventArgs e) {
            ShapeDrawingV2.ShapeDrawingPopupShouldCloseEvent += (o, args) => {
                ShapeDrawingPopupV2.IsOpen = false;
                ShapeDrawingV2_ShapeDrawingPopupShouldCloseEvent?.Invoke(o, args);
            };
            ShapeDrawingV2.ShapeSelectedEvent += (o, args) => {
                ShapeDrawingV2_ShapeSelectedEvent?.Invoke(o, args);
            };
        }

        public event EventHandler<RoutedEventArgs> ShapeDrawingV2_ShapeDrawingPopupShouldCloseEvent;
        public event EventHandler<ShapeSelectedEventArgs> ShapeDrawingV2_ShapeSelectedEvent;

        #endregion

        private double ScalingFactor;

        public ObservableCollection<FloatingBarItem> ToolbarItems { get; set; } =
            new ObservableCollection<FloatingBarItem>();

        private Grid _mouseDownButton = null;
        private bool _isMouseDownHeadIcon = false;

        private double CaculateToolBarWindowWidth(Collection<FloatingBarItem> toolbarItems) {
            // ------------------------------------------------------//
            var containerMargin = 4D;       // 内容StackPanel的Margin
            var itemWidth = 42D;            // 按钮宽度
            var itemsSpacing = 2D;          // 按钮之间的间距
            var headIconSpacingRight = 4D;  // icc图标的右间距
            var separatorWidth = 1D;        // 分隔符宽度
            var separatorSpacing = 3D;      // 分隔符左右的间距
            // ------------------------------------------------------//

            var _width = containerMargin * 2 + itemWidth + headIconSpacingRight - itemsSpacing;
            foreach (var i in toolbarItems) {
                if (!i.IsVisible) continue;
                if (i.Type != FloatingBarItemType.Separator) _width += itemsSpacing + itemWidth; 
                    else _width += separatorSpacing + separatorWidth + separatorSpacing - itemsSpacing;
            }

            return _width;
        }

        /// <summary>
        /// 修改工具栏的变体
        /// </summary>
        /// <param name="variant">0为全长，1为鼠标模式下变体，2为迷你，3为仅HeadIcon图标</param>
        private void UpdateToolBarVariant(int variant) {
            var _snapTypeTemp = SnapType;
            ToolBarNowVariantMode = variant;
            HideAllPopups();
            IEnumerable<FloatingBarItem> items;
            if (variant == 0) items = ToolbarItems.AsEnumerable();
            else if (variant == 1)
                items = ToolbarItems.Where((item, i) => (new string[] {
                    "Cursor", "Pen", "Clear", "SeparatorC", "Whiteboard", "Gesture", "Menu", "Fold"
                }).Contains(item.Name));
            else if (variant == 2)
                items = ToolbarItems.Where((item, i) => (new string[] {
                    "Cursor", "Pen", "Clear"
                }).Contains(item.Name));
            else if (variant == 3) 
                items = ToolbarItems.Where((item, i) => item.Selected);
            else return;

            foreach (var fi in ToolbarItems) {
                fi.IsVisible = items.Contains(fi);
            }

            CollectionViewSource.GetDefaultView(ToolbarItems).Refresh();
            ReMeasureToolBar();
            if (_snapTypeTemp == ToolBarSnapType.RightSide) {
                Left = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24;
            } else if (_snapTypeTemp == ToolBarSnapType.RightTopCorner) {
                Top = -24;
                Left = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24;
            } else if (_snapTypeTemp == ToolBarSnapType.RightBottomCorner) {
                Top = System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 24;
                Left = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24;
            }
        }

        private void UpdateToolBarSelectedTool(MainWindow.ICCToolsEnum type) {
            var _i = ToolbarItems.Where((item, i) =>
                item.Type == FloatingBarItemType.StateButton &&
                item.ToolType == type).Single();
            var nowSelected = ToolbarItems.Where((item, i) =>
                item.Selected);
            if (nowSelected.Any() && nowSelected.Count()==1 && _i == nowSelected.Single()) return;

            foreach (var fi in ToolbarItems) {
                if (fi.Type == FloatingBarItemType.Separator) continue;
                fi.Selected = false;
                fi.IconSource = FindResource(fi.IconSourceResourceKey) as ImageSource;
            }
            
            _i.Selected = true;
            var clonedIcon = _i.IconSource.Clone() as DrawingImage;
            foreach (var d in (clonedIcon.Drawing as DrawingGroup).Children) 
                ((GeometryDrawing)d).Brush = new SolidColorBrush(Colors.White);
            _i.IconSource = clonedIcon;
            
            CollectionViewSource.GetDefaultView(ToolbarItems).Refresh();
        }

        private void ReMeasureToolBar() {
            var barWidth = CaculateToolBarWindowWidth(ToolbarItems);
            ToolBarV2Grid.Width = barWidth;
            Width = barWidth + 24 * 2; // 6是工具栏和窗口的Margin
            Height = 48 + 24 * 2; // 48是工具栏高度
            var offset = ToolBarNowVariantMode == 3 ? 0 : 6;
            var offsetCp = ToolBarNowVariantMode == 3 ? 0.5 : 6.5;
            var path = GenerateSuperEllipsePathByContentWidth(barWidth + offset, 48);
            ToolBarBackgroundBorder.Geometry = Geometry.Parse(path);
            var cg = GenerateSuperEllipsePathClipGeometry(barWidth + offsetCp, 48);
            BackgroundBorderDrawingGroup.ClipGeometry = Geometry.Parse(cg);

            Top = Math.Max(Math.Min(Top, 
                System.Windows.SystemParameters.PrimaryScreenHeight-ActualHeight +24),-24);
            Left = Math.Max(Math.Min(Left,
                System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth +24),-24);
        }

        public int ToolBarNowVariantMode = -1;

        private string GenerateSuperEllipsePathClipGeometry(double width, double renderingHeight) {
            double acutalHeight = 119.33;
            double renderingScalingFactor = acutalHeight / renderingHeight;
            double actualFullWidth = width * renderingScalingFactor;
            double actualCenterWidth = actualFullWidth - 119.34 < 0 ? 0 : actualFullWidth - 119.34;

            var cg = $"M0,0 V120 H{Math.Round(actualFullWidth, 0)} V0 H0 Z";
            return cg;
        }

        /// <summary>
        /// 根据给定的渲染高度计算比例，并根据实际内容宽度计算超椭圆圆角矩形的SVG路径
        /// </summary>
        /// <param name="width"></param>
        /// <param name="renderingHeight"></param>
        /// <returns></returns>
        private string GenerateSuperEllipsePathByContentWidth(double width, double renderingHeight) {
            double acutalHeight = 119.33;
            double renderingScalingFactor = acutalHeight / renderingHeight;
            double actualFullWidth = width * renderingScalingFactor;
            double actualCenterWidth = actualFullWidth - 119.34 < 0 ? 0 : actualFullWidth - 119.34;

            var sb = "M64.3285 "
                     + "0.015625"
                     + $"H{Math.Round(64.3395 + actualCenterWidth,3)}"
                     + $"C{Math.Round(90.3185 + actualCenterWidth,3)} "
                     + "0.015625 "
                     + $"{Math.Round(99.3081 + actualCenterWidth, 3)} "
                     + "0.015625 "
                     + $"{Math.Round(108.103 + actualCenterWidth, 3)} "
                     + "7.11388"
                     + $"C{Math.Round(109.871 + actualCenterWidth, 3)} "
                     + "8.54031 "
                     + $"{Math.Round(111.481 + actualCenterWidth, 3)} "
                     + "10.1509 "
                     + $"{Math.Round(112.908 + actualCenterWidth, 3)} "
                     + "11.9183"
                     + $"C{Math.Round(120.006 + actualCenterWidth, 3)} "
                     + "20.7134 "
                     + $"{Math.Round(120.006 + actualCenterWidth, 3)} "
                     + "33.7029 "
                     + $"{Math.Round(120.006 + actualCenterWidth, 3)} "
                     + "59.6819"
                     + $"C{Math.Round(120.006 + actualCenterWidth, 3)} "
                     + "85.661 "
                     + $"{Math.Round(120.006 + actualCenterWidth, 3)} "
                     + "98.6505 "
                     + $"{Math.Round(112.908 + actualCenterWidth, 3)} "
                     + "107.446"
                     + $"C{Math.Round(111.481 + actualCenterWidth, 3)} "
                     + "109.213 "
                     + $"{Math.Round(109.871 + actualCenterWidth, 3)} "
                     + "110.824 "
                     + $"{Math.Round(108.103 + actualCenterWidth, 3)} "
                     + "112.25"
                     + $"C{Math.Round(99.3081 + actualCenterWidth, 3)} "
                     + "119.348 "
                     + $"{Math.Round(90.3185 + actualCenterWidth, 3)} "
                     + "119.348 "
                     + $"{Math.Round(64.3395 + actualCenterWidth, 3)} "
                     + "119.348"
                     + "H64.3285C38.3494 "
                     + "119.348 "
                     + "21.3599 "
                     + "119.348 "
                     + "12.5648 "
                     + "112.25"
                     + "C10.7973 "
                     + "110.824 "
                     + "9.1868 "
                     + "109.213 "
                     + "7.76037 "
                     + "107.446"
                     + "C0.662109 "
                     + "98.6505 "
                     + "0.662109 "
                     + "85.661 "
                     + "0.662109 "
                     + "59.6819"
                     + "C0.662109 "
                     + "33.7029 "
                     + "0.662109 "
                     + "20.7134 "
                     + "7.76037 "
                     + "11.9183"
                     + "C9.1868 "
                     + "10.1509 "
                     + "10.7973 "
                     + "8.54031 "
                     + "12.5648 "
                     + "7.11388"
                     + "C21.3599 "
                     + "0.015625 "
                     + "38.3494 "
                     + "0.015625 "
                     + "64.3285 "
                     + "0.015625"
                     + "Z";

            return sb;
        }

        public enum ToolBarSnapType {
            NoSnap,
            LeftTopCorner,
            RightTopCorner,
            LeftBottomCorner,
            RightBottomCorner,
            RightSide,
            LeftSide,
            BottomSide,
            TopSide
        }

        public ToolBarSnapType SnapType {
            get {
                if (Top <= -24 && Left <= -24) return ToolBarSnapType.LeftTopCorner;
                if (Top >= System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 24 && Left <= -24) return ToolBarSnapType.LeftBottomCorner;
                if (Top <= -24 && Left >= System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24) return ToolBarSnapType.RightTopCorner;
                if (Top >= System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 24 && 
                    Left >= System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24) return ToolBarSnapType.RightBottomCorner;
                if (Top <= -24) return ToolBarSnapType.TopSide;
                if (Top >= System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 24)
                    return ToolBarSnapType.BottomSide;
                if (Left <= -24) return ToolBarSnapType.LeftSide;
                if (Left >= System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24)
                    return ToolBarSnapType.RightSide;
                return ToolBarSnapType.NoSnap;
            }
        }

        public event EventHandler<EventArgs> FloatingBarToolSelectionChanged;
        public event EventHandler<EventArgs> FloatingBarToolButtonClicked;

        private void OnToolSelectionChanged(FloatingBarItem sender) {
            if (ToolBarNowVariantMode == 3) return;
            Dispatcher.InvokeAsync(() => HideAllPopups());
            if (sender.Selected && sender.ToolType != MainWindow.ICCToolsEnum.CursorMode) {
                if (ToolBarNowVariantMode != 0) UpdateToolBarVariant(0);
            } else {
                if (ToolBarNowVariantMode != 1) UpdateToolBarVariant(1);
            }
            FloatingBarToolSelectionChanged?.Invoke(sender,EventArgs.Empty);
        }

        private void OnToolButtonClicked(FloatingBarItem sender, Grid container) {
            if (sender.ToolType == MainWindow.ICCToolsEnum.PenMode) {
                if (PenPaletteV2Popup.IsOpen) {
                    HideAllPopups();
                } else {
                    var containerPoint = container.TranslatePoint(new Point(0, 0), ToolBarV2Grid);
                    PenPaletteV2Popup.HorizontalOffset = containerPoint.X;
                    if (System.Windows.SystemParameters.PrimaryScreenHeight - Top - Height + 24 > PenPaletteV2.Height) PenPaletteV2Popup.VerticalOffset = Height -24 ;
                        else PenPaletteV2Popup.VerticalOffset = 0 - PenPaletteV2.Height + 24;
                    PenPaletteV2Popup.IsOpen = true;
                }
            } else if (sender.ToolType == MainWindow.ICCToolsEnum.LassoMode) {
                if (SelectionPopupV2.IsOpen) {
                    HideAllPopups();
                } else {
                    var containerPoint = container.TranslatePoint(new Point(0, 0), ToolBarV2Grid);
                    SelectionPopupV2.HorizontalOffset = containerPoint.X;
                    if (System.Windows.SystemParameters.PrimaryScreenHeight - Top - Height + 24 > SelectionV2.Height) SelectionPopupV2.VerticalOffset = Height - 24 ;
                        else SelectionPopupV2.VerticalOffset = 0 - SelectionV2.Height + 24;
                    SelectionPopupV2.IsOpen = true;
                }
            } else if (sender.Name == "ShapeDrawing") {
                if (ShapeDrawingPopupV2.IsOpen) {
                    HideAllPopups();
                } else {
                    var containerPoint = container.TranslatePoint(new Point(0, 0), ToolBarV2Grid);
                    ShapeDrawingPopupV2.HorizontalOffset = containerPoint.X;
                    if (System.Windows.SystemParameters.PrimaryScreenHeight - Top - Height + 24 > ShapeDrawingV2.Height) ShapeDrawingPopupV2.VerticalOffset = Height - 24 ;
                    else ShapeDrawingPopupV2.VerticalOffset = 0 - ShapeDrawingV2.Height + 24;
                    ShapeDrawingPopupV2.IsOpen = true;
                }
            }
            FloatingBarToolButtonClicked?.Invoke(sender,EventArgs.Empty);
        }

        private void ToolbarButton_MouseDown(object sender, MouseButtonEventArgs e) {
            if (_mouseDownButton != null) return;
            var gd = sender as Grid;
            var itemData = gd.Tag as FloatingBarItem;
            _mouseDownButton = gd;
            if (!itemData.Selected) {
                var bgImg = gd.Children[1] as Image;
                bgImg.Opacity = 1;
            }
            gd.RenderTransform = new ScaleTransform(0.9, 0.9);
        }

        private void ToolbarButton_MouseUp(object sender, MouseButtonEventArgs e) {
            if (_mouseDownButton == null || _mouseDownButton != sender) return;
            ToolbarButton_MouseLeave(sender, null);

            var gd = sender as Grid;
            var itemData = gd.Tag as FloatingBarItem;
            if (itemData.Type == FloatingBarItemType.StateButton && !itemData.Selected) {
                UpdateToolBarSelectedTool(itemData.ToolType);
                OnToolSelectionChanged(itemData);
            } else {
                OnToolButtonClicked(itemData, gd);
            }
        }

        public int DynamicTransparentVariant { get; set; } = 1;

        /// <summary>
        /// 根据提供的变体ID修改工具栏动态透明的变体类型
        /// </summary>
        /// <param name="variant">0为透明，1为不透明</param>
        private void UpdateToolBarDynamicOpacityVariant(int variant) {
            DynamicTransparentVariant = variant;
            if (variant == 0) {
                FallbackBackgroundLayer.Opacity = 0.05;
                ToolBarBackgroundBorder.Pen = new Pen() {
                    Brush = new SolidColorBrush(Color.FromArgb(48, 34, 34, 34)),
                    Thickness = 3,
                };
                ToolBarBackgroundBorder.Brush = new SolidColorBrush(Color.FromArgb(16, 255,255,255));
                HeadIcon.Opacity = 0.5;
                ToolBarItemsControl.Opacity = 0.92;
                foreach (var fi in ToolbarItems) {
                    fi.IsSemiTransparent = true;
                    if (fi.Type == FloatingBarItemType.Separator) continue;
                    if (fi.IconSourceResourceKey == "") continue;
                    var icon = FindResource(fi.IconSourceResourceKey) as DrawingImage;
                    foreach (var gd in (icon.Drawing as DrawingGroup).Children) {
                        var _gd = gd as GeometryDrawing;
                        _gd.Pen = new Pen() {
                            Brush = new SolidColorBrush(Color.FromArgb(148, 34,34,34)),
                            Thickness = 1,
                        };
                        if (fi.IconColor != null)
                            _gd.Brush = new SolidColorBrush(Color.FromArgb(96, (byte)Math.Min(((Color)fi.IconColor).R * 4.25,255),
                            (byte)Math.Min(((Color)fi.IconColor).G * 4.25,255),(byte)Math.Min(((Color)fi.IconColor).B * 4.25,255)));
                        else _gd.Brush = new SolidColorBrush(Color.FromArgb(96, 255,255,255));
                    }

                    if (fi.Selected) {
                        var clonedIcon = icon.Clone();
                        foreach (var d in (clonedIcon.Drawing as DrawingGroup).Children) 
                            ((GeometryDrawing)d).Brush = new SolidColorBrush(Colors.White);
                        fi.IconSource = clonedIcon;
                    }
                }
            } else if (variant == 1) {
                FallbackBackgroundLayer.Opacity = 1;
                ToolBarBackgroundBorder.Pen = new Pen() {
                    Brush = new SolidColorBrush(Color.FromRgb(212,212,216)),
                    Thickness = 3,
                };
                ToolBarBackgroundBorder.Brush = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                HeadIcon.Opacity = 1;
                ToolBarItemsControl.Opacity = 1;
                foreach (var fi in ToolbarItems) {
                    fi.IsSemiTransparent = false;
                    if (fi.Type == FloatingBarItemType.Separator) continue;
                    if (fi.IconSourceResourceKey == "") continue;
                    var icon = FindResource(fi.IconSourceResourceKey) as DrawingImage;
                    foreach (var gd in (icon.Drawing as DrawingGroup).Children) {
                        var _gd = gd as GeometryDrawing;
                        _gd.Pen = null;
                        _gd.Brush = new SolidColorBrush(fi.IconColor??Color.FromRgb(34,34,34));
                    }

                    if (fi.Selected) {
                        var clonedIcon = icon.Clone();
                        foreach (var d in (clonedIcon.Drawing as DrawingGroup).Children) 
                            ((GeometryDrawing)d).Brush = new SolidColorBrush(Colors.White);
                        fi.IconSource = clonedIcon;
                    }
                }
            }

            CollectionViewSource.GetDefaultView(ToolbarItems).Refresh();
            ReMeasureToolBar();
        }

        [DllImport("USER32.DLL", SetLastError = true)]
        public static extern int GetSystemMetrics(int nIndex);

        private void ToolbarButton_MouseLeave(object sender, MouseEventArgs e) {
            if (_mouseDownButton == null || _mouseDownButton != sender) return;
            var itemData = _mouseDownButton.Tag as FloatingBarItem;
            if (!itemData.Selected) {
                var bgImg = _mouseDownButton.Children[1] as Image;
                bgImg.Opacity = 0;
            }
            _mouseDownButton.RenderTransform = null;
            _mouseDownButton = null;
        }

        #region icc按钮

        public Point prevPoint = new Point(0,0);
        public bool isInMovingMode = false;
        public double winLeft = 0;
        public double winTop = 0;
        public ToolBarSnapType _snapTypeTemp = ToolBarSnapType.NoSnap;

        private void HeadIconButton_MouseDown(object sender, MouseButtonEventArgs e) {
            if (_isMouseDownHeadIcon) return;
            if (e.RightButton == MouseButtonState.Pressed) {
                UpdateToolBarDynamicOpacityVariant(Math.Abs(DynamicTransparentVariant-1));
            } else {
                _isMouseDownHeadIcon = true;
                var gd = sender as Grid;
                prevPoint = e.GetPosition(ToolBarV2Grid);
                winLeft = Left;
                winTop = Top;
                Trace.WriteLine(prevPoint);
                gd.CaptureMouse();
                gd.RenderTransform = new ScaleTransform(0.92, 0.92);
                isInMovingMode = false;
                _snapTypeTemp = SnapType;
            }
            
        }

        private void HeadIconButton_MouseMove(object sender, MouseEventArgs e) {
            if (_isMouseDownHeadIcon == false) return;
            var mp = System.Windows.Forms.Control.MousePosition;
            var mpLogical = new Point(mp.X * ScalingFactor, mp.Y * ScalingFactor);
            var deltaX = mpLogical.X - prevPoint.X - winLeft - 24;
            var deltaY = mpLogical.Y - prevPoint.Y - winTop - 24;
            var movingLimitation = _snapTypeTemp == ToolBarSnapType.NoSnap ? 12 : 24;
            if (Math.Abs(deltaY) > movingLimitation || Math.Abs(deltaX) > movingLimitation) isInMovingMode = true;
            if (isInMovingMode) {
                HideAllPopups();
                ToolbarV2.RenderTransform = null;
                ToolBarV2Grid.RenderTransform = null;
                HeadIconImage.RenderTransform = null;
                UpdateToolbarPlacementFeedback();
                Top = Math.Max(Math.Min(mp.Y * ScalingFactor - prevPoint.Y - 24, 
                    System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight +24),-24);
                Left = Math.Max(Math.Min(mp.X * ScalingFactor - prevPoint.X - 24,
                    System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth +24),-24);
            } else {
                double tbMovingX = deltaX/3, tbMovingY = deltaY/3;
                tbMovingX = (new int[] { 1, 3, 6 }).Contains((int)_snapTypeTemp)?Math.Max(deltaX / 3, 0):tbMovingX;
                tbMovingX = (new int[] { 2, 4, 5 }).Contains((int)_snapTypeTemp)?Math.Min(deltaX / 3, 0):tbMovingX;
                tbMovingY = (new int[] { 1, 2, 8 }).Contains((int)_snapTypeTemp)?Math.Max(deltaY / 3, 0):tbMovingY;
                tbMovingY = (new int[] { 3, 4, 7 }).Contains((int)_snapTypeTemp)?Math.Min(deltaY / 3, 0):tbMovingY;
                ToolBarV2Grid.RenderTransformOrigin = new Point(
                    (new int[] { 1, 3, 6 }).Contains((int)_snapTypeTemp) ? 0 :
                    (new int[] { 7, 8 }).Contains((int)_snapTypeTemp) ? 0.5 : 1,
                    (new int[] { 1, 2, 8 }).Contains((int)_snapTypeTemp) ? 0 :
                    (new int[] { 5, 6 }).Contains((int)_snapTypeTemp) ? 0.5 : 1
                );
                ToolBarV2Grid.RenderTransform = new ScaleTransform(
                    1 + (tbMovingX /350)*((new int[] { 1, 3, 6 }).Contains((int)_snapTypeTemp)?1:(new int[] { 2, 4, 5 }).Contains((int)_snapTypeTemp)?-1:0),
                    1 + (tbMovingY /120)*((new int[] { 1, 2, 8 }).Contains((int)_snapTypeTemp)?1:(new int[] { 3, 4, 7 }).Contains((int)_snapTypeTemp)?-1:0));
                ToolbarV2.RenderTransform = new TranslateTransform(tbMovingX, tbMovingY);
                HeadIconImage.RenderTransform = new TranslateTransform(tbMovingX / 2, tbMovingY / 2);
            }
        }
        
        #endregion

        private CornerRadius _ltCornerRadius = new CornerRadius(0, 0, 4, 0);
        private CornerRadius _lbCornerRadius = new CornerRadius(0, 4, 0, 0);
        private CornerRadius _rtCornerRadius = new CornerRadius(0, 0, 0, 4);
        private CornerRadius _rbCornerRadius = new CornerRadius(4, 0, 0, 0);
        private CornerRadius _tsCornerRadius = new CornerRadius(0, 0, 4, 4);
        private CornerRadius _bsCornerRadius = new CornerRadius(4, 4, 0, 0);

        private void UpdateToolbarPlacementFeedback() {
            var snapT = SnapType;
            var xSideCenter = (System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
            var xDeltaCenter = xSideCenter - Left;
            PlaceFeedbackBorder.Width = ActualWidth - 16;
            PlaceFeedbackBorder.Height = ActualHeight - 16;
            PlacementFeedbackPopup.IsOpen = true;
            if (Math.Abs(24 + Top) <= 24 && Math.Abs(24 + Left) <= 24) {
                PlacementFeedbackPopup.VerticalOffset = 0;
                PlacementFeedbackPopup.HorizontalOffset = 0;
                PlaceFeedbackBorder.CornerRadius = _ltCornerRadius;
                PlacementFeedbackTipText.VerticalAlignment = VerticalAlignment.Bottom;
                PlacementFeedbackTipText.HorizontalAlignment = HorizontalAlignment.Left;
                PlacementFeedbackTipText.Text = "左上角";
            } else if (Math.Abs(24 + Left) <= 24 && 24 - (Top + ActualHeight - System.Windows.SystemParameters.PrimaryScreenHeight) <= 24) {
                PlacementFeedbackPopup.VerticalOffset = System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 16;
                PlacementFeedbackPopup.HorizontalOffset = 0;
                PlaceFeedbackBorder.CornerRadius = _lbCornerRadius;
                PlacementFeedbackTipText.VerticalAlignment = VerticalAlignment.Top;
                PlacementFeedbackTipText.HorizontalAlignment = HorizontalAlignment.Left;
                PlacementFeedbackTipText.Text = "左下角";
            } else if (24 - (Left + ActualWidth - System.Windows.SystemParameters.PrimaryScreenWidth) <= 24 && Math.Abs(24 + Top) <= 24) {
                PlacementFeedbackPopup.VerticalOffset = 0;
                PlacementFeedbackPopup.HorizontalOffset = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 16;
                PlaceFeedbackBorder.CornerRadius = _rtCornerRadius;
                PlacementFeedbackTipText.VerticalAlignment = VerticalAlignment.Bottom;
                PlacementFeedbackTipText.HorizontalAlignment = HorizontalAlignment.Right;
                PlacementFeedbackTipText.Text = "右上角";
            } else if (24 - (Left + ActualWidth - System.Windows.SystemParameters.PrimaryScreenWidth) <= 24 &&
                       24 - (Top + ActualHeight - System.Windows.SystemParameters.PrimaryScreenHeight) <= 24) {
                PlacementFeedbackPopup.VerticalOffset = System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 16;
                PlacementFeedbackPopup.HorizontalOffset = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 16;
                PlaceFeedbackBorder.CornerRadius = _rbCornerRadius;
                PlacementFeedbackTipText.VerticalAlignment = VerticalAlignment.Top;
                PlacementFeedbackTipText.HorizontalAlignment = HorizontalAlignment.Right;
                PlacementFeedbackTipText.Text = "右下角";
            } else if (snapT == ToolBarSnapType.TopSide && Math.Abs(xDeltaCenter)<=50) {
                PlacementFeedbackPopup.VerticalOffset = 0;
                PlacementFeedbackPopup.HorizontalOffset =
                    (System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 16) / 2;
                PlaceFeedbackBorder.CornerRadius = _tsCornerRadius;
                PlacementFeedbackTipText.VerticalAlignment = VerticalAlignment.Bottom;
                PlacementFeedbackTipText.HorizontalAlignment = HorizontalAlignment.Center;
                PlacementFeedbackTipText.Text = "顶部居中";
            } else if (snapT == ToolBarSnapType.BottomSide && Math.Abs(xDeltaCenter)<=90) {
                PlacementFeedbackPopup.VerticalOffset = System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 16;
                PlacementFeedbackPopup.HorizontalOffset =
                    (System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 16) / 2;
                PlaceFeedbackBorder.CornerRadius = _bsCornerRadius;
                PlacementFeedbackTipText.VerticalAlignment = VerticalAlignment.Top;
                PlacementFeedbackTipText.HorizontalAlignment = HorizontalAlignment.Center;
                PlacementFeedbackTipText.Text = "底部居中";
            } else {
                PlacementFeedbackPopup.IsOpen = false;
            }
        }

        public FloatingBarItem SelectedItem {
            get => ToolbarItems.Single(item => item.Selected);
        }

        private void UseNearSnap() {
            if (Math.Abs(24 + Top) <= 24 && Math.Abs(24 + Left) <= 24) {
                Top = -24;
                Left = -24;
            } else if (
                24 - (Left + ActualWidth - System.Windows.SystemParameters.PrimaryScreenWidth) <= 24 &&
                Math.Abs(24 + Top) <= 24
            ) {
                Top = -24;
                Left = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24;
            } else if (
                Math.Abs(24 + Left) <= 24 &&
                24 - (Top + ActualHeight - System.Windows.SystemParameters.PrimaryScreenHeight) <= 24
            ) {
                Top = System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 24;
                Left = -24;
            } else if (
                24 - (Left + ActualWidth - System.Windows.SystemParameters.PrimaryScreenWidth) <= 24 &&
                24 - (Top + ActualHeight - System.Windows.SystemParameters.PrimaryScreenHeight) <= 24
            ) {
                Top = System.Windows.SystemParameters.PrimaryScreenHeight - ActualHeight + 24;
                Left = System.Windows.SystemParameters.PrimaryScreenWidth - ActualWidth + 24;
            }
        }

        private void HeadIconButton_MouseUp(object sender, MouseButtonEventArgs e) {
            if (_isMouseDownHeadIcon == false) return;
            _isMouseDownHeadIcon = false;
            var gd = sender as Grid;
            gd.ReleaseMouseCapture();
            gd.RenderTransform = null;
            ToolbarV2.RenderTransform = null;
            ToolBarV2Grid.RenderTransform = null;
            HeadIconImage.RenderTransform = null;
            PlacementFeedbackPopup.IsOpen = false;
            UseNearSnap();
            if (!isInMovingMode) {
                var mp = System.Windows.Forms.Control.MousePosition;
                var mpLogical = new Point(mp.X * ScalingFactor, mp.Y * ScalingFactor);
                if (Math.Abs(mpLogical.X - prevPoint.X - winLeft - 24) < 2.5 || Math.Abs(mpLogical.Y - prevPoint.Y - winTop - 24) < 2.5) {
                    if (ToolBarNowVariantMode == 3) {
                        if (SelectedItem.ToolType != MainWindow.ICCToolsEnum.CursorMode)
                            UpdateToolBarVariant(0);
                        else UpdateToolBarVariant(1);
                    } else {
                        UpdateToolBarVariant(3);
                    }
                }
            }
            isInMovingMode = false;
        }
    }
}
