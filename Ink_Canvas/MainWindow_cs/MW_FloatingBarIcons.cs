#region 主要的工具按鈕事件

/// <summary>
///     计算任务栏高度，桌面模式下仅计算任务栏自身高度
/// </summary>
private double CalculateToolbarHeight(bool isDesktopMode)
{
    if (isDesktopMode)
    {
        // 桌面模式: 任务栏高度 = 主屏幕高度 - 全屏可用高度
        return SystemParameters.PrimaryScreenHeight - SystemParameters.FullPrimaryScreenHeight;
    }
    else
    {
        // 其他模式: 原有计算方式(包含窗口标题栏)
        return SystemParameters.PrimaryScreenHeight 
             - SystemParameters.FullPrimaryScreenHeight 
             - SystemParameters.WindowCaptionHeight;
    }
}

#endregion

public async void ViewboxFloatingBarMarginAnimation(int MarginFromEdge,
    bool PosXCaculatedWithTaskbarHeight = false)
{

    // 删除旧计算方式
    // var toolbarHeight = System.Windows.SystemParameters.PrimaryScreenHeight - System.Windows.SystemParameters.FullPrimaryScreenHeight - System.Windows.SystemParameters.WindowCaptionHeight;
    
    // 替换为新计算方式
    var toolbarHeight = CalculateToolbarHeight(Topmost == false);

}

public async void PureViewboxFloatingBarMarginAnimationInDesktopMode()
{
    // 删除旧计算方式
    // var toolbarHeight = System.Windows.SystemParameters.PrimaryScreenHeight - System.Windows.SystemParameters.FullPrimaryScreenHeight - System.Windows.SystemParameters.WindowCaptionHeight;

    // 替换为新计算方式(桌面模式专用)
    var toolbarHeight = System.Windows.SystemParameters.PrimaryScreenHeight 
                      - System.Windows.SystemParameters.FullPrimaryScreenHeight;

}
