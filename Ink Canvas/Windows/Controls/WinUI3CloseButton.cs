using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ink_Canvas.Windows.Controls
{
    public class WinUI3CloseButton : ButtonBase
    {
        static WinUI3CloseButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WinUI3CloseButton), new FrameworkPropertyMetadata(typeof(WinUI3CloseButton)));
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(WinUI3CloseButton),
            new PropertyMetadata(true)
        );
    }
}
