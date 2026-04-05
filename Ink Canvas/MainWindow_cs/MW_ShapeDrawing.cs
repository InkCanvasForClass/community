using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Floating Bar Control

        /// <summary>
        /// 处理形状绘制按钮的鼠标抬起事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当形状绘制按钮被点击时：
        /// 1. 重置之前按下的面板背景
        /// 2. 检查是否是浮动栏按钮且不是当前按下的对象
        /// 3. 如果形状绘制面板可见，则隐藏它
        /// 4. 如果形状绘制面板不可见，则显示它
        /// </remarks>
        private void ImageDrawShape_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == ShapeDrawFloatingBarBtn && lastBorderMouseDownObject != ShapeDrawFloatingBarBtn) return;

            // FloatingBarIcons_MouseUp_New(sender);
            if (BorderDrawShape.Visibility == Visibility.Visible)
            {
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
            }
            else
            {
                HideSubPanels();
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BorderDrawShape);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderDrawShape);
            }
        }

        #endregion Floating Bar Control

        /// <summary>
        /// 形状绘制模式
        /// </summary>
        /// <remarks>
        /// 不同的值表示不同的形状绘制模式：
        /// 1: 直线
        /// 2: 箭头
        /// 3: 矩形
        /// 4: 椭圆
        /// 5: 圆形
        /// 6: 圆柱
        /// 7: 圆锥
        /// 8: 虚线
        /// 9: 长方体
        /// 10: 虚线圆形
        /// 11: 坐标系1
        /// 12: 坐标系2
        /// 13: 坐标系3
        /// 14: 坐标系4
        /// 15: 平行线
        /// 16: 中心椭圆
        /// 17: 坐标系5
        /// 18: 点线
        /// 19: 中心矩形
        /// 20: 抛物线1
        /// 21: 抛物线2
        /// 22: 带焦点的抛物线
        /// 23: 带焦点的中心椭圆
        /// 24: 双曲线
        /// 25: 带焦点的双曲线
        /// </remarks>
        private int drawingShapeMode;

        /// <summary>
        /// 用于存储是否是"选中"状态，便于后期抬笔后不做切换到笔的处理
        /// </summary>
        private bool isLongPressSelected;

        #region Buttons

        /// <summary>
        /// 处理形状绘制面板固定按钮的鼠标抬起事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当形状绘制面板固定按钮被点击时：
        /// 1. 检查是否是当前按下的对象
        /// 2. 切换自动隐藏开关的状态
        /// 3. 根据开关状态更新图标为固定或未固定状态
        /// </remarks>
        private void SymbolIconPinBorderDrawShape_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            ToggleSwitchDrawShapeBorderAutoHide.IsOn = !ToggleSwitchDrawShapeBorderAutoHide.IsOn;

            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn)
                ((FontIcon)sender).Icon = SegoeFluentIcons.Pin;
            else
                ((FontIcon)sender).Icon = SegoeFluentIcons.Unpin;
        }

        /// <summary>
        /// 上一次鼠标按下的发送者
        /// </summary>
        private object lastMouseDownSender;

        /// <summary>
        /// 上一次鼠标按下的时间
        /// </summary>
        private DateTime lastMouseDownTime = DateTime.MinValue;

        /// <summary>
        /// 处理形状绘制按钮的鼠标按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当形状绘制按钮被长按（500毫秒）时：
        /// 1. 记录当前按下的对象和时间
        /// 2. 等待500毫秒检查是否仍然是同一个对象
        /// 3. 如果是同一个对象，设置透明度动画
        /// 4. 禁用橡皮擦覆盖
        /// 5. 设置强制橡皮擦模式
        /// 6. 根据不同的按钮设置不同的形状绘制模式
        /// 7. 更新工具模式缓存
        /// 8. 设置长按选中标志
        /// 9. 如果是单指拖拽模式，取消该模式
        /// </remarks>
        private async void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastMouseDownSender = sender;
            lastMouseDownTime = DateTime.Now;

            await Task.Delay(500);

            if (lastMouseDownSender == sender)
            {
                lastMouseDownSender = null;
                var dA = new DoubleAnimation(1, 0.3, new Duration(TimeSpan.FromMilliseconds(100)));
                ((UIElement)sender).BeginAnimation(OpacityProperty, dA);

                forcePointEraser = false;
                DisableEraserOverlay();

                forceEraser = true;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                if (sender == ImageDrawLine || sender == BoardImageDrawLine)
                    drawingShapeMode = 1;
                else if (sender == ImageDrawDashedLine || sender == BoardImageDrawDashedLine)
                    drawingShapeMode = 8;
                else if (sender == ImageDrawDotLine || sender == BoardImageDrawDotLine)
                    drawingShapeMode = 18;
                else if (sender == ImageDrawArrow || sender == BoardImageDrawArrow)
                    drawingShapeMode = 2;
                else if (sender == ImageDrawParallelLine || sender == BoardImageDrawParallelLine) drawingShapeMode = 15;

                // 更新模式缓存
                UpdateCurrentToolMode("shape");

                isLongPressSelected = true;
                if (isSingleFingerDragMode) BtnFingerDragMode_Click(BtnFingerDragMode, null);
            }
        }

        /// <summary>
        /// 处理画笔按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 当画笔按钮被点击时：
        /// 1. 禁用强制橡皮擦模式
        /// 2. 重置形状绘制模式
        /// 3. 设置墨水画布编辑模式为墨迹
        /// 4. 启用墨水画布的操作功能
        /// 5. 取消单指拖拽模式
        /// 6. 重置长按选中标志
        /// </remarks>
        private void BtnPen_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = false;
            drawingShapeMode = 0;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            isLongPressSelected = false;
        }

        /// <summary>
        /// 检查是否在多点触控模式下绘制形状
        /// </summary>
        /// <returns>返回一个表示操作成功的Task<bool></returns>
        /// <remarks>
        /// 检查多点触控模式并在需要时禁用：
        /// 1. 如果当前在多点触控模式下
        /// 2. 禁用多点触控模式
        /// 3. 记录之前的多点触控模式状态
        /// 4. 返回成功的任务
        /// </remarks>
        private Task<bool> CheckIsDrawingShapesInMultiTouchMode()
        {
            if (isInMultiTouchMode)
            {
                ToggleSwitchEnableMultiTouchMode.IsOn = false;
                lastIsInMultiTouchMode = true;
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// 处理绘制直线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制直线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 如果是长按操作，设置绘制模式为直线
        /// 3. 重置鼠标按下发送者
        /// 4. 如果是长按选中状态，处理相关逻辑
        /// 5. 提示切换到画笔模式
        /// </remarks>
        public async void BtnDrawLine_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            if (lastMouseDownSender == sender)
            {
                forcePointEraser = false;
                DisableEraserOverlay();

                forceEraser = true;
                drawingShapeMode = 1;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                CancelSingleFingerDragMode();
            }

            lastMouseDownSender = null;
            if (isLongPressSelected)
            {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                if (sender is UIElement ui)
                {
                    var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                    ui.BeginAnimation(OpacityProperty, dA);
                }
            }

            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制虚线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制虚线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 如果是长按操作，设置绘制模式为虚线
        /// 3. 重置鼠标按下发送者
        /// 4. 如果是长按选中状态，处理相关逻辑
        /// 5. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawDashedLine_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            if (lastMouseDownSender == sender)
            {
                forcePointEraser = false;
                DisableEraserOverlay();

                forceEraser = true;
                drawingShapeMode = 8;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                CancelSingleFingerDragMode();
            }

            lastMouseDownSender = null;
            if (isLongPressSelected)
            {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                if (sender is UIElement ui)
                {
                    var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                    ui.BeginAnimation(OpacityProperty, dA);
                }
            }

            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制点线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制点线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 如果是长按操作，设置绘制模式为点线
        /// 3. 重置鼠标按下发送者
        /// 4. 如果是长按选中状态，处理相关逻辑
        /// 5. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawDotLine_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            if (lastMouseDownSender == sender)
            {
                forcePointEraser = false;
                DisableEraserOverlay();

                forceEraser = true;
                drawingShapeMode = 18;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                CancelSingleFingerDragMode();
            }

            lastMouseDownSender = null;
            if (isLongPressSelected)
            {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                if (sender is UIElement ui)
                {
                    var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                    ui.BeginAnimation(OpacityProperty, dA);
                }
            }

            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制箭头按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制箭头按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 如果是长按操作，设置绘制模式为箭头
        /// 3. 重置鼠标按下发送者
        /// 4. 如果是长按选中状态，处理相关逻辑
        /// 5. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawArrow_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            if (lastMouseDownSender == sender)
            {
                forcePointEraser = false;
                DisableEraserOverlay();

                forceEraser = true;
                drawingShapeMode = 2;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                CancelSingleFingerDragMode();
            }

            lastMouseDownSender = null;
            if (isLongPressSelected)
            {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                if (sender is UIElement ui)
                {
                    var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                    ui.BeginAnimation(OpacityProperty, dA);
                }
            }

            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制平行线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制平行线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 如果是长按操作，设置绘制模式为平行线
        /// 3. 重置鼠标按下发送者
        /// 4. 如果是长按选中状态，处理相关逻辑
        /// 5. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawParallelLine_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            if (lastMouseDownSender == sender)
            {
                forcePointEraser = false;
                DisableEraserOverlay();

                forceEraser = true;
                drawingShapeMode = 15;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                CancelSingleFingerDragMode();
            }

            lastMouseDownSender = null;
            if (isLongPressSelected)
            {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                if (sender is UIElement ui)
                {
                    var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                    ui.BeginAnimation(OpacityProperty, dA);
                }
            }

            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制坐标系1按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制坐标系1按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为坐标系1
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCoordinate1_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 11;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制坐标系2按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制坐标系2按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为坐标系2
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCoordinate2_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 12;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制坐标系3按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制坐标系3按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为坐标系3
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCoordinate3_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 13;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制坐标系4按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制坐标系4按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为坐标系4
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCoordinate4_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 14;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制坐标系5按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制坐标系5按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为坐标系5
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCoordinate5_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 17;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制矩形按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制矩形按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为矩形
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawRectangle_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 3;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制中心矩形按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制中心矩形按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为中心矩形
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawRectangleCenter_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 19;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制椭圆按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制椭圆按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为椭圆
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawEllipse_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 4;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制圆形按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制圆形按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为圆形
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCircle_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 5;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制中心椭圆按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制中心椭圆按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为中心椭圆
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCenterEllipse_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 16;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制带焦点的中心椭圆按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制带焦点的中心椭圆按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为带焦点的中心椭圆
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCenterEllipseWithFocalPoint_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 23;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制虚线圆形按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制虚线圆形按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为虚线圆形
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawDashedCircle_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 10;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制双曲线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制双曲线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为双曲线
        /// 6. 重置多步形状绘制当前步骤
        /// 7. 设置墨水画布编辑模式为无
        /// 8. 启用墨水画布的操作功能
        /// 9. 取消单指拖拽模式
        /// 10. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawHyperbola_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 24;
            drawMultiStepShapeCurrentStep = 0;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制带焦点的双曲线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制带焦点的双曲线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为带焦点的双曲线
        /// 6. 重置多步形状绘制当前步骤
        /// 7. 设置墨水画布编辑模式为无
        /// 8. 启用墨水画布的操作功能
        /// 9. 取消单指拖拽模式
        /// 10. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawHyperbolaWithFocalPoint_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 25;
            drawMultiStepShapeCurrentStep = 0;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制抛物线1按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制抛物线1按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为抛物线1
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawParabola1_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 20;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制带焦点的抛物线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制带焦点的抛物线按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为带焦点的抛物线
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawParabolaWithFocalPoint_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 22;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制抛物线2按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制抛物线2按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为抛物线2
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawParabola2_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 21;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制圆柱按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制圆柱按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为圆柱
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCylinder_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 6;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制圆锥按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制圆锥按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为圆锥
        /// 6. 设置墨水画布编辑模式为无
        /// 7. 启用墨水画布的操作功能
        /// 8. 取消单指拖拽模式
        /// 9. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCone_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 7;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        /// <summary>
        /// 处理绘制长方体按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当绘制长方体按钮被点击时：
        /// 1. 检查是否在多点触控模式下
        /// 2. 禁用点橡皮擦模式
        /// 3. 禁用橡皮擦覆盖
        /// 4. 设置强制橡皮擦模式
        /// 5. 设置绘制模式为长方体
        /// 6. 重置长方体绘制的首次触摸标志
        /// 7. 初始化长方体前方面矩形的起始点和结束点
        /// 8. 设置墨水画布编辑模式为无
        /// 9. 启用墨水画布的操作功能
        /// 10. 取消单指拖拽模式
        /// 11. 提示切换到画笔模式
        /// </remarks>
        private async void BtnDrawCuboid_Click(object sender, MouseButtonEventArgs e)
        {
            await CheckIsDrawingShapesInMultiTouchMode();
            forcePointEraser = false;
            DisableEraserOverlay();

            forceEraser = true;
            drawingShapeMode = 9;
            isFirstTouchCuboid = true;
            CuboidFrontRectIniP = new Point();
            CuboidFrontRectEndP = new Point();
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsManipulationEnabled = true;
            CancelSingleFingerDragMode();
            DrawShapePromptToPen();
        }

        #endregion

        /// <summary>
        /// 处理墨水画布的触摸移动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 当在墨水画布上发生触摸移动时：
        /// 1. 如果是单指拖拽模式，直接返回
        /// 2. 如果是形状绘制模式：
        ///    - 如果等待下一次触摸按下，直接返回
        ///    - 如果触摸点数量大于1，设置等待标志并清理临时笔画
        ///    - 确保墨水画布编辑模式为无
        /// 3. 调用MouseTouchMove方法处理触摸点位置
        /// </remarks>
        private void inkCanvas_TouchMove(object sender, TouchEventArgs e)
        {
            if (isSingleFingerDragMode) return;
            if (drawingShapeMode != 0)
            {
                //EraserContainer.Background = null;
                //ImageEraser.Visibility = Visibility.Visible;
                if (isWaitUntilNextTouchDown) return;
                if (dec.Count > 1)
                {
                    isWaitUntilNextTouchDown = true;
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    return;
                }

                if (inkCanvas.EditingMode != InkCanvasEditingMode.None)
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }

            MouseTouchMove(e.GetTouchPoint(inkCanvas).Position);
        }

        /// <summary>
        /// 多笔完成的图形 当前所处在的笔画步骤
        /// </summary>
        private int drawMultiStepShapeCurrentStep;

        /// <summary>
        /// 多笔完成的图形 当前所处在的笔画集合
        /// </summary>
        private StrokeCollection drawMultiStepShapeSpecialStrokeCollection = new StrokeCollection();

        //double drawMultiStepShapeSpecialParameter1 = 0.0; //多笔完成的图形 特殊参数 通常用于表示a
        //double drawMultiStepShapeSpecialParameter2 = 0.0; //多笔完成的图形 特殊参数 通常用于表示b

        /// <summary>
        /// 多笔完成的图形 特殊参数 通常用于表示k
        /// </summary>
        private double drawMultiStepShapeSpecialParameter3;

        #region 形状绘制主函数

        /// <summary>
        /// 处理鼠标或触摸移动事件，用于形状绘制
        /// </summary>
        /// <param name="endP">结束点坐标</param>
        /// <remarks>
        /// 当鼠标或触摸在画布上移动时：
        /// 1. 禁用原有的FitToCurve，使用新的高级贝塞尔曲线平滑
        /// 2. 在绘制过程中禁用浮动栏交互，避免干扰绘制
        /// 3. 根据当前的绘制模式，生成不同的形状：
        ///    - 直线
        ///    - 虚线
        ///    - 点线
        ///    - 箭头
        ///    - 平行线
        ///    - 各种坐标系
        ///    - 矩形
        ///    - 中心矩形
        ///    - 椭圆
        ///    - 圆形
        ///    - 中心椭圆
        ///    - 带焦点的中心椭圆
        ///    - 虚线圆形
        ///    - 双曲线
        ///    - 带焦点的双曲线
        ///    - 抛物线
        ///    - 带焦点的抛物线
        ///    - 圆柱
        ///    - 圆锥
        ///    - 长方体
        /// 4. 移除之前的临时笔画，添加新生成的笔画
        /// </remarks>
        private void MouseTouchMove(Point endP)
        {
            // 禁用原有的FitToCurve，使用新的高级贝塞尔曲线平滑
            if (Settings.Canvas.FitToCurve) drawingAttributes.FitToCurve = false;
            // 在绘制过程中禁用浮动栏交互，避免干扰绘制
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;
            List<Point> pointList;
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            var newIniP = iniP;
            switch (drawingShapeMode)
            {
                case 1:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    pointList = new List<Point> {
                        new Point(iniP.X, iniP.Y),
                        new Point(endP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 8:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateDashedLineStrokeCollection(iniP, endP));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 18:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateDotLineStrokeCollection(iniP, endP));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 2:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    double w = 15, h = 10;
                    var theta = Math.Atan2(iniP.Y - endP.Y, iniP.X - endP.X);
                    var sint = Math.Sin(theta);
                    var cost = Math.Cos(theta);

                    pointList = new List<Point> {
                        new Point(iniP.X, iniP.Y),
                        new Point(endP.X, endP.Y),
                        new Point(endP.X + (w * cost - h * sint), endP.Y + (w * sint + h * cost)),
                        new Point(endP.X, endP.Y),
                        new Point(endP.X + (w * cost + h * sint), endP.Y - (h * cost - w * sint))
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 15:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var d = GetDistance(iniP, endP);
                    if (d == 0) return;
                    var sinTheta = (iniP.Y - endP.Y) / d;
                    var cosTheta = (endP.X - iniP.X) / d;
                    var tanTheta = Math.Abs(sinTheta / cosTheta);
                    double x = 25;
                    if (Math.Abs(tanTheta) < 1.0 / 12)
                    {
                        sinTheta = 0;
                        cosTheta = 1;
                        endP.Y = iniP.Y;
                    }

                    if (tanTheta < 0.63 && tanTheta > 0.52) //30
                    {
                        sinTheta = sinTheta / Math.Abs(sinTheta) * 0.5;
                        cosTheta = cosTheta / Math.Abs(cosTheta) * 0.866;
                        endP.Y = iniP.Y - d * sinTheta;
                        endP.X = iniP.X + d * cosTheta;
                    }

                    if (tanTheta < 1.08 && tanTheta > 0.92) //45
                    {
                        sinTheta = sinTheta / Math.Abs(sinTheta) * 0.707;
                        cosTheta = cosTheta / Math.Abs(cosTheta) * 0.707;
                        endP.Y = iniP.Y - d * sinTheta;
                        endP.X = iniP.X + d * cosTheta;
                    }

                    if (tanTheta < 1.95 && tanTheta > 1.63) //60
                    {
                        sinTheta = sinTheta / Math.Abs(sinTheta) * 0.866;
                        cosTheta = cosTheta / Math.Abs(cosTheta) * 0.5;
                        endP.Y = iniP.Y - d * sinTheta;
                        endP.X = iniP.X + d * cosTheta;
                    }

                    if (Math.Abs(cosTheta / sinTheta) < 1.0 / 12)
                    {
                        endP.X = iniP.X;
                        sinTheta = 1;
                        cosTheta = 0;
                    }

                    strokes.Add(GenerateLineStroke(new Point(iniP.X - 3 * x * sinTheta, iniP.Y - 3 * x * cosTheta),
                        new Point(endP.X - 3 * x * sinTheta, endP.Y - 3 * x * cosTheta)));
                    strokes.Add(GenerateLineStroke(new Point(iniP.X - x * sinTheta, iniP.Y - x * cosTheta),
                        new Point(endP.X - x * sinTheta, endP.Y - x * cosTheta)));
                    strokes.Add(GenerateLineStroke(new Point(iniP.X + x * sinTheta, iniP.Y + x * cosTheta),
                        new Point(endP.X + x * sinTheta, endP.Y + x * cosTheta)));
                    strokes.Add(GenerateLineStroke(new Point(iniP.X + 3 * x * sinTheta, iniP.Y + 3 * x * cosTheta),
                        new Point(endP.X + 3 * x * sinTheta, endP.Y + 3 * x * cosTheta)));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 11:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateArrowLineStroke(new Point(2 * iniP.X - (endP.X - 20), iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, 2 * iniP.Y - (endP.Y + 20)),
                        new Point(iniP.X, endP.Y)));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 12:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (Math.Abs(iniP.X - endP.X) < 0.01) return;
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X + (iniP.X - endP.X) / Math.Abs(iniP.X - endP.X) * 25, iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, 2 * iniP.Y - (endP.Y + 20)),
                        new Point(iniP.X, endP.Y)));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 13:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    strokes.Add(GenerateArrowLineStroke(new Point(2 * iniP.X - (endP.X - 20), iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X, iniP.Y + (iniP.Y - endP.Y) / Math.Abs(iniP.Y - endP.Y) * 25),
                        new Point(iniP.X, endP.Y)));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 14:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X + (iniP.X - endP.X) / Math.Abs(iniP.X - endP.X) * 25, iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X, iniP.Y + (iniP.Y - endP.Y) / Math.Abs(iniP.Y - endP.Y) * 25),
                        new Point(iniP.X, endP.Y)));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 17:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, iniP.Y),
                        new Point(iniP.X + Math.Abs(endP.X - iniP.X), iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, iniP.Y),
                        new Point(iniP.X, iniP.Y - Math.Abs(endP.Y - iniP.Y))));
                    d = (Math.Abs(iniP.X - endP.X) + Math.Abs(iniP.Y - endP.Y)) / 2;
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, iniP.Y),
                        new Point(iniP.X - d / 1.76, iniP.Y + d / 1.76)));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 3:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    pointList = new List<Point> {
                        new Point(iniP.X, iniP.Y),
                        new Point(iniP.X, endP.Y),
                        new Point(endP.X, endP.Y),
                        new Point(endP.X, iniP.Y),
                        new Point(iniP.X, iniP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 19:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var a = iniP.X - endP.X;
                    var b = iniP.Y - endP.Y;
                    pointList = new List<Point> {
                        new Point(iniP.X - a, iniP.Y - b),
                        new Point(iniP.X - a, iniP.Y + b),
                        new Point(iniP.X + a, iniP.Y + b),
                        new Point(iniP.X + a, iniP.Y - b),
                        new Point(iniP.X - a, iniP.Y - b)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 4:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    pointList = GenerateEllipseGeometry(iniP, endP);
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 5:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var R = GetDistance(iniP, endP);
                    pointList = GenerateEllipseGeometry(new Point(iniP.X - R, iniP.Y - R),
                        new Point(iniP.X + R, iniP.Y + R));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);

                    // 如果启用了圆心标记功能，则绘制圆心
                    if (Settings.Canvas.ShowCircleCenter)
                    {
                        DrawCircleCenter(iniP);
                    }
                    break;
                case 16:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var halfA = endP.X - iniP.X;
                    var halfB = endP.Y - iniP.Y;
                    pointList = GenerateEllipseGeometry(new Point(iniP.X - halfA, iniP.Y - halfB),
                        new Point(iniP.X + halfA, iniP.Y + halfB));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 23:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    a = Math.Abs(endP.X - iniP.X);
                    b = Math.Abs(endP.Y - iniP.Y);
                    pointList = GenerateEllipseGeometry(new Point(iniP.X - a, iniP.Y - b),
                        new Point(iniP.X + a, iniP.Y + b));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke);
                    var c = Math.Sqrt(Math.Abs(a * a - b * b));
                    StylusPoint stylusPoint;
                    if (a > b)
                    {
                        stylusPoint = new StylusPoint(iniP.X + c, iniP.Y, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point)
                        {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                        stylusPoint = new StylusPoint(iniP.X - c, iniP.Y, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point)
                        {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                    }
                    else if (a < b)
                    {
                        stylusPoint = new StylusPoint(iniP.X, iniP.Y - c, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point)
                        {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                        stylusPoint = new StylusPoint(iniP.X, iniP.Y + c, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point)
                        {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                    }

                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 10:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    R = GetDistance(iniP, endP);
                    strokes = GenerateDashedLineEllipseStrokeCollection(new Point(iniP.X - R, iniP.Y - R),
                        new Point(iniP.X + R, iniP.Y + R));
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 24:
                case 25:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //双曲线 x^2/a^2 - y^2/b^2 = 1
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    var pointList2 = new List<Point>();
                    var pointList3 = new List<Point>();
                    var pointList4 = new List<Point>();
                    if (drawMultiStepShapeCurrentStep == 0)
                    {
                        //第一笔：画渐近线
                        var k = Math.Abs((endP.Y - iniP.Y) / (endP.X - iniP.X));
                        strokes.Add(
                            GenerateDashedLineStrokeCollection(new Point(2 * iniP.X - endP.X, 2 * iniP.Y - endP.Y),
                                endP));
                        strokes.Add(GenerateDashedLineStrokeCollection(new Point(2 * iniP.X - endP.X, endP.Y),
                                new Point(endP.X, 2 * iniP.Y - endP.Y)));
                        drawMultiStepShapeSpecialParameter3 = k;
                        drawMultiStepShapeSpecialStrokeCollection = strokes;
                    }
                    else
                    {
                        //第二笔：画双曲线
                        // 先将第一笔的渐近线添加到strokes中
                        if (drawMultiStepShapeSpecialStrokeCollection != null && drawMultiStepShapeSpecialStrokeCollection.Count > 0)
                        {
                            foreach (var asymptoteStroke in drawMultiStepShapeSpecialStrokeCollection)
                            {
                                strokes.Add(asymptoteStroke.Clone());
                            }
                        }

                        var k = drawMultiStepShapeSpecialParameter3;
                        var isHyperbolaFocalPointOnXAxis = Math.Abs((endP.Y - iniP.Y) / (endP.X - iniP.X)) < k;
                        if (isHyperbolaFocalPointOnXAxis)
                        {
                            // 焦点在 x 轴上
                            a = Math.Sqrt(Math.Abs((endP.X - iniP.X) * (endP.X - iniP.X) -
                                                   (endP.Y - iniP.Y) * (endP.Y - iniP.Y) / (k * k)));
                            b = a * k;
                            pointList = new List<Point>();
                            for (var i = a; i <= Math.Abs(endP.X - iniP.X); i += 0.5)
                            {
                                var rY = Math.Sqrt(Math.Abs(k * k * i * i - b * b));
                                pointList.Add(new Point(iniP.X + i, iniP.Y - rY));
                                pointList2.Add(new Point(iniP.X + i, iniP.Y + rY));
                                pointList3.Add(new Point(iniP.X - i, iniP.Y - rY));
                                pointList4.Add(new Point(iniP.X - i, iniP.Y + rY));
                            }
                        }
                        else
                        {
                            // 焦点在 y 轴上
                            a = Math.Sqrt(Math.Abs((endP.Y - iniP.Y) * (endP.Y - iniP.Y) -
                                                   (endP.X - iniP.X) * (endP.X - iniP.X) * (k * k)));
                            b = a / k;
                            pointList = new List<Point>();
                            for (var i = a; i <= Math.Abs(endP.Y - iniP.Y); i += 0.5)
                            {
                                var rX = Math.Sqrt(Math.Abs(i * i / k / k - b * b));
                                pointList.Add(new Point(iniP.X - rX, iniP.Y + i));
                                pointList2.Add(new Point(iniP.X + rX, iniP.Y + i));
                                pointList3.Add(new Point(iniP.X - rX, iniP.Y - i));
                                pointList4.Add(new Point(iniP.X + rX, iniP.Y - i));
                            }
                        }

                        try
                        {
                            point = new StylusPointCollection(pointList);
                            stroke = new Stroke(point)
                            { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            point = new StylusPointCollection(pointList2);
                            stroke = new Stroke(point)
                            { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            point = new StylusPointCollection(pointList3);
                            stroke = new Stroke(point)
                            { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            point = new StylusPointCollection(pointList4);
                            stroke = new Stroke(point)
                            { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            if (drawingShapeMode == 25)
                            {
                                //画焦点
                                c = Math.Sqrt(a * a + b * b);
                                stylusPoint = isHyperbolaFocalPointOnXAxis
                                    ? new StylusPoint(iniP.X + c, iniP.Y, (float)1.0)
                                    : new StylusPoint(iniP.X, iniP.Y + c, (float)1.0);
                                point = new StylusPointCollection();
                                point.Add(stylusPoint);
                                stroke = new Stroke(point)
                                { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                                strokes.Add(stroke.Clone());
                                stylusPoint = isHyperbolaFocalPointOnXAxis
                                    ? new StylusPoint(iniP.X - c, iniP.Y, (float)1.0)
                                    : new StylusPoint(iniP.X, iniP.Y - c, (float)1.0);
                                point = new StylusPointCollection();
                                point.Add(stylusPoint);
                                stroke = new Stroke(point)
                                { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                                strokes.Add(stroke.Clone());
                            }
                        }
                        catch
                        {
                            return;
                        }
                    }

                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 20:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //抛物线 y=ax^2
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    a = (iniP.Y - endP.Y) / ((iniP.X - endP.X) * (iniP.X - endP.X));
                    pointList = new List<Point>();
                    pointList2 = new List<Point>();
                    for (var i = 0.0; i <= Math.Abs(endP.X - iniP.X); i += 0.5)
                    {
                        pointList.Add(new Point(iniP.X + i, iniP.Y - a * i * i));
                        pointList2.Add(new Point(iniP.X - i, iniP.Y - a * i * i));
                    }

                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    point = new StylusPointCollection(pointList2);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 21:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //抛物线 y^2=ax
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    a = (iniP.X - endP.X) / ((iniP.Y - endP.Y) * (iniP.Y - endP.Y));
                    pointList = new List<Point>();
                    pointList2 = new List<Point>();
                    for (var i = 0.0; i <= Math.Abs(endP.Y - iniP.Y); i += 0.5)
                    {
                        pointList.Add(new Point(iniP.X - a * i * i, iniP.Y + i));
                        pointList2.Add(new Point(iniP.X - a * i * i, iniP.Y - i));
                    }

                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    point = new StylusPointCollection(pointList2);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 22:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //抛物线 y^2=ax, 含焦点
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    var p = (iniP.Y - endP.Y) * (iniP.Y - endP.Y) / (2 * (iniP.X - endP.X));
                    a = 0.5 / p;
                    pointList = new List<Point>();
                    pointList2 = new List<Point>();
                    for (var i = 0.0; i <= Math.Abs(endP.Y - iniP.Y); i += 0.5)
                    {
                        pointList.Add(new Point(iniP.X - a * i * i, iniP.Y + i));
                        pointList2.Add(new Point(iniP.X - a * i * i, iniP.Y - i));
                    }

                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    point = new StylusPointCollection(pointList2);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    stylusPoint = new StylusPoint(iniP.X - p / 2, iniP.Y, (float)1.0);
                    point = new StylusPointCollection();
                    point.Add(stylusPoint);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 6:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    newIniP = iniP;
                    if (iniP.Y > endP.Y)
                    {
                        newIniP = new Point(iniP.X, endP.Y);
                        endP = new Point(endP.X, iniP.Y);
                    }

                    var topA = Math.Abs(newIniP.X - endP.X);
                    var topB = topA / 2.646;
                    //顶部椭圆
                    pointList = GenerateEllipseGeometry(new Point(newIniP.X, newIniP.Y - topB / 2),
                        new Point(endP.X, newIniP.Y + topB / 2));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    //底部椭圆
                    pointList = GenerateEllipseGeometry(new Point(newIniP.X, endP.Y - topB / 2),
                        new Point(endP.X, endP.Y + topB / 2), false);
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    strokes.Add(GenerateDashedLineEllipseStrokeCollection(new Point(newIniP.X, endP.Y - topB / 2),
                        new Point(endP.X, endP.Y + topB / 2), true, false));
                    //左侧
                    pointList = new List<Point> {
                        new Point(newIniP.X, newIniP.Y),
                        new Point(newIniP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    //右侧
                    pointList = new List<Point> {
                        new Point(endP.X, newIniP.Y),
                        new Point(endP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 7:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (iniP.Y > endP.Y)
                    {
                        newIniP = new Point(iniP.X, endP.Y);
                        endP = new Point(endP.X, iniP.Y);
                    }

                    var bottomA = Math.Abs(newIniP.X - endP.X);
                    var bottomB = bottomA / 2.646;
                    //底部椭圆
                    pointList = GenerateEllipseGeometry(new Point(newIniP.X, endP.Y - bottomB / 2),
                        new Point(endP.X, endP.Y + bottomB / 2), false);
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    strokes.Add(GenerateDashedLineEllipseStrokeCollection(new Point(newIniP.X, endP.Y - bottomB / 2),
                        new Point(endP.X, endP.Y + bottomB / 2), true, false));
                    //左侧
                    pointList = new List<Point> {
                        new Point((newIniP.X + endP.X) / 2, newIniP.Y),
                        new Point(newIniP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    //右侧
                    pointList = new List<Point> {
                        new Point((newIniP.X + endP.X) / 2, newIniP.Y),
                        new Point(endP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try
                    {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 9:
                    // 画长方体
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (isFirstTouchCuboid)
                    {
                        //分开画线条方便后期单独擦除某一条棱
                        strokes.Add(GenerateLineStroke(new Point(iniP.X, iniP.Y), new Point(iniP.X, endP.Y)));
                        strokes.Add(GenerateLineStroke(new Point(iniP.X, endP.Y), new Point(endP.X, endP.Y)));
                        strokes.Add(GenerateLineStroke(new Point(endP.X, endP.Y), new Point(endP.X, iniP.Y)));
                        strokes.Add(GenerateLineStroke(new Point(iniP.X, iniP.Y), new Point(endP.X, iniP.Y)));
                        try
                        {
                            inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                        }
                        catch
                        {
                            Trace.WriteLine("lastTempStrokeCollection failed.");
                        }

                        lastTempStrokeCollection = strokes;
                        inkCanvas.Strokes.Add(strokes);
                        CuboidFrontRectIniP = iniP;
                        CuboidFrontRectEndP = endP;
                    }
                    else
                    {
                        d = CuboidFrontRectIniP.Y - endP.Y;
                        if (d < 0) d = -d; //就是懒不想做反向的，不要让我去做，想做自己做好之后 Pull Request
                        a = CuboidFrontRectEndP.X - CuboidFrontRectIniP.X; //正面矩形长
                        b = CuboidFrontRectEndP.Y - CuboidFrontRectIniP.Y; //正面矩形宽

                        //横上
                        var newLineIniP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectIniP.Y - d);
                        var newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectIniP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //横下 (虚线)
                        newLineIniP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectEndP.Y - d);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectEndP.Y - d);
                        strokes.Add(GenerateDashedLineStrokeCollection(newLineIniP, newLineEndP));
                        //斜左上
                        newLineIniP = new Point(CuboidFrontRectIniP.X, CuboidFrontRectIniP.Y);
                        newLineEndP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectIniP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //斜右上
                        newLineIniP = new Point(CuboidFrontRectEndP.X, CuboidFrontRectIniP.Y);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectIniP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //斜左下 (虚线)
                        newLineIniP = new Point(CuboidFrontRectIniP.X, CuboidFrontRectEndP.Y);
                        newLineEndP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectEndP.Y - d);
                        strokes.Add(GenerateDashedLineStrokeCollection(newLineIniP, newLineEndP));
                        //斜右下
                        newLineIniP = new Point(CuboidFrontRectEndP.X, CuboidFrontRectEndP.Y);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectEndP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //竖左 (虚线)
                        newLineIniP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectIniP.Y - d);
                        newLineEndP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectEndP.Y - d);
                        strokes.Add(GenerateDashedLineStrokeCollection(newLineIniP, newLineEndP));
                        //竖右
                        newLineIniP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectIniP.Y - d);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectEndP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());

                        try
                        {
                            inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                        }
                        catch
                        {
                            Trace.WriteLine("lastTempStrokeCollection failed.");
                        }

                        lastTempStrokeCollection = strokes;
                        inkCanvas.Strokes.Add(strokes);
                    }

                    break;
            }
        }

        #endregion

        /// <summary>
        /// 长方体绘制的首次触摸标志
        /// </summary>
        /// <remarks>
        /// 用于标识是否是长方体绘制的第一次触摸，第一次触摸绘制正面矩形，第二次触摸绘制深度
        /// </remarks>
        private bool isFirstTouchCuboid = true;

        /// <summary>
        /// 长方体正面矩形的起始点
        /// </summary>
        private Point CuboidFrontRectIniP;

        /// <summary>
        /// 长方体正面矩形的结束点
        /// </summary>
        private Point CuboidFrontRectEndP;

        /// <summary>
        /// 上一次的临时笔画
        /// </summary>
        /// <remarks>
        /// 用于存储当前正在绘制的临时笔画，在绘制过程中实时更新
        /// </remarks>
        private Stroke lastTempStroke;

        /// <summary>
        /// 上一次的临时笔画集合
        /// </summary>
        /// <remarks>
        /// 用于存储当前正在绘制的临时笔画集合，在绘制复杂形状时使用
        /// </remarks>
        private StrokeCollection lastTempStrokeCollection = new StrokeCollection();

        /// <summary>
        /// 是否等待下一次触摸按下
        /// </summary>
        /// <remarks>
        /// 当触摸点数量大于1时，设置此标志并清理临时笔画
        /// </remarks>
        private bool isWaitUntilNextTouchDown;

        // 添加节流机制，减少更新频率
        /// <summary>
        /// 上一次更新时间
        /// </summary>
        private DateTime lastUpdateTime = DateTime.MinValue;

        /// <summary>
        /// 更新节流时间（毫秒）
        /// </summary>
        /// <remarks>
        /// 约60fps的更新频率，用于限制UI更新频率，提高性能
        /// </remarks>
        private const int UpdateThrottleMs = 16;

        /// <summary>
        /// 安全地更新临时笔画，减少预览闪烁
        /// </summary>
        /// <param name="newStroke">新的临时笔画</param>
        private void UpdateTempStrokeSafely(Stroke newStroke)
        {
            // 节流机制：限制更新频率
            var now = DateTime.Now;
            if ((now - lastUpdateTime).TotalMilliseconds < UpdateThrottleMs)
            {
                return;
            }
            lastUpdateTime = now;

            try
            {
                // 使用Dispatcher.BeginInvoke确保UI更新在UI线程上执行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 先添加新笔画，再删除旧笔画，减少视觉闪烁
                        inkCanvas.Strokes.Add(newStroke);

                        if (lastTempStroke != null && inkCanvas.Strokes.Contains(lastTempStroke))
                        {
                            inkCanvas.Strokes.Remove(lastTempStroke);
                        }

                        lastTempStroke = newStroke;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateTempStrokeSafely 失败: {ex.Message}");
                        // 如果更新失败，确保清理状态
                        if (lastTempStroke != null && inkCanvas.Strokes.Contains(lastTempStroke))
                        {
                            try { inkCanvas.Strokes.Remove(lastTempStroke); } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                        }
                        lastTempStroke = newStroke;
                        try { inkCanvas.Strokes.Add(newStroke); } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                    }
                }), DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateTempStrokeSafely Dispatcher 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全地更新临时笔画集合，减少预览闪烁
        /// </summary>
        /// <param name="newStrokeCollection">新的临时笔画集合</param>
        private void UpdateTempStrokeCollectionSafely(StrokeCollection newStrokeCollection)
        {
            // 节流机制：限制更新频率
            var now = DateTime.Now;
            if ((now - lastUpdateTime).TotalMilliseconds < UpdateThrottleMs)
            {
                return;
            }
            lastUpdateTime = now;

            try
            {
                // 使用Dispatcher.BeginInvoke确保UI更新在UI线程上执行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 先添加新笔画集合，再删除旧笔画集合，减少视觉闪烁
                        inkCanvas.Strokes.Add(newStrokeCollection);

                        if (lastTempStrokeCollection != null && lastTempStrokeCollection.Count > 0)
                        {
                            foreach (var stroke in lastTempStrokeCollection)
                            {
                                if (inkCanvas.Strokes.Contains(stroke))
                                {
                                    inkCanvas.Strokes.Remove(stroke);
                                }
                            }
                        }

                        lastTempStrokeCollection = newStrokeCollection;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateTempStrokeCollectionSafely 失败: {ex.Message}");
                        // 如果更新失败，确保清理状态
                        if (lastTempStrokeCollection != null && lastTempStrokeCollection.Count > 0)
                        {
                            foreach (var stroke in lastTempStrokeCollection)
                            {
                                try { inkCanvas.Strokes.Remove(stroke); } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                            }
                        }
                        lastTempStrokeCollection = newStrokeCollection;
                        try { inkCanvas.Strokes.Add(newStrokeCollection); } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                    }
                }), DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateTempStrokeCollectionSafely Dispatcher 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成椭圆几何图形的点列表
        /// </summary>
        /// <param name="st">起始点坐标</param>
        /// <param name="ed">结束点坐标</param>
        /// <param name="isDrawTop">是否绘制上半部分</param>
        /// <param name="isDrawBottom">是否绘制下半部分</param>
        /// <returns>返回椭圆的点列表</returns>
        /// <remarks>
        /// 根据给定的起始点和结束点，生成椭圆的点列表：
        /// 1. 计算椭圆的半长轴和半短轴
        /// 2. 根据参数决定是否绘制上半部分和下半部分
        /// 3. 使用参数方程生成椭圆上的点
        /// </remarks>
        private List<Point> GenerateEllipseGeometry(Point st, Point ed, bool isDrawTop = true,
            bool isDrawBottom = true)
        {
            var a = 0.5 * (ed.X - st.X);
            var b = 0.5 * (ed.Y - st.Y);
            var pointList = new List<Point>();
            if (isDrawTop && isDrawBottom)
            {
                for (double r = 0; r <= 2 * Math.PI; r = r + 0.01)
                    pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                        0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
            }
            else
            {
                if (isDrawBottom)
                    for (double r = 0; r <= Math.PI; r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
                if (isDrawTop)
                    for (var r = Math.PI; r <= 2 * Math.PI; r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
            }

            return pointList;
        }

        /// <summary>
        /// 生成虚线椭圆的笔画集合
        /// </summary>
        /// <param name="st">起始点坐标</param>
        /// <param name="ed">结束点坐标</param>
        /// <param name="isDrawTop">是否绘制上半部分</param>
        /// <param name="isDrawBottom">是否绘制下半部分</param>
        /// <returns>返回虚线椭圆的笔画集合</returns>
        /// <remarks>
        /// 根据给定的起始点和结束点，生成虚线椭圆的笔画集合：
        /// 1. 计算椭圆的半长轴和半短轴
        /// 2. 根据参数决定是否绘制上半部分和下半部分
        /// 3. 使用参数方程生成椭圆上的点，并将其分割为虚线段
        /// 4. 为每个虚线段创建一个笔画
        /// </remarks>
        private StrokeCollection GenerateDashedLineEllipseStrokeCollection(Point st, Point ed, bool isDrawTop = true,
            bool isDrawBottom = true)
        {
            var a = 0.5 * (ed.X - st.X);
            var b = 0.5 * (ed.Y - st.Y);
            var step = 0.05;
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            if (isDrawBottom)
                for (var i = 0.0; i < 1.0; i += step * 1.66)
                {
                    pointList = new List<Point>();
                    for (var r = Math.PI * i; r <= Math.PI * (i + step); r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                }

            if (isDrawTop)
                for (var i = 1.0; i < 2.0; i += step * 1.66)
                {
                    pointList = new List<Point>();
                    for (var r = Math.PI * i; r <= Math.PI * (i + step); r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point)
                    {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                }

            return strokes;
        }

        /// <summary>
        /// 生成直线笔画
        /// </summary>
        /// <param name="st">起始点坐标</param>
        /// <param name="ed">结束点坐标</param>
        /// <returns>返回直线笔画</returns>
        /// <remarks>
        /// 根据给定的起始点和结束点，生成直线笔画：
        /// 1. 创建包含起始点和结束点的点列表
        /// 2. 将点列表转换为StylusPointCollection
        /// 3. 创建并返回带有默认绘图属性的笔画
        /// </remarks>
        private Stroke GenerateLineStroke(Point st, Point ed)
        {
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            pointList = new List<Point> {
                new Point(st.X, st.Y),
                new Point(ed.X, ed.Y)
            };
            point = new StylusPointCollection(pointList);
            stroke = new Stroke(point)
            {
                DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
            };
            return stroke;
        }

        /// <summary>
        /// 生成带箭头的直线笔画
        /// </summary>
        /// <param name="st">起始点坐标</param>
        /// <param name="ed">结束点坐标</param>
        /// <returns>返回带箭头的直线笔画</returns>
        /// <remarks>
        /// 根据给定的起始点和结束点，生成带箭头的直线笔画：
        /// 1. 计算箭头的角度和方向
        /// 2. 创建包含起始点、结束点和箭头尖端的点列表
        /// 3. 将点列表转换为StylusPointCollection
        /// 4. 创建并返回带有默认绘图属性的笔画
        /// </remarks>
        private Stroke GenerateArrowLineStroke(Point st, Point ed)
        {
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;

            double w = 20, h = 7;
            var theta = Math.Atan2(st.Y - ed.Y, st.X - ed.X);
            var sint = Math.Sin(theta);
            var cost = Math.Cos(theta);

            pointList = new List<Point> {
                new Point(st.X, st.Y),
                new Point(ed.X, ed.Y),
                new Point(ed.X + (w * cost - h * sint), ed.Y + (w * sint + h * cost)),
                new Point(ed.X, ed.Y),
                new Point(ed.X + (w * cost + h * sint), ed.Y - (h * cost - w * sint))
            };
            point = new StylusPointCollection(pointList);
            stroke = new Stroke(point)
            {
                DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
            };
            return stroke;
        }


        /// <summary>
        /// 生成虚线笔画集合
        /// </summary>
        /// <param name="st">起始点坐标</param>
        /// <param name="ed">结束点坐标</param>
        /// <returns>返回虚线笔画集合</returns>
        /// <remarks>
        /// 根据给定的起始点和结束点，生成虚线笔画集合：
        /// 1. 计算两点之间的距离和方向
        /// 2. 按照指定的步长将直线分割为虚线段
        /// 3. 为每个虚线段创建一个笔画
        /// 4. 返回包含所有虚线段笔画的集合
        /// </remarks>
        private StrokeCollection GenerateDashedLineStrokeCollection(Point st, Point ed)
        {
            double step = 5;
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            var d = GetDistance(st, ed);
            var sinTheta = (ed.Y - st.Y) / d;
            var cosTheta = (ed.X - st.X) / d;
            for (var i = 0.0; i < d; i += step * 2.76)
            {
                pointList = new List<Point> {
                    new Point(st.X + i * cosTheta, st.Y + i * sinTheta),
                    new Point(st.X + Math.Min(i + step, d) * cosTheta, st.Y + Math.Min(i + step, d) * sinTheta)
                };
                point = new StylusPointCollection(pointList);
                stroke = new Stroke(point)
                {
                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                };
                strokes.Add(stroke.Clone());
            }

            return strokes;
        }

        /// <summary>
        /// 生成点线笔画集合
        /// </summary>
        /// <param name="st">起始点坐标</param>
        /// <param name="ed">结束点坐标</param>
        /// <returns>返回点线笔画集合</returns>
        /// <remarks>
        /// 根据给定的起始点和结束点，生成点线笔画集合：
        /// 1. 计算两点之间的距离和方向
        /// 2. 按照指定的步长在直线上生成点
        /// 3. 为每个点创建一个笔画
        /// 4. 返回包含所有点笔画的集合
        /// </remarks>
        private StrokeCollection GenerateDotLineStrokeCollection(Point st, Point ed)
        {
            double step = 3;
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            var d = GetDistance(st, ed);
            var sinTheta = (ed.Y - st.Y) / d;
            var cosTheta = (ed.X - st.X) / d;
            for (var i = 0.0; i < d; i += step * 2.76)
            {
                var stylusPoint = new StylusPoint(st.X + i * cosTheta, st.Y + i * sinTheta, (float)0.8);
                point = new StylusPointCollection();
                point.Add(stylusPoint);
                stroke = new Stroke(point)
                {
                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                };
                strokes.Add(stroke.Clone());
            }

            return strokes;
        }

        /// <summary>
        /// 鼠标按下状态标志
        /// </summary>
        /// <remarks>
        /// 用于标识鼠标是否处于按下状态，在绘制过程中使用
        /// </remarks>
        private bool isMouseDown;

        /// <summary>
        /// 触摸按下状态标志
        /// </summary>
        /// <remarks>
        /// 用于标识触摸是否处于按下状态，在绘制过程中使用
        /// </remarks>
        private bool isTouchDown;

        /// <summary>
        /// 处理墨水画布的鼠标按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当在墨水画布上按下鼠标时：
        /// 1. 捕获鼠标输入
        /// 2. 禁用浮动栏和黑板UI的命中测试，避免干扰绘制
        /// 3. 设置鼠标按下状态标志
        /// 4. 如果需要更新起始点，则更新起始点为当前鼠标位置
        /// </remarks>
        private void inkCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            inkCanvas.CaptureMouse();
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            isMouseDown = true;
            if (NeedUpdateIniP()) iniP = e.GetPosition(inkCanvas);
        }

        /// <summary>
        /// 处理墨水画布的鼠标移动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        /// <remarks>
        /// 当在墨水画布上移动鼠标时：
        /// 1. 如果鼠标处于按下状态，调用MouseTouchMove方法处理移动
        /// 2. 如果启用了光标显示，根据编辑模式设置光标
        /// </remarks>
        private void inkCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown) MouseTouchMove(e.GetPosition(inkCanvas));

            if (Settings.Canvas.IsShowCursor)
            {
                SetCursorBasedOnEditingMode(inkCanvas);
            }
        }

        /// <summary>
        /// 处理墨水画布的鼠标抬起事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 当在墨水画布上抬起鼠标时：
        /// 1. 释放鼠标捕获
        /// 2. 启用浮动栏和黑板UI的命中测试
        /// 3. 根据不同的绘制模式进行处理：
        ///    - 圆形模式：创建圆形对象并添加到圆形集合
        ///    - 长方体模式：处理长方体绘制的两个步骤
        ///    - 双曲线模式：处理多步绘制的步骤切换
        /// 4. 还原到笔模式（除特殊情况外）
        /// 5. 提交笔画历史记录
        /// 6. 清理临时笔画和状态
        /// 7. 恢复FitToCurve设置
        /// </remarks>
        private void inkCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HandleEraserOperationEnded();
            inkCanvas.ReleaseMouseCapture();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;

            if (drawingShapeMode == 5)
            {
                if (lastTempStroke != null)
                {
                    var circle = new Circle(new Point(), 0, lastTempStroke);
                    circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                        circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                    circle.Centroid = new Point(
                        (circle.Stroke.StylusPoints[0].X +
                         circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                        (circle.Stroke.StylusPoints[0].Y +
                         circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                    circles.Add(circle);
                }

                if (lastIsInMultiTouchMode)
                {
                    ToggleSwitchEnableMultiTouchMode.IsOn = true;
                    lastIsInMultiTouchMode = false;
                }
            }

            if (drawingShapeMode != 9 && drawingShapeMode != 0 && drawingShapeMode != 24 && drawingShapeMode != 25)
            {
                if (isLongPressSelected) { }
                else
                {
                    BtnPen_Click(null, null); //画完一次还原到笔模式
                    if (lastIsInMultiTouchMode)
                    {
                        ToggleSwitchEnableMultiTouchMode.IsOn = true;
                        lastIsInMultiTouchMode = false;
                    }
                }
            }

            if (drawingShapeMode == 9)
            {
                if (isFirstTouchCuboid)
                {
                    if (CuboidStrokeCollection == null) CuboidStrokeCollection = new StrokeCollection();
                    isFirstTouchCuboid = false;
                    var newIniP = new Point(Math.Min(CuboidFrontRectIniP.X, CuboidFrontRectEndP.X),
                        Math.Min(CuboidFrontRectIniP.Y, CuboidFrontRectEndP.Y));
                    var newEndP = new Point(Math.Max(CuboidFrontRectIniP.X, CuboidFrontRectEndP.X),
                        Math.Max(CuboidFrontRectIniP.Y, CuboidFrontRectEndP.Y));
                    CuboidFrontRectIniP = newIniP;
                    CuboidFrontRectEndP = newEndP;
                    try
                    {
                        CuboidStrokeCollection.Add(lastTempStrokeCollection);
                    }
                    catch
                    {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }
                }
                else
                {
                    BtnPen_Click(null, null); //画完还原到笔模式
                    if (lastIsInMultiTouchMode)
                    {
                        ToggleSwitchEnableMultiTouchMode.IsOn = true;
                        lastIsInMultiTouchMode = false;
                    }

                    if (_currentCommitType == CommitReason.ShapeDrawing)
                    {
                        try
                        {
                            CuboidStrokeCollection.Add(lastTempStrokeCollection);
                        }
                        catch
                        {
                            Trace.WriteLine("lastTempStrokeCollection failed.");
                        }

                        _currentCommitType = CommitReason.UserInput;
                        timeMachine.CommitStrokeUserInputHistory(CuboidStrokeCollection);
                        CuboidStrokeCollection = null;
                    }
                }
            }

            if (drawingShapeMode == 24 || drawingShapeMode == 25)
            {
                if (drawMultiStepShapeCurrentStep == 0)
                {
                    drawMultiStepShapeCurrentStep = 1;
                }
                else
                {
                    drawMultiStepShapeCurrentStep = 0;
                    if (drawMultiStepShapeSpecialStrokeCollection != null)
                    {
                        var opFlag = false;
                        switch (Settings.Canvas.HyperbolaAsymptoteOption)
                        {
                            case OptionalOperation.Yes:
                                opFlag = true;
                                break;
                            case OptionalOperation.No:
                                opFlag = false;
                                break;
                            case OptionalOperation.Ask:
                                opFlag = MessageBox.Show("是否移除渐近线？", "Ink Canvas", MessageBoxButton.YesNo) !=
                                         MessageBoxResult.Yes;
                                break;
                        }

                        ;
                        if (!opFlag) inkCanvas.Strokes.Remove(drawMultiStepShapeSpecialStrokeCollection);
                    }

                    BtnPen_Click(null, null); //画完还原到笔模式
                    if (lastIsInMultiTouchMode)
                    {
                        ToggleSwitchEnableMultiTouchMode.IsOn = true;
                        lastIsInMultiTouchMode = false;
                    }
                }
            }

            isMouseDown = false;
            if (ReplacedStroke != null || AddedStroke != null)
            {
                timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                AddedStroke = null;
                ReplacedStroke = null;
            }

            if (_currentCommitType == CommitReason.ShapeDrawing && drawingShapeMode != 9)
            {
                _currentCommitType = CommitReason.UserInput;
                StrokeCollection collection = null;
                if (lastTempStrokeCollection != null && lastTempStrokeCollection.Count > 0)
                    collection = lastTempStrokeCollection;
                else if (lastTempStroke != null) collection = new StrokeCollection { lastTempStroke };
                if (collection != null) timeMachine.CommitStrokeUserInputHistory(collection);
            }

            lastTempStroke = null;
            lastTempStrokeCollection = null;

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

            if (Settings.Canvas.FitToCurve == true) drawingAttributes.FitToCurve = true;
        }

        /// <summary>
        /// 检查是否需要更新起始点
        /// </summary>
        /// <returns>返回是否需要更新起始点</returns>
        /// <remarks>
        /// 检查当前绘制模式和步骤，判断是否需要更新起始点：
        /// 1. 对于双曲线模式（24和25），如果是第二笔（步骤1），则不更新起点
        /// 2. 其他情况都需要更新起点
        /// </remarks>
        private bool NeedUpdateIniP()
        {
            if (drawingShapeMode == 24 || drawingShapeMode == 25)
            {
                if (drawMultiStepShapeCurrentStep == 1)
                    return false; // 第二笔不更新起点
            }
            return true;
        }
        /// <summary>
        /// 绘制圆心标记
        /// </summary>
        /// <param name="centerPoint">圆心位置</param>
        private void DrawCircleCenter(Point centerPoint)
        {
            try
            {
                // 创建一个点作为圆心标记
                var centerSize = 0.5; // 圆心标记的大小

                // 创建一个小圆作为圆心标记
                var circlePoints = new List<Point>();
                for (double angle = 0; angle <= 2 * Math.PI; angle += 0.1)
                {
                    circlePoints.Add(new Point(
                        centerPoint.X + centerSize * Math.Cos(angle),
                        centerPoint.Y + centerSize * Math.Sin(angle)
                    ));
                }

                // 绘制圆心点
                var point = new StylusPointCollection(circlePoints);
                var stroke = new Stroke(point)
                {
                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                };

                // 设置圆心点的样式
                stroke.DrawingAttributes.Width = 2.0;
                stroke.DrawingAttributes.Height = 2.0;

                // 添加到画布
                inkCanvas.Strokes.Add(stroke);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"绘制圆心标记失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 处理主窗口的鼠标移动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        /// <remarks>
        /// 当在主窗口上移动鼠标时：
        /// 1. 如果启用了光标显示，则显示光标并根据编辑模式设置光标
        /// 2. 如果禁用了光标显示：
        ///    - 如果没有触笔设备，则显示光标
        ///    - 如果有触笔设备，则隐藏光标
        /// </remarks>
        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (Settings.Canvas.IsShowCursor)
            {
                System.Windows.Forms.Cursor.Show();
                SetCursorBasedOnEditingMode(inkCanvas);
            }
            else
            {
                if (e.StylusDevice == null)
                {
                    System.Windows.Forms.Cursor.Show();
                }
                else
                {
                    System.Windows.Forms.Cursor.Hide();
                }
            }
        }
    }
}
