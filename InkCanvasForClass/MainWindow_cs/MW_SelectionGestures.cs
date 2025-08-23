using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Resources;
using Ink_Canvas.Popups;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Application = System.Windows.Application;
using Matrix = System.Windows.Media.Matrix;
using Point = System.Windows.Point;
using Ink_Canvas.Helpers;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        #region Floating Control

        private object lastBorderMouseDownObject;

        private void Border_MouseDown(object sender, MouseButtonEventArgs e) {
            lastBorderMouseDownObject = sender;
        }

        private bool isStrokeSelectionCloneOn = false;

        #region StrokesSelectionToolbarButtons

        private Border BorderStrokeSelectionToolButtonMouseDown = null;

        private void BorderStrokeSelectionToolButton_MouseDown(object sender, MouseButtonEventArgs e) {
            var bd = (Border)sender;
            BorderStrokeSelectionToolButtonMouseDown = bd;
            var innerBd = (Border)bd.Child;
            if (bd.Name != "BorderStrokeSelectionDelete") innerBd.Background = new SolidColorBrush(Color.FromArgb(24, 9, 9, 11));
            else innerBd.Background = new SolidColorBrush(Color.FromRgb(254, 202, 202));
        }

        private void BorderStrokeSelectionToolButton_MouseLeave(object sender, MouseEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown == null) return;

            var innerBd = (Border)BorderStrokeSelectionToolButtonMouseDown.Child;
            innerBd.Background = new SolidColorBrush(Colors.Transparent);
            BorderStrokeSelectionToolButtonMouseDown = null;
        }

        private void UpdateStrokesSelectionCloneToolButtonLimitStatus() {
            UpdateSelectionToolbarOtherIconLockedStatus(isStrokeSelectionCloneOn);
            BorderStrokeSelectionLock.IsHitTestVisible = !isStrokeSelectionCloneOn;
            BorderStrokeSelectionLock.Opacity = isStrokeSelectionCloneOn ? 0.5 : 1;
            BorderStrokeSelectionLock.IsEnabled = !isStrokeSelectionCloneOn;
            BorderStrokeSelectionClone.IsHitTestVisible = true;
            BorderStrokeSelectionClone.Opacity = 1;
            BorderStrokeSelectionClone.IsEnabled = true;
        }

        /// <summary>
        /// 墨迹克隆按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BorderStrokeSelectionClone_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            var bd = (Border)sender;
            var innerBd = (Border)bd.Child;

            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);

            if (isStrokeSelectionCloneOn) {
                isStrokeSelectionCloneOn = false;
                innerBd.Background = new SolidColorBrush(Colors.Transparent);
                BorderStrokeSelectionCloneGeometryIcon.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
            } else {
                isStrokeSelectionCloneOn = true;
                innerBd.Background = new SolidColorBrush(Color.FromRgb(187, 247, 208));
                BorderStrokeSelectionCloneGeometryIcon.Brush = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            }

            UpdateStrokesSelectionCloneToolButtonLimitStatus();
        }

        /// <summary>
        /// 复制到白板或复制到新页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BorderStrokeSelectionCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);

            var strokes = inkCanvas.GetSelectedStrokes();
            inkCanvas.Select(new StrokeCollection());
            CancelCurrentStrokesSelection();
            strokes = strokes.Clone();
            if (currentMode == 0) ImageBlackboard_MouseUp(null, null);
            else BtnWhiteBoardAdd_Click(null, null);
            inkCanvas.Strokes.Add(strokes);
        }

        /// <summary>
        /// 删除墨迹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BorderStrokeSelectionDelete_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;
            SymbolIconDelete_MouseUp(sender, e);

            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);
        }


        /// <summary>
        /// 墨迹的 更多 菜单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BorderStrokeMoreMenuButton_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            var cm = this.FindResource("StrokesSelectionMoreMenuButtonContextMenu") as ContextMenu;
            cm.IsOpen = true;

            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);
        }

        /// <summary>
        /// 通用的墨迹矩阵操作
        /// </summary>
        /// <param name="func"></param>
        private void MatrixStrokes(Func<Matrix,Point,Matrix> func) {
            var m = new Matrix();

            Point center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
            center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

            m = func(m,center);

            var targetStrokes = inkCanvas.GetSelectedStrokes();
            foreach (var stroke in targetStrokes) stroke.Transform(m, false);

            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
        }

        private void __tb_preview__(object sender, MouseButtonEventArgs e) {
            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);

            // update preview and border
            InkSelectionStrokesOverlay.DrawStrokes(inkCanvas.GetSelectedStrokes(), new Matrix());
            UpdateStrokeSelectionBorder(true, inkCanvas.GetSelectionBounds());
            updateBorderStrokeSelectionControlLocation();
        }

        /// <summary>
        /// 旋转墨迹按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageRotate_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            MatrixStrokes((m, cent) => {
                m.RotateAt((((Border)sender).Name=="BorderImageRotate45"?45:90) * (StrokesRotateClockwise == 0 ? 1 : -1), cent.X, cent.Y);
                return m;
            });

            __tb_preview__(sender, e);
        }

        /// <summary>
        /// 翻转墨迹按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageFlip_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            MatrixStrokes((m, cent) => {
                m.ScaleAt((((Border)sender).Name=="BorderImageFlipHorizontal"?-1:1),(((Border)sender).Name=="BorderImageFlipVertical"?-1:1),cent.X,cent.Y);
                return m;
            });

            __tb_preview__(sender, e);
        }

        #endregion

        private void GridPenWidthDecrease_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            ChangeStrokeThickness(0.8);
        }

        private void GridPenWidthIncrease_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            ChangeStrokeThickness(1.25);
        }

        private void ChangeStrokeThickness(double multipler) {
            foreach (var stroke in inkCanvas.GetSelectedStrokes()) {
                var newWidth = stroke.DrawingAttributes.Width * multipler;
                var newHeight = stroke.DrawingAttributes.Height * multipler;
                if (!(newWidth >= DrawingAttributes.MinWidth) || !(newWidth <= DrawingAttributes.MaxWidth)
                                                              || !(newHeight >= DrawingAttributes.MinHeight) ||
                                                              !(newHeight <= DrawingAttributes.MaxHeight)) continue;
                stroke.DrawingAttributes.Width = newWidth;
                stroke.DrawingAttributes.Height = newHeight;
            }
            if (DrawingAttributesHistory.Count > 0)
            {

                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
        }

        private void GridPenWidthRestore_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            foreach (var stroke in inkCanvas.GetSelectedStrokes()) {
                stroke.DrawingAttributes.Width = inkCanvas.DefaultDrawingAttributes.Width;
                stroke.DrawingAttributes.Height = inkCanvas.DefaultDrawingAttributes.Height;
            }
        }

        private enum StrokesRotateClockwiseEnum {
            RotateClockwise,
            RotateCounterClockwise,
        }
        private StrokesRotateClockwiseEnum StrokesRotateClockwise = StrokesRotateClockwiseEnum.RotateClockwise;

        private void ImageRotateClockwise_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            StrokesRotateClockwise = (StrokesRotateClockwiseEnum)(StrokesRotateClockwise == 0 ? 1 : 0);

            BorderImageRotateClockwise.RenderTransform = new ScaleTransform(StrokesRotateClockwise == 0 ? 1 : -1, 1);

            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);
        }

        #endregion

        private bool isGridInkCanvasSelectionCoverMouseDown = false;
        private StrokeCollection StrokesSelectionClone = new StrokeCollection();

        private bool isRectangleSelectionMouseDown = false;
        // 矩形框选
        private Point rectangleSelection_FirstPoint = new Point(0, 0);
        private Point rectangleSelection_LastPoint = new Point(0, 0);
        // 套索框选
        private PointCollection rectangleSelection_LassoPoints = new PointCollection();

        private void GridInkCanvasSelectionCover_MouseDown(object sender, MouseButtonEventArgs e) {
            isGridInkCanvasSelectionCoverMouseDown = true;
        }

        private void GridInkCanvasSelectionCover_MouseUp(object sender, MouseButtonEventArgs e) {
            isGridInkCanvasSelectionCoverMouseDown = false;
            CancelCurrentStrokesSelection();
        }

        private void InkCanvasDeleteCommandFiredEvent(object sender, RoutedEventArgs e) {
            SymbolIconDelete_MouseUp(null, null);
        }

        private void RectangleSelectionHitTestBorder_MouseDown(object sender, MouseButtonEventArgs e) {
            var pt = e.GetPosition(Main_Grid);
            var nt = inkCanvas.Strokes.HitTest(pt, 8);
            if (nt.Count > 0 && !(e.RightButton == MouseButtonState.Pressed)) {
                if (nt.Count > 1) {
                    var nodia = nt.HitTest(pt);
                    if (nodia.Count > 0) {
                        if (nodia[nodia.Count - 1].ContainsPropertyData(IsLockGuid) && Settings.Canvas.AllowClickToSelectLockedStroke) return;
                        inkCanvas.Select(new StrokeCollection() {nodia[nodia.Count-1]});
                    } else {
                        if (nt[0].ContainsPropertyData(IsLockGuid) && Settings.Canvas.AllowClickToSelectLockedStroke) return;
                        inkCanvas.Select(new StrokeCollection() { nt[0] });
                    }
                } else if (nt.Count == 1) {
                    if (nt[0].ContainsPropertyData(IsLockGuid) && Settings.Canvas.AllowClickToSelectLockedStroke) return;
                    inkCanvas.Select(nt);
                }
            } else {
                RectangleSelectionHitTestBorder.CaptureMouse();
                isRectangleSelectionMouseDown = true;
                if (Settings.Canvas.SelectionMethod == 1) {
                    rectangleSelection_FirstPoint = pt;
                } else {
                    rectangleSelection_LassoPoints.Clear();
                    rectangleSelection_LassoPoints.Add(pt);
                }
                
            }
        }

        private void RectangleSelectionHitTestBorder_MouseMove(object sender, MouseEventArgs e) {
            var pt = e.GetPosition(Main_Grid);
            if (!isRectangleSelectionMouseDown) return;
            if (Settings.Canvas.SelectionMethod == 1) {
                rectangleSelection_LastPoint = pt;
                RectangleSelection.DrawSelectionBox(new Rect(rectangleSelection_FirstPoint, rectangleSelection_LastPoint));
            } else {
                rectangleSelection_LassoPoints.Add(pt);
                RectangleSelection.DrawLassoLine(rectangleSelection_LassoPoints);
            }
        }

        private void RectangleSelectionHitTestBorder_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isRectangleSelectionMouseDown) return;
            RectangleSelectionHitTestBorder.ReleaseMouseCapture();
            isRectangleSelectionMouseDown = false;
            var pt = e.GetPosition(Main_Grid);

            var ilh = inkCanvas.Strokes.GetIncrementalLassoHitTester(Settings.Canvas.OnlyHitTestFullyContainedStrokes ? 100 : 1);

            void func(object s, LassoSelectionChangedEventArgs _e) {
                var _ilh = s as IncrementalLassoHitTester;
                if (_e.SelectedStrokes.Count==0) UpdateStrokeSelectionBorder(false, null);
                inkCanvas.Select(_e.SelectedStrokes);
                _ilh.EndHitTesting();
            }

            ilh.SelectionChanged += func;

            if (Settings.Canvas.SelectionMethod == 1) {
                rectangleSelection_LastPoint = pt;
                var rct = new Rect(rectangleSelection_FirstPoint, rectangleSelection_LastPoint);
                ilh.AddPoints(new Point[] {
                    rct.TopLeft,
                    rct.TopRight,
                    rct.BottomRight,
                    rct.BottomLeft,
                    rct.TopLeft
                });
            } else {
                rectangleSelection_LassoPoints.Add(pt);
                ilh.AddPoints(rectangleSelection_LassoPoints);
            }

            RectangleSelection.ClearDrawing();
        }


        private Border[] StrokeSelectionBorderHandles = new Border[] { };

        #region StrokeSelectionBorder

        private bool isLockedStrokeSelectionHandle = false;
        private Border lockedStrokeSelectionHandle = null;
        private StrokeSelectionBorderHandlesEnum? lockedStrokeSelectionBorderHandleType = null;

        private Rect? originalSelectionBounds = null;
        private Rect? resizedSelectionBounds = null;

        private Point? resizingFirstPoint = null;
        private Point? resizingLastPoint = null;

        private void StrokeSelectionBorderHandle_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionMove || isLockedStrokeSelectionRotate) return;
            // lock
            isLockedStrokeSelectionHandle = true;
            var bd = (Border)sender;
            lockedStrokeSelectionHandle = bd;
            if (bd.Name != "StrokeSelectionRotateHandle") {
                var index = StrokeSelectionBorderGrid.Children.IndexOf((Border)sender) - 3;
                lockedStrokeSelectionBorderHandleType = (StrokeSelectionBorderHandlesEnum)index;
            } else {
                lockedStrokeSelectionBorderHandleType = StrokeSelectionBorderHandlesEnum.Rotate;
            }

            // capture
            // TODO 这里还需要对触摸屏幕的单个TouchDevice进行Capture
            bd.CaptureMouse();

            // hide selectionToolBar
            BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;

            // resize toast
            var sb = inkCanvas.GetSelectionBounds();
            if (!(sb.Width < 96 && sb.Height < 64)) {
                StrokeSelectionSizeToast.Visibility = Visibility.Visible;
                ((TextBlock)StrokeSelectionSizeToastInner.Children[0]).Text = Math.Round(sb.Width, 2).ToString();
                ((TextBlock)StrokeSelectionSizeToastInner.Children[2]).Text = Math.Round(sb.Height, 2).ToString();
                var lti = (int)lockedStrokeSelectionBorderHandleType;
                System.Windows.Controls.Canvas.SetLeft(StrokeSelectionSizeToast, double.NaN);
                System.Windows.Controls.Canvas.SetRight(StrokeSelectionSizeToast, double.NaN);
                System.Windows.Controls.Canvas.SetTop(StrokeSelectionSizeToast, double.NaN);
                System.Windows.Controls.Canvas.SetBottom(StrokeSelectionSizeToast, double.NaN);
                if (lti == 0 || lti == 2 || lti == 4 || lti == 7) System.Windows.Controls.Canvas.SetLeft(StrokeSelectionSizeToast,1);
                    else System.Windows.Controls.Canvas.SetRight(StrokeSelectionSizeToast, 1);
                if (lti == 0 || lti == 1 || lti == 4 || lti == 6) System.Windows.Controls.Canvas.SetTop(StrokeSelectionSizeToast, 1);
                    else System.Windows.Controls.Canvas.SetBottom(StrokeSelectionSizeToast, 1);
            } else {
                StrokeSelectionSizeToast.Visibility = Visibility.Collapsed;
            }

            // record first pt
            var nowWindowPosition = e.GetPosition(Main_Grid);
            resizingFirstPoint = nowWindowPosition;

            // record original bounds
            var transform = StrokeSelectionBorder.TransformToVisual(Main_Grid);
            var ori_lt = transform.Transform(new Point(0, 0));
            var ori_rb = transform.Transform(new Point(StrokeSelectionBorder.Width, StrokeSelectionBorder.Height));
            originalSelectionBounds = new Rect(ori_lt, ori_rb);
        }

        private void StrokeSelectionBorderHandle_MouseUp(object sender, MouseButtonEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (!isLockedStrokeSelectionHandle || isLockedStrokeSelectionMove || isLockedStrokeSelectionRotate) return;

            // resize strokes and preview strokes
            SelectedStrokesResize(inkCanvas.GetSelectedStrokes(), (StrokeSelectionBorderHandlesEnum)lockedStrokeSelectionBorderHandleType,
                (Rect)originalSelectionBounds, (Rect)resizedSelectionBounds);
            InkSelectionStrokesOverlay.DrawStrokes(inkCanvas.GetSelectedStrokes(), new Matrix());

            // display selectionToolBar
            var bd = new Binding("Visibility");
            bd.Source = GridInkCanvasSelectionCover;
            BorderStrokeSelectionControl.SetBinding(Border.VisibilityProperty, bd);

            // unlock
            isLockedStrokeSelectionHandle = false;
            lockedStrokeSelectionBorderHandleType = null;

            // release capture
            // TODO 这里还需要对触摸屏幕的单个TouchDevice进行ReleaseCapture
            lockedStrokeSelectionHandle.ReleaseMouseCapture();

            // unlock
            lockedStrokeSelectionHandle = null;

            // resize toast
            StrokeSelectionSizeToast.Visibility = Visibility.Collapsed;

            // update selection border size
            UpdateStrokeSelectionBorder(true, inkCanvas.GetSelectionBounds());
            updateBorderStrokeSelectionControlLocation();
        }

        private void StrokeSelectionBorderHandle_MouseMove(object sender, MouseEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (!isLockedStrokeSelectionHandle || isLockedStrokeSelectionMove || isLockedStrokeSelectionRotate) return;

            var bd = (Border)sender;
            var nowWindowPosition = e.GetPosition(Main_Grid);

            // record last pt
            var rlp = nowWindowPosition;

            // resize preview border
            var isReverseWidth = false; // 是否逆向 resize Width,true 為調小，false為調大
            var isReverseHeight = false; // 是否逆向 resize Height,true 為調小，false為調大
            if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.LeftTop) {
                isReverseWidth = ((Point)rlp).X > ((Point)resizingFirstPoint).X;
                isReverseHeight = ((Point)rlp).Y > ((Point)resizingFirstPoint).Y;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.RightTop) {
                isReverseWidth = ((Point)rlp).X < ((Point)resizingFirstPoint).X;
                isReverseHeight = ((Point)rlp).Y > ((Point)resizingFirstPoint).Y;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.LeftBottom) {
                isReverseWidth = ((Point)rlp).X > ((Point)resizingFirstPoint).X;
                isReverseHeight = ((Point)rlp).Y < ((Point)resizingFirstPoint).Y;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.RightBottom) {
                isReverseWidth = ((Point)rlp).X < ((Point)resizingFirstPoint).X;
                isReverseHeight = ((Point)rlp).Y < ((Point)resizingFirstPoint).Y;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.Top) {
                isReverseWidth = false;
                isReverseHeight = ((Point)rlp).Y > ((Point)resizingFirstPoint).Y;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.Bottom) {
                isReverseWidth = false;
                isReverseHeight = ((Point)rlp).Y < ((Point)resizingFirstPoint).Y;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.Left) {
                isReverseWidth = ((Point)rlp).X > ((Point)resizingFirstPoint).X;
                isReverseHeight = false;
            } else if (lockedStrokeSelectionBorderHandleType == StrokeSelectionBorderHandlesEnum.Right) {
                isReverseWidth = ((Point)rlp).X < ((Point)resizingFirstPoint).X;
                isReverseHeight = false;
            }

            // caculate height width left top
            var l = Math.Round(
                ((Rect)originalSelectionBounds).Left +
                ((int)lockedStrokeSelectionBorderHandleType == 0 || (int)lockedStrokeSelectionBorderHandleType == 2 || (int)lockedStrokeSelectionBorderHandleType == 4
                    ? new Rect(((Point)resizingFirstPoint), ((Point)rlp)).Width *
                      (isReverseWidth ? 1 : -1)
                    : 0), 0);
            var t = Math.Round(
                ((Rect)originalSelectionBounds).Top + 
                ((int)lockedStrokeSelectionBorderHandleType == 0 || (int)lockedStrokeSelectionBorderHandleType == 1 || (int)lockedStrokeSelectionBorderHandleType == 6
                    ? new Rect(((Point)resizingFirstPoint), ((Point)rlp)).Height * 
                      (isReverseHeight ? 1 : -1)
                    : 0), 0);
            var w = (int)lockedStrokeSelectionBorderHandleType != 6 && (int)lockedStrokeSelectionBorderHandleType != 7 ? Math.Round(((Rect)originalSelectionBounds).Width + new Rect(((Point)resizingFirstPoint),
                ((Point)rlp)).Width * (isReverseWidth ? -1 : 1), 0) : ((Rect)originalSelectionBounds).Width;
            var h = (int)lockedStrokeSelectionBorderHandleType != 4 && (int)lockedStrokeSelectionBorderHandleType != 5 ? Math.Round(((Rect)originalSelectionBounds).Height + new Rect(((Point)resizingFirstPoint),
                ((Point)rlp)).Height * (isReverseHeight ? -1 : 1), 0) : ((Rect)originalSelectionBounds).Height;

            var final_w = Math.Round(w,0);
            var final_h = Math.Round(h,0);

            if (isShiftKeyDown) {
                var scaleW = w / ((Rect)originalSelectionBounds).Width;
                var scaleH = h / ((Rect)originalSelectionBounds).Height;

                final_w = Math.Round(((Rect)originalSelectionBounds).Width * Math.Max(scaleW, scaleH),0);
                final_h = Math.Round(((Rect)originalSelectionBounds).Height * Math.Max(scaleW, scaleH),0);
            }

            if (final_w >= 1 && final_h >= 1) {
                // TODO 此處還需要修改行為，讓其能夠縮放到1x1；
                StrokeSelectionBorder.Width = final_w >=1 ? final_w : 1;
                StrokeSelectionBorder.Height = final_h >=1 ? final_h : 1;
                
                System.Windows.Controls.Canvas.SetLeft(StrokeSelectionBorder, l);
                System.Windows.Controls.Canvas.SetTop(StrokeSelectionBorder, t);

                resizingLastPoint = rlp;

                if (!(final_w < 96 && final_h < 64)) StrokeSelectionSizeToast.Visibility = Visibility.Visible;
                else StrokeSelectionSizeToast.Visibility = Visibility.Collapsed;

                // resize toast text
                ((TextBlock)StrokeSelectionSizeToastInner.Children[0]).Text = final_w.ToString();
                ((TextBlock)StrokeSelectionSizeToastInner.Children[2]).Text = final_h.ToString();

                // record resized bounds
                var transform = StrokeSelectionBorder.TransformToVisual(Main_Grid);
                var ori_lt = transform.Transform(new Point(0, 0));
                var ori_rb = transform.Transform(new Point(StrokeSelectionBorder.Width, StrokeSelectionBorder.Height));
                resizedSelectionBounds = new Rect(ori_lt, ori_rb);

                // preview resize
                SelectedStrokesResize(inkCanvas.GetSelectedStrokes(), (StrokeSelectionBorderHandlesEnum)lockedStrokeSelectionBorderHandleType,
                    (Rect)originalSelectionBounds, (Rect)resizedSelectionBounds, true);
            }
        }

        private Point? movingFirstPoint = null;
        private Point? movingLastPoint  = null;

        private double movingFirstBorderLeft = 0;
        private double movingFirstBorderTop = 0;

        private bool isLockedStrokeSelectionMove = false;

        private bool isProgramChangeStrokesSelection = false;

        private void StrokeSelectionBorder_MouseUp(object sender, MouseButtonEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionRotate || !isLockedStrokeSelectionMove) return;

            // release capture
            var bd = (Border)sender;
            bd.ReleaseMouseCapture();

            // record last move point
            var pt = e.GetPosition(Main_Grid);
            movingLastPoint = pt;

            // caculate offset
            var offX = ((Point)movingLastPoint).X - ((Point)movingFirstPoint).X;
            var offY = ((Point)movingLastPoint).Y - ((Point)movingFirstPoint).Y;

            // unlock
            isLockedStrokeSelectionMove = false;

            if (isStrokeSelectionCloneOn) {
                // transform strokes
                var matrix = new Matrix();
                matrix.Translate(((Point)movingLastPoint).X-((Point)movingFirstPoint).X, ((Point)movingLastPoint).Y - ((Point)movingFirstPoint).Y);
                
                isProgramChangeStrokesSelection = true;
                var ori = inkCanvas.GetSelectedStrokes();
                inkCanvas.Select(new StrokeCollection());
                clonedStrokes = ori.Clone();
                inkCanvas.Select(ori);
                isProgramChangeStrokesSelection = false;

                clonedStrokes.Transform(matrix,false);
                
                // add to inkcanvas
                inkCanvas.Strokes.Add(clonedStrokes);
                
                InkSelectionStrokesOverlay.DrawStrokes(inkCanvas.GetSelectedStrokes(), new Matrix());
                InkSelectionStrokesBackgroundInkCanvas.Strokes.Add(clonedStrokes);

            } else {
                if (isStrokesSelectionBorderMovingUseRenderTransform) {
                    StrokeSelectionBorderTranslateTransform.X = 0;
                    StrokeSelectionBorderTranslateTransform.Y = 0;
                    System.Windows.Controls.Canvas.SetLeft(StrokeSelectionBorder, movingFirstBorderLeft + offX);
                    System.Windows.Controls.Canvas.SetTop(StrokeSelectionBorder, movingFirstBorderTop + offY);
                }

                // preview
                SelectedStrokesMove(inkCanvas.GetSelectedStrokes(), (Point)movingFirstPoint, (Point)movingLastPoint, true);

                // transform strokes
                SelectedStrokesMove(inkCanvas.GetSelectedStrokes(), (Point)movingFirstPoint, (Point)movingLastPoint);

                // display selectionToolBar
                var _bd = new Binding("Visibility");
                _bd.Source = GridInkCanvasSelectionCover;
                BorderStrokeSelectionControl.SetBinding(Border.VisibilityProperty, _bd);
                updateBorderStrokeSelectionControlLocation();

                // hide move toast
                StrokeSelectionMoveToast.Visibility = Visibility.Collapsed;
            }

            
        }

        private void StrokeSelectionBorder_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionRotate || isLockedStrokeSelectionMove) return;

            // record first move point
            var pt = e.GetPosition(Main_Grid);
            movingFirstPoint = pt;

            // capture mouse
            var bd = (Border)sender;
            bd.CaptureMouse();

            // lock
            isLockedStrokeSelectionMove = true;

            // record first border position
            movingFirstBorderLeft = System.Windows.Controls.Canvas.GetLeft(StrokeSelectionBorder);
            movingFirstBorderTop = System.Windows.Controls.Canvas.GetTop(StrokeSelectionBorder);
        }

        private bool isStrokesSelectionBorderMovingUseRenderTransform = true;

        private StrokeCollection clonedStrokes;

        private void StrokeSelectionBorder_MouseMove(object sender, MouseEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionRotate || !isLockedStrokeSelectionMove) return;

            // record last move point
            var pt = e.GetPosition(Main_Grid);
            movingLastPoint = pt;

            // caculate offset
            var offX = ((Point)movingLastPoint).X - ((Point)movingFirstPoint).X;
            var offY = ((Point)movingLastPoint).Y - ((Point)movingFirstPoint).Y;

            if (isStrokeSelectionCloneOn) {
                
                var matrix = new Matrix();
                matrix.Translate(((Point)movingLastPoint).X-((Point)movingFirstPoint).X, ((Point)movingLastPoint).Y - ((Point)movingFirstPoint).Y);

                InkSelectionStrokesOverlay.Open();
                InkSelectionStrokesOverlay.DrawStrokes(inkCanvas.GetSelectedStrokes(), new Matrix(), false);
                InkSelectionStrokesOverlay.DrawStrokes(inkCanvas.GetSelectedStrokes(), matrix, false);
                InkSelectionStrokesOverlay.Close();

            } else {
                // preview
                SelectedStrokesMove(inkCanvas.GetSelectedStrokes(), (Point)movingFirstPoint, (Point)movingLastPoint, true);

                // relocate border position
                if (isStrokesSelectionBorderMovingUseRenderTransform) {
                    StrokeSelectionBorderTranslateTransform.X = offX;
                    StrokeSelectionBorderTranslateTransform.Y = offY;
                } else {
                    System.Windows.Controls.Canvas.SetLeft(StrokeSelectionBorder, movingFirstBorderLeft + offX);
                    System.Windows.Controls.Canvas.SetTop(StrokeSelectionBorder, movingFirstBorderTop + offY);
                }

                // display move toast
                if (StrokeSelectionMoveToast.Visibility == Visibility.Collapsed) StrokeSelectionMoveToast.Visibility = Visibility.Visible;

                // move toast text
                ((TextBlock)((StackPanel)StrokeSelectionMoveToastInner.Children[0]).Children[0]).Text =
                    $"X: {Math.Round(System.Windows.Controls.Canvas.GetLeft(StrokeSelectionBorder), 2)}";
                ((TextBlock)((StackPanel)StrokeSelectionMoveToastInner.Children[0]).Children[1]).Text =
                    $"Y: {Math.Round(System.Windows.Controls.Canvas.GetTop(StrokeSelectionBorder),2)}";
                ((TextBlock)((StackPanel)StrokeSelectionMoveToastInner.Children[1]).Children[1]).Text =
                    $"Δx: {Math.Round(offX,2)}";
                ((TextBlock)((StackPanel)StrokeSelectionMoveToastInner.Children[1]).Children[2]).Text =
                    $"Δy: {Math.Round(offY,2)}";

                // hide selectionToolBar
                if (BorderStrokeSelectionControl.Visibility != Visibility.Collapsed) BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;
            }
        }

        private Point? StrokesRotateSelectionBoundsCenterPoint = null;
        private Point? rotatingLastPoint = null;
        private bool isLockedStrokeSelectionRotate = false;

        private void StrokeSelectionRotateHandle_MouseUp(object sender, MouseButtonEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionMove || !isLockedStrokeSelectionRotate) return;

            // unlock
            isLockedStrokeSelectionRotate = false;

            // record last point
            var pt = e.GetPosition(Main_Grid);
            rotatingLastPoint = pt;

            // caculate rotate angle
            var vec1 = new double[] {
                ((Point)rotatingLastPoint).X - ((Point)StrokesRotateSelectionBoundsCenterPoint).X ,
                ((Point)rotatingLastPoint).Y - ((Point)StrokesRotateSelectionBoundsCenterPoint).Y
            };
            var vec_base = new double[] {
                0,
                ((Point)StrokesRotateSelectionBoundsCenterPoint).Y
            };
            var cosine = (vec_base[0] * vec1[0] + vec_base[1] * vec1[1]) /
                         (Math.Sqrt(Math.Pow(vec_base[0], 2) + Math.Pow(vec_base[1], 2)) *
                          Math.Sqrt(Math.Pow(vec1[0], 2) + Math.Pow(vec1[1], 2)));
            var angle = Math.Acos(cosine);

            // 判斷在第幾象限
            var isIn2And3Quadrant = ((Point)rotatingLastPoint).X <= ((Point)StrokesRotateSelectionBoundsCenterPoint).X;

            // final angle
            var rotateAngle = Math.Round(180 + 180 * (angle / Math.PI) * (isIn2And3Quadrant ? 1 : -1), 0);

            // release capture
            var bd = (Border)sender;
            bd.ReleaseMouseCapture();

            // hide rotate toast
            StrokeSelectionRotateToast.Visibility = Visibility.Collapsed;

            // hide guideline overlay
            InkSelectionRotateGuidelineOverlay.Visibility = Visibility.Collapsed;

            // border rotate reset
            StrokeSelectionBorderRotateTransform.Angle = 0;
            StrokeSelectionBorderRotateTransform.CenterX = 0;
            StrokeSelectionBorderRotateTransform.CenterY = 0;

            // rotate stroke
            SelectedStrokesRotate(inkCanvas.GetSelectedStrokes(), rotateAngle,
                (Point)StrokesRotateSelectionBoundsCenterPoint);

            // border re-caculate strokes bounds
            UpdateStrokeSelectionBorder(true, inkCanvas.GetSelectionBounds());

            // display selectionToolBar
            var _bd = new Binding("Visibility");
            _bd.Source = GridInkCanvasSelectionCover;
            BorderStrokeSelectionControl.SetBinding(Border.VisibilityProperty, _bd);
            updateBorderStrokeSelectionControlLocation();
        }

        private void StrokeSelectionRotateHandle_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionMove || isLockedStrokeSelectionRotate) return;

            // lock
            isLockedStrokeSelectionRotate = true;

            // record center point
            var bounds = inkCanvas.GetSelectionBounds();
            StrokesRotateSelectionBoundsCenterPoint = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

            // capture mouse
            var bd = (Border)sender;
            bd.CaptureMouse();
        }

        private void StrokeSelectionRotateHandle_MouseMove(object sender, MouseEventArgs e) {
            if (isStrokeSelectionBorderLocked) return;
            if (isLockedStrokeSelectionHandle || isLockedStrokeSelectionMove || !isLockedStrokeSelectionRotate) return;

            // record last point
            var pt = e.GetPosition(Main_Grid);
            rotatingLastPoint = pt;

            // caculate rotate angle
            var vec1 = new double[] {
                ((Point)rotatingLastPoint).X - ((Point)StrokesRotateSelectionBoundsCenterPoint).X ,
                ((Point)rotatingLastPoint).Y - ((Point)StrokesRotateSelectionBoundsCenterPoint).Y
            };
            var vec_base = new double[] {
                0,
                ((Point)StrokesRotateSelectionBoundsCenterPoint).Y
            };
            var cosine = (vec_base[0] * vec1[0] + vec_base[1] * vec1[1]) /
                         (Math.Sqrt(Math.Pow(vec_base[0],2) + Math.Pow(vec_base[1],2)) *
                          Math.Sqrt(Math.Pow(vec1[0],2) + Math.Pow(vec1[1],2)));
            var angle = Math.Acos(cosine);

            // 判斷在第幾象限
            var isIn2And3Quadrant = ((Point)rotatingLastPoint).X <= ((Point)StrokesRotateSelectionBoundsCenterPoint).X;

            // final angle
            var rotateAngle = Math.Round(180 + 180 * (angle / Math.PI) * (isIn2And3Quadrant ? 1 : -1), 0);

            // display guideline overlay
            if (InkSelectionRotateGuidelineOverlay.Visibility == Visibility.Collapsed)
                InkSelectionRotateGuidelineOverlay.Visibility = Visibility.Visible;

            var guidelinePen1 = new Pen(); // guideline1 thickness1 dash yellow opacity255
            guidelinePen1.Brush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            guidelinePen1.DashStyle = DashStyles.Dash;
            guidelinePen1.Thickness = 1;

            var guidelinePen2 = new Pen(); // guideline2 thickness2 dash yellow opacity255
            guidelinePen2.Brush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            guidelinePen2.DashStyle = DashStyles.Dash;
            guidelinePen2.Thickness = 2.5;

            var guidelinePen3 = new Pen(); // guideline3 thickness1 dash yellow opacity72
            guidelinePen3.Brush = new SolidColorBrush(Color.FromArgb(72, 245, 158, 11));
            guidelinePen3.DashStyle = DashStyles.Dash;
            guidelinePen3.Thickness = 1;

            var guidelinePen4 = new Pen(); // guideline4 thickness1 dot green opacity255
            guidelinePen4.Brush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            guidelinePen4.DashStyle = DashStyles.Dot;
            guidelinePen4.Thickness = 1;

            var guidelineSnapAngle = 0;
            // draw guideline
            var dv = InkSelectionRotateGuidelineOverlay.DrawingVisual;
            using (DrawingContext dc = dv.RenderOpen()) {
                dc.DrawLine(guidelinePen1, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X,0));
                dc.DrawLine(guidelinePen3, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X,Main_Grid.ActualHeight));
                dc.DrawLine(guidelinePen2, (Point)StrokesRotateSelectionBoundsCenterPoint, (Point)rotatingLastPoint);
                
                var QuadrantOneLongest = Math.Max(
                    Main_Grid.ActualWidth - ((Point)StrokesRotateSelectionBoundsCenterPoint).X,
                    ((Point)StrokesRotateSelectionBoundsCenterPoint).Y);
                //30 angle
                if (rotateAngle >= 26 && rotateAngle <= 34) {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X + QuadrantOneLongest, ((Point)StrokesRotateSelectionBoundsCenterPoint).Y - QuadrantOneLongest * Math.Sqrt(3)));
                    guidelineSnapAngle = 30;
                }
                //45 angle
                if (rotateAngle >= 41 && rotateAngle <= 49) {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X+QuadrantOneLongest, ((Point)StrokesRotateSelectionBoundsCenterPoint).Y - QuadrantOneLongest));
                    guidelineSnapAngle = 45;
                }
                //60 angle
                if (rotateAngle >= 56 && rotateAngle <= 64) {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X + QuadrantOneLongest *Math.Sqrt(3), ((Point)StrokesRotateSelectionBoundsCenterPoint).Y - QuadrantOneLongest));
                    guidelineSnapAngle = 60;
                }
                //90 angle
                if (rotateAngle >= 86 && rotateAngle <= 94) {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X + QuadrantOneLongest, ((Point)StrokesRotateSelectionBoundsCenterPoint).Y));
                    guidelineSnapAngle = 90;
                }
                var QuadrantFourLongest = Math.Max(
                    Main_Grid.ActualWidth - ((Point)StrokesRotateSelectionBoundsCenterPoint).X,
                    Main_Grid.ActualHeight - ((Point)StrokesRotateSelectionBoundsCenterPoint).Y);
                //120 angle
                if (rotateAngle >= 116 && rotateAngle <= 124) {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X + QuadrantFourLongest * Math.Sqrt(3), ((Point)StrokesRotateSelectionBoundsCenterPoint).Y + QuadrantFourLongest));
                    guidelineSnapAngle = 120;
                }
                //150 angle
                if (rotateAngle >= 146 && rotateAngle <= 154) {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X + QuadrantFourLongest, ((Point)StrokesRotateSelectionBoundsCenterPoint).Y + QuadrantFourLongest * Math.Sqrt(3)));
                    guidelineSnapAngle = 150;
                }
                //180 angle
                if (rotateAngle >= 176 && rotateAngle <= 184)
                {
                    dc.DrawLine(guidelinePen4, (Point)StrokesRotateSelectionBoundsCenterPoint, new Point(((Point)StrokesRotateSelectionBoundsCenterPoint).X, ((Point)StrokesRotateSelectionBoundsCenterPoint).Y + QuadrantFourLongest));
                    guidelineSnapAngle = 180;
                }
            }

            // preview
            SelectedStrokesRotate(inkCanvas.GetSelectedStrokes(), guidelineSnapAngle!=0?guidelineSnapAngle:rotateAngle,
                (Point)StrokesRotateSelectionBoundsCenterPoint, true);

            // border rotate
            StrokeSelectionBorderRotateTransform.Angle = guidelineSnapAngle != 0 ? guidelineSnapAngle : rotateAngle;
            StrokeSelectionBorderRotateTransform.CenterX = StrokeSelectionBorder.Width / 2;
            StrokeSelectionBorderRotateTransform.CenterY = StrokeSelectionBorder.Height / 2;

            // display rotate toast
            if (StrokeSelectionRotateToast.Visibility == Visibility.Collapsed) StrokeSelectionRotateToast.Visibility = Visibility.Visible;

            // update rotate toast
            ((TextBlock)StrokeSelectionRotateToast.Child).Text =
                (guidelineSnapAngle != 0 ? guidelineSnapAngle : rotateAngle) == 360 
                ? "360° = 0°" : $"{(guidelineSnapAngle != 0 ? guidelineSnapAngle : rotateAngle)}°";


            // hide selectionToolBar
            if (BorderStrokeSelectionControl.Visibility != Visibility.Collapsed) BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;
        }

        private void StrokeSelectionBorder_MouseEnter(object sender, MouseEventArgs e) {
            if (isStrokeSelectionBorderLocked) {
                StrokeSelectionCursorArea.Background = new SolidColorBrush(Color.FromArgb(5, 96, 165, 250));
                return;
            };
            StrokeSelectionCursorArea.Background = new SolidColorBrush(Color.FromArgb(17, 96, 165, 250));
        }

        private void StrokeSelectionBorder_MouseLeave(object sender, MouseEventArgs e) {
            if (isStrokeSelectionBorderLocked) {
                StrokeSelectionCursorArea.Background = new SolidColorBrush(Color.FromArgb(5, 96, 165, 250));
                return;
            };
            StrokeSelectionCursorArea.Background = new SolidColorBrush(Color.FromArgb(5, 96, 165, 250));
        }

        private void ApplyCursorToStrokeSelectionBorder() {
            StrokeSelectionCursorArea.ForceCursor = true;
            StreamResourceInfo sri_move = Application.GetResourceStream(
                new Uri("Resources/Cursors/cursor-move.cur", UriKind.Relative));
            StrokeSelectionCursorArea.Cursor = new Cursor(sri_move.Stream);

            StreamResourceInfo sri_lr = Application.GetResourceStream(
                new Uri("Resources/Cursors/cursor-resize-lr.cur", UriKind.Relative));
            StreamResourceInfo sri_tb = Application.GetResourceStream(
                new Uri("Resources/Cursors/cursor-resize-tb.cur", UriKind.Relative));
            StreamResourceInfo sri_lt_rb = Application.GetResourceStream(
                new Uri("Resources/Cursors/cursor-resize-lt-rb.cur", UriKind.Relative));
            StreamResourceInfo sri_rt_lb = Application.GetResourceStream(
                new Uri("Resources/Cursors/cursor-resize-rt-lb.cur", UriKind.Relative));

            foreach (var bd in StrokeSelectionBorderHandles) {
                bd.ForceCursor = true;
            }
            StrokeSelectionBorderHandles[0].Cursor = StrokeSelectionBorderHandles[3].Cursor = new Cursor(sri_lt_rb.Stream);
            StrokeSelectionBorderHandles[1].Cursor = StrokeSelectionBorderHandles[2].Cursor = new Cursor(sri_rt_lb.Stream);
            StrokeSelectionBorderHandles[4].Cursor = StrokeSelectionBorderHandles[5].Cursor = new Cursor(sri_lr.Stream);
            StrokeSelectionBorderHandles[6].Cursor = StrokeSelectionBorderHandles[7].Cursor = new Cursor(sri_tb.Stream);

            StreamResourceInfo sri_open_hand = Application.GetResourceStream(
                new Uri("Resources/Cursors/open-hand-cursor.cur", UriKind.Relative));
            StrokeSelectionBorderHandles[8].Cursor = new Cursor(sri_open_hand.Stream);
        }

        public enum StrokeSelectionBorderHandlesEnum {
            LeftTop, RightTop, LeftBottom, RightBottom,
            Left, Right, Top, Bottom,
            Rotate
        }

        private void AddStrokeSelectionHandlesToArr() {
            StrokeSelectionBorderHandles = new Border[] {
                StrokeSelectionLTHandle,
                StrokeSelectionRTHandle,
                StrokeSelectionLBHandle,
                StrokeSelectionRBHandle,
                StrokeSelectionLHandle,
                StrokeSelectionRHandle,
                StrokeSelectionTHandle,
                StrokeSelectionBHandle,
                StrokeSelectionRotateHandle
            };
            foreach (var hd in StrokeSelectionBorderHandles) {
                if (hd.Name== "StrokeSelectionRotateHandle") continue;
                hd.MouseUp += StrokeSelectionBorderHandle_MouseUp;
                hd.MouseDown += StrokeSelectionBorderHandle_MouseDown;
                hd.MouseMove += StrokeSelectionBorderHandle_MouseMove;
            }
            StrokeSelectionRotateHandle.MouseUp += StrokeSelectionRotateHandle_MouseUp;
            StrokeSelectionRotateHandle.MouseDown += StrokeSelectionRotateHandle_MouseDown;
            StrokeSelectionRotateHandle.MouseMove += StrokeSelectionRotateHandle_MouseMove;

            ApplyCursorToStrokeSelectionBorder();
        }

        private void SelectedStrokesRotate(StrokeCollection strokes, double angle, Point? centerPoint, bool isPreview = false) {
            if (centerPoint == null) return;
            var matrix = new Matrix();
            matrix.RotateAt(angle, ((Point)centerPoint).X, ((Point)centerPoint).Y);

            if (isPreview) {
                InkSelectionStrokesOverlay.DrawStrokes(strokes, matrix);
            } else {
                strokes.Transform(matrix, Settings.Canvas.ApplyScaleToStylusTip);
            }
        }

        private void SelectedStrokesMove(StrokeCollection strokes, Point firstPoint, Point lastPoint, bool isPreview = false) {

            var matrix = new Matrix();
            matrix.Translate(lastPoint.X-firstPoint.X, lastPoint.Y - firstPoint.Y);

            if (isPreview) {
                InkSelectionStrokesOverlay.DrawStrokes(strokes, matrix);
            } else {
                strokes.Transform(matrix, Settings.Canvas.ApplyScaleToStylusTip);
            }
        }

        /// <summary>
        /// 調整選中墨跡的大小
        /// </summary>
        /// <param name="strokes"></param>
        /// <param name="handleType"></param>
        /// <param name="originalRect"></param>
        /// <param name="resizedRect"></param>
        /// <param name="isPreview"></param>
        /// <returns></returns>
        private Rect? SelectedStrokesResize(StrokeCollection strokes, StrokeSelectionBorderHandlesEnum handleType, Rect originalRect, Rect resizedRect, bool isPreview = false) {
            if ((strokes?.Count ?? 0) == 0 || handleType == null || originalRect.Width == 0 || originalRect.Height == 0
                || resizedRect.Width == 0 || resizedRect.Height == 0) return null;
            Matrix matrix = new Matrix();

            if ((int)handleType <= 3) {
                var asidePt = (int)handleType == 0 ? originalRect.BottomRight :
                    (int)handleType == 1 ? originalRect.BottomLeft :
                    (int)handleType == 2 ? originalRect.TopRight : originalRect.TopLeft;
                matrix.ScaleAt(Math.Round(resizedRect.Width / originalRect.Width, 2),
                    Math.Round(resizedRect.Height / originalRect.Height, 2),
                    asidePt.X, asidePt.Y);
            } else if ((int)handleType == 4 || (int)handleType == 5) {
                var asideX = (int)handleType == 5 ? originalRect.Left : originalRect.Right;
                matrix.ScaleAt(Math.Round(resizedRect.Width / originalRect.Width, 2),1D,asideX, 0);
            } else if ((int)handleType == 6 || (int)handleType == 7) {
                var asideY = (int)handleType == 7 ? originalRect.Top : originalRect.Bottom;
                matrix.ScaleAt(1D, Math.Round(resizedRect.Height / originalRect.Height, 2), 0, asideY);
            }

            if (isPreview) {
                InkSelectionStrokesOverlay.DrawStrokes(strokes, matrix);
            } else {
                strokes.Transform(matrix, Settings.Canvas.ApplyScaleToStylusTip);
            }

            return strokes.GetBounds();
        }

        private void UpdateStrokeSelectionBorder(bool isDisplay, Rect? rect) {
            if (isDisplay) {
                if (rect == null) return;
                var r = (Rect)rect;
                if (r.Height == 0 || r.Width == 0) return;
                InkCanvasSelectFakeAdornerCanvas.Visibility = Visibility.Visible;
                System.Windows.Controls.Canvas.SetLeft(StrokeSelectionBorder,r.Left);
                System.Windows.Controls.Canvas.SetTop(StrokeSelectionBorder,r.Top);
                StrokeSelectionBorder.Width = r.Width;
                StrokeSelectionBorder.Height = r.Height;
                if (StrokeSelectionBorderHandles.Length == 0) AddStrokeSelectionHandlesToArr();
                StrokeSelectionBorder.Visibility = Visibility.Visible;
            } else {
                InkCanvasSelectFakeAdornerCanvas.Visibility = Visibility.Collapsed;
                StrokeSelectionBorder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 取消选中本次选中的墨迹或对象，准备继续进行矩形选框操作。
        /// </summary>
        private void CancelCurrentStrokesSelection() {
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            inkCanvas.Select(new StrokeCollection());
            UpdateStrokeSelectionBorder(false, null);
            RectangleSelectionHitTestBorder.Visibility = Visibility.Visible;
            var cm = this.FindResource("StrokesSelectionMoreMenuButtonContextMenu") as ContextMenu;
            cm.IsOpen = false;
        }

        #endregion

        private void inkCanvas_SelectionChanged(object sender, EventArgs e) {
            if (isProgramChangeStrokesSelection) return;
            isStrokeSelectionCloneOn = false;
            if (inkCanvas.GetSelectedStrokes().Count == 0) {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                UpdateStrokeSelectionBorder(false,null);
                inkCanvas.Opacity = 1;
                InkSelectionStrokesOverlay.Visibility = Visibility.Collapsed;
                InkSelectionStrokesBackgroundInkCanvas.Visibility = Visibility.Collapsed;
                InkSelectionStrokesOverlay.DrawStrokes(new StrokeCollection(), new Matrix());
            }
            else {
                var isAllStrokesLocked = false;
                if (inkCanvas.GetSelectedStrokes().Count > 1) {
                    List<Stroke> lockedStrokes = new List<Stroke>();
                    var stks = inkCanvas.GetSelectedStrokes();
                    foreach (var stk in stks) {
                        if (stk.ContainsPropertyData(IsLockGuid)) lockedStrokes.Add(stk);
                    }

                    if (lockedStrokes.Count != stks.Count && lockedStrokes.Count!=0) {
                        stks.Remove(new StrokeCollection(lockedStrokes));
                        inkCanvas.Select(stks);
                        return;
                    } else if (lockedStrokes.Count == stks.Count) {
                        isAllStrokesLocked = true;
                    }
                } else {
                    isAllStrokesLocked = inkCanvas.GetSelectedStrokes().Single().ContainsPropertyData(IsLockGuid);
                }

                GridInkCanvasSelectionCover.Visibility = Visibility.Visible;
                BorderStrokeSelectionClone.Background = Brushes.Transparent;
                RectangleSelectionHitTestBorder.Visibility = Visibility.Collapsed;
                UpdateStrokeSelectionBorder(true, inkCanvas.GetSelectionBounds());
                inkCanvas.Opacity = 0;
                InkSelectionStrokesOverlay.Visibility = Visibility.Visible;
                InkSelectionStrokesBackgroundInkCanvas.Visibility = Visibility.Visible;
                InkSelectionStrokesBackgroundInkCanvas.Strokes.Clear();
                InkSelectionStrokesBackgroundInkCanvas.Strokes.Add(inkCanvas.Strokes);
                InkSelectionStrokesBackgroundInkCanvas.Strokes.Remove(inkCanvas.GetSelectedStrokes());
                InkSelectionStrokesOverlay.DrawStrokes(inkCanvas.GetSelectedStrokes(), new Matrix());
                UpdateStrokesSelectionCloneToolButtonLimitStatus();
                updateBorderStrokeSelectionControlLocation();
                UpdateSelectionToolbarOtherIconLockedStatus(isAllStrokesLocked);
                UpdateSelectionToolBarLockIcon(isAllStrokesLocked);
                UpdateSelectionBorderHandlesLockStatus(isAllStrokesLocked);
            }
        }

        private void updateBorderStrokeSelectionControlLocation() {
            if (currentMode == 0) BorderStrokeSelectionCloneToNewBoardTextBlock.Text = "克隆到白板";
                else BorderStrokeSelectionCloneToNewBoardTextBlock.Text = "克隆到新页";

            var _w = 660;
            BorderStrokeSelectionControl.Width = _w;

            var borderLeft = (inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Right - _w) / 2;
            var borderTop = inkCanvas.GetSelectionBounds().Bottom + 24;
            if (borderLeft < 0) borderLeft = 0;
            if (borderTop < 0) borderTop = 0;
            if (Width - borderLeft < _w || double.IsNaN(borderLeft))
                borderLeft = Width - _w;
            if (Height - borderTop < 48 || double.IsNaN(borderTop))
                borderTop = Height - 48;

            //if (borderTop > 60) borderTop -= 60;
            BorderStrokeSelectionControl.Margin = new Thickness(borderLeft, borderTop, 0, 0);
        }

        #region Strokes Lock

        public Guid IsLockGuid = new Guid("b701bb3f-16bf-43ce-a88f-30eab85cd77b");

        private void UpdateSelectionToolBarLockIcon(bool isLocked) {
            BorderStrokeSelectionLock_LockClose.Visibility = !isLocked ? Visibility.Visible : Visibility.Collapsed;
            BorderStrokeSelectionLock_LockOpen.Visibility = isLocked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateSelectionToolbarOtherIconLockedStatus(bool isLocked) {
            var toolbtns = new Border[] {
                BorderStrokeSelectionClone,
                BorderStrokeSelectionCloneToNewBoard,
                BorderImageRotate45,
                BorderImageRotate90,
                BorderImageFlipHorizontal,
                BorderImageFlipVertical,
                BorderStrokePaletteButton,
                BorderStrokeSelectionDelete,
                BorderImageRotateClockwise
            };

            foreach (var tb in toolbtns)
            {
                tb.IsHitTestVisible = !isLocked;
                tb.Opacity = isLocked ? 0.5 : 1;
                tb.IsEnabled = !isLocked;
            }
        }

        private bool isStrokeSelectionBorderLocked = false;

        private void UpdateSelectionBorderHandlesLockStatus(bool isLocked) {
            var _v = !isLocked ? Visibility.Visible : Visibility.Collapsed;
            foreach (var hd in StrokeSelectionBorderHandles) hd.Visibility = _v;
            StrokeSelectionRotateHandleConnectLine.Visibility = _v;
            isStrokeSelectionBorderLocked = isLocked;
            if (isLocked) {
                StrokeSelectionCursorArea.ForceCursor = false;
                StrokeSelectionCursorArea.Cursor = Cursors.Arrow;
            } else {
                StrokeSelectionCursorArea.ForceCursor = true;
                StreamResourceInfo sri_move = Application.GetResourceStream(
                    new Uri("Resources/Cursors/cursor-move.cur", UriKind.Relative));
                StrokeSelectionCursorArea.Cursor = new Cursor(sri_move.Stream);
            }
        }

        private void BorderStrokeSelectionLock_MouseUp(object sender, MouseButtonEventArgs e) {
            if (BorderStrokeSelectionToolButtonMouseDown != (Border)sender) return;

            var isAllStrokesLocked = BorderStrokeSelectionLock_LockOpen.Visibility == Visibility.Visible;

            var stks = inkCanvas.GetSelectedStrokes();
            if (isAllStrokesLocked) {
                foreach (var stk in stks) {
                    stk.RemovePropertyData(IsLockGuid);
                }
            } else {
                foreach (var stk in stks) {
                    stk.AddPropertyData(IsLockGuid, "Locked");
                }
            }
            
            UpdateSelectionToolBarLockIcon(!isAllStrokesLocked);
            UpdateSelectionToolbarOtherIconLockedStatus(!isAllStrokesLocked);
            UpdateSelectionBorderHandlesLockStatus(!isAllStrokesLocked);

            // toolbutton
            BorderStrokeSelectionToolButton_MouseLeave(sender, e);
        }

        #endregion

        private void GridInkCanvasSelectionCover_ManipulationStarting(object sender, ManipulationStartingEventArgs e) {
            e.Mode = ManipulationModes.All;
        }

        private void GridInkCanvasSelectionCover_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e) {
            if (StrokeManipulationHistory?.Count > 0)
            {
                timeMachine.CommitStrokeManipulationHistory(StrokeManipulationHistory);
                foreach (var item in StrokeManipulationHistory)
                {
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
                }
                StrokeManipulationHistory = null;
            }
            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
        }

        private void GridInkCanvasSelectionCover_ManipulationDelta(object sender, ManipulationDeltaEventArgs e) {
            try {
                if (dec.Count >= 1) {
                    var md = e.DeltaManipulation;
                    var trans = md.Translation; // 获得位移矢量
                    var rotate = md.Rotation; // 获得旋转角度
                    var scale = md.Scale; // 获得缩放倍数

                    var m = new Matrix();

                    // Find center of element and then transform to get current location of center
                    var fe = e.Source as FrameworkElement;
                    var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                    center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                        inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
                    center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

                    // Update matrix to reflect translation/rotation
                    m.Translate(trans.X, trans.Y); // 移动
                    m.ScaleAt(scale.X, scale.Y, center.X, center.Y); // 缩放

                    var strokes = inkCanvas.GetSelectedStrokes();
                    if (StrokesSelectionClone.Count != 0)
                        strokes = StrokesSelectionClone;
                    else if (Settings.Gesture.IsEnableTwoFingerRotationOnSelection)
                        m.RotateAt(rotate, center.X, center.Y); // 旋转
                    foreach (var stroke in strokes) {
                        stroke.Transform(m, false);

                        try {
                            stroke.DrawingAttributes.Width *= md.Scale.X;
                            stroke.DrawingAttributes.Height *= md.Scale.Y;
                        }
                        catch { }
                    }

                    updateBorderStrokeSelectionControlLocation();
                }
            }
            catch { }
        }

        #region SelectionV2 Popup

        private void SelectionV2Init() {
            FloatingToolBarV2.SelectionV2_SelectAllEvent += (sender, args) => {
                inkCanvas.Select(inkCanvas.Strokes);
                FloatingToolBarV2.SelectionPopupV2.IsOpen = false;
            };
            FloatingToolBarV2.SelectionV2_UnSelectEvent += (sender, args) => {
                CancelCurrentStrokesSelection();
                FloatingToolBarV2.SelectionPopupV2.IsOpen = false;
            };
            FloatingToolBarV2.SelectionV2_ReverseSelectEvent += (sender, args) => {
                var strokes = new StrokeCollection(inkCanvas.Strokes.Where(stroke =>
                    !inkCanvas.GetSelectedStrokes().Contains(stroke)).Where(stroke=>!stroke.ContainsPropertyData(IsLockGuid)));
                if (strokes.Any()) {
                    inkCanvas.Select(strokes);
                } else {
                    CancelCurrentStrokesSelection();
                }
                FloatingToolBarV2.SelectionPopupV2.IsOpen = false;
            };
            FloatingToolBarV2.SelectionV2.ApplyScaleToStylusTip = Settings.Canvas.ApplyScaleToStylusTip;
            FloatingToolBarV2.SelectionV2.OnlyHitTestFullyContainedStrokes = Settings.Canvas.OnlyHitTestFullyContainedStrokes;
            FloatingToolBarV2.SelectionV2.AllowClickToSelectLockedStroke = Settings.Canvas.AllowClickToSelectLockedStroke;
            FloatingToolBarV2.SelectionV2.SelectionModeSelected = (SelectionPopup.SelectionMode)Settings.Canvas.SelectionMethod;
            FloatingToolBarV2.SelectionV2_ApplyScaleToStylusTipChanged += (sender, args) => {
                if (!isLoaded) return;
                Settings.Canvas.ApplyScaleToStylusTip = FloatingToolBarV2.SelectionV2.ApplyScaleToStylusTip;
                ToggleSwitchApplyScaleToStylusTip.IsOn = FloatingToolBarV2.SelectionV2.ApplyScaleToStylusTip;
                SaveSettingsToFile();
            };
            FloatingToolBarV2.SelectionV2_OnlyHitTestFullyContainedStrokesChanged += (sender, args) => {
                if (!isLoaded) return;
                Settings.Canvas.OnlyHitTestFullyContainedStrokes = FloatingToolBarV2.SelectionV2.OnlyHitTestFullyContainedStrokes;
                ToggleSwitchOnlyHitTestFullyContainedStrokes.IsOn = FloatingToolBarV2.SelectionV2.OnlyHitTestFullyContainedStrokes;
                SaveSettingsToFile();
            };
            FloatingToolBarV2.SelectionV2_AllowClickToSelectLockedStrokeChanged += (sender, args) => {
                if (!isLoaded) return;
                Settings.Canvas.AllowClickToSelectLockedStroke = FloatingToolBarV2.SelectionV2.AllowClickToSelectLockedStroke;
                ToggleSwitchAllowClickToSelectLockedStroke.IsOn = FloatingToolBarV2.SelectionV2.AllowClickToSelectLockedStroke;
                SaveSettingsToFile();
            };
            FloatingToolBarV2.SelectionV2_SelectionModeChanged += (sender, args) => {
                if (!isLoaded) return;
                Settings.Canvas.SelectionMethod = (int)args.NowMode;
                ComboBoxSelectionMethod.SelectedIndex = (int)args.NowMode;
                SaveSettingsToFile();
            };
        }

        #endregion
    }
}