# ToggleSwitch

## 定义

命名空间：`Ink_Canvas.Components`

ToggleSwitch 开关按钮，只有开和关两种状态，可通过点击来切换状态。

## 属性

| Name | Description |
|------|-------------|
| `IsOn` | 指示是否为开启状态 |
| `IsEnabled` | 指示是否可用，不可用透明度减半且无HitTest，无TabStop |
| `IsDisplayTextIndicator` | 指示是否显示文字提示，I 和 O (WIP) |
| `OnContent` | 指示开启时的文字，为空或不指定则不显示 (WIP) |
| `OffContent` | 指示关闭时的文字，为空或不指定则不显示 (WIP) |
| `SwitchBackground` | 指示切换按钮的背景色，不设置则采用默认颜色 |
| `ThumbForeground` | 指示切换按钮Thumb的颜色，不设置则采用默认颜色 (WIP) |
| `IsEnableClickFeedback` | 指示是否启用点击时的变暗反馈 (WIP) |
| `IsReduceAnimations` | 指示是否减弱动画效果 (WIP) |
| `SwitchSize` | 指示ToggleSwitch的大小 (WIP) |

## 事件

| Name | Description |
|------|-------------|
| `OnToggled` | 当切换按钮的开关状态被修改时触发 |
| `IsEnableClickFeedbackChanged` | 当 `IsEnableClickFeedback` 被修改时触发 (WIP) |
| `IsReduceAnimationsChanged` | 当 `IsReduceAnimations` 被修改时触发 (WIP) |
| `OnSwitchsizeChanged` | 当 `SwitchSize` 变化时触发 (WIP) |