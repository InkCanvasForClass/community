import re

with open("Ink Canvas/MainWindow_cs/MW_Settings.cs", "r", encoding="utf-8") as f:
    content = f.read()

old_str = """        private void UpdateChickenSoupPosition()
        {
            if (BlackBoardWaterMark == null) return;
            
            if (double.IsNaN(Settings.Appearance.ChickenSoupPositionX) || double.IsNaN(Settings.Appearance.ChickenSoupPositionY))
            {
                // 默认右上角
                BlackBoardWaterMark.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                BlackBoardWaterMark.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                BlackBoardWaterMark.Margin = new Thickness(0, 15, 25, 0);
            }
            else
            {
                BlackBoardWaterMark.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                BlackBoardWaterMark.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                BlackBoardWaterMark.Margin = new Thickness(Settings.Appearance.ChickenSoupPositionX, Settings.Appearance.ChickenSoupPositionY, 0, 0);
            }
        }

        private void ButtonCustomChickenSoupPosition_Click(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            
            // 切换到自定义位置模式
            WaterMarkGrid.IsHitTestVisible = true;
            BlackBoardWaterMark.Cursor = System.Windows.Input.Cursors.SizeAll;
            
            // 如果还未自定义过位置，先将对齐方式改为左上角，并计算当前实际位置
            if (double.IsNaN(Settings.Appearance.ChickenSoupPositionX))
            {
                Point relativePoint = BlackBoardWaterMark.TransformToAncestor(WaterMarkGrid).Transform(new Point(0, 0));
                Settings.Appearance.ChickenSoupPositionX = relativePoint.X;
                Settings.Appearance.ChickenSoupPositionY = relativePoint.Y;
                UpdateChickenSoupPosition();
            }
            
            // 隐藏设置面板，以便用户拖动
            HideSubPanels();
            
            // 提示用户
            ShowNotification("已进入自定义位置模式，请拖动屏幕上的文本进行位置调整。再次点击任意工具栏按钮即可退出该模式。");
        }

        private void BlackBoardWaterMark_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (WaterMarkGrid.IsHitTestVisible)
            {
                double left = BlackBoardWaterMark.Margin.Left + e.HorizontalChange;
                double top = BlackBoardWaterMark.Margin.Top + e.VerticalChange;
                
                // 防止拖出边界
                if (left < 0) left = 0;
                if (top < 0) top = 0;
                
                Settings.Appearance.ChickenSoupPositionX = left;
                Settings.Appearance.ChickenSoupPositionY = top;
                
                BlackBoardWaterMark.Margin = new Thickness(left, top, 0, 0);
                SaveSettingsToFile();
            }
        }

        private void WaterMarkGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WaterMarkGrid.IsHitTestVisible)
            {
                WaterMarkGrid.IsHitTestVisible = false;
                BlackBoardWaterMark.Cursor = System.Windows.Input.Cursors.Arrow;
                ShowNotification("位置已保存，已退出自定义位置模式。");
            }
        }"""

new_str = """        private void UpdateChickenSoupPosition()
        {
            if (BlackBoardWaterMarkContainer == null) return;
            
            if (double.IsNaN(Settings.Appearance.ChickenSoupPositionX) || double.IsNaN(Settings.Appearance.ChickenSoupPositionY))
            {
                // 默认右上角
                BlackBoardWaterMarkContainer.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                BlackBoardWaterMarkContainer.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                BlackBoardWaterMarkContainer.Margin = new Thickness(0, 15, 25, 0);
            }
            else
            {
                BlackBoardWaterMarkContainer.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                BlackBoardWaterMarkContainer.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                BlackBoardWaterMarkContainer.Margin = new Thickness(Settings.Appearance.ChickenSoupPositionX, Settings.Appearance.ChickenSoupPositionY, 0, 0);
            }
        }

        private void ButtonCustomChickenSoupPosition_Click(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            
            // 切换到自定义位置模式
            WaterMarkGrid.IsHitTestVisible = true;
            BlackBoardWaterMarkThumb.Cursor = System.Windows.Input.Cursors.SizeAll;
            
            // 如果还未自定义过位置，先将对齐方式改为左上角，并计算当前实际位置
            if (double.IsNaN(Settings.Appearance.ChickenSoupPositionX))
            {
                Point relativePoint = BlackBoardWaterMarkContainer.TransformToAncestor(WaterMarkGrid).Transform(new Point(0, 0));
                Settings.Appearance.ChickenSoupPositionX = relativePoint.X;
                Settings.Appearance.ChickenSoupPositionY = relativePoint.Y;
                UpdateChickenSoupPosition();
            }
            
            // 隐藏设置面板，以便用户拖动
            HideSubPanels();
            
            // 提示用户
            ShowNotification("已进入自定义位置模式，请拖动屏幕上的文本进行位置调整。再次点击任意工具栏按钮即可退出该模式。");
        }

        private void BlackBoardWaterMark_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (WaterMarkGrid.IsHitTestVisible)
            {
                double left = BlackBoardWaterMarkContainer.Margin.Left + e.HorizontalChange;
                double top = BlackBoardWaterMarkContainer.Margin.Top + e.VerticalChange;
                
                // 防止拖出边界
                if (left < 0) left = 0;
                if (top < 0) top = 0;
                
                Settings.Appearance.ChickenSoupPositionX = left;
                Settings.Appearance.ChickenSoupPositionY = top;
                
                BlackBoardWaterMarkContainer.Margin = new Thickness(left, top, 0, 0);
                SaveSettingsToFile();
            }
        }

        private void WaterMarkGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WaterMarkGrid.IsHitTestVisible)
            {
                WaterMarkGrid.IsHitTestVisible = false;
                BlackBoardWaterMarkThumb.Cursor = System.Windows.Input.Cursors.Arrow;
                ShowNotification("位置已保存，已退出自定义位置模式。");
            }
        }"""

# Handle both \n and \r\n
old_str_regex = re.escape(old_str).replace(r'\n', r'\r?\n')

if re.search(old_str_regex, content):
    content = re.sub(old_str_regex, new_str, content)
    with open("Ink Canvas/MainWindow_cs/MW_Settings.cs", "w", encoding="utf-8") as f:
        f.write(content)
    print("Success")
else:
    print("Not found")
