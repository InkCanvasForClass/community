using Ink_Canvas.Controls;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class SettingsBinder
    {
        private static bool _isInitializing = false;

        #region PropertyPath

        public static readonly DependencyProperty PropertyPathProperty =
            DependencyProperty.RegisterAttached(
                "PropertyPath",
                typeof(string),
                typeof(SettingsBinder),
                new PropertyMetadata(null, OnPropertyPathChanged));

        public static string GetPropertyPath(DependencyObject obj) =>
            (string)obj.GetValue(PropertyPathProperty);

        public static void SetPropertyPath(DependencyObject obj, string value) =>
            obj.SetValue(PropertyPathProperty, value);

        #endregion

        #region SettingsChanged

        public static readonly DependencyProperty SettingsChangedProperty =
            DependencyProperty.RegisterAttached(
                "SettingsChanged",
                typeof(string),
                typeof(SettingsBinder),
                new PropertyMetadata(null));

        public static string GetSettingsChanged(DependencyObject obj) =>
            (string)obj.GetValue(SettingsChangedProperty);

        public static void SetSettingsChanged(DependencyObject obj, string value) =>
            obj.SetValue(SettingsChangedProperty, value);

        #endregion

        #region FormatString

        public static readonly DependencyProperty FormatStringProperty =
            DependencyProperty.RegisterAttached(
                "FormatString",
                typeof(string),
                typeof(SettingsBinder),
                new PropertyMetadata(null));

        public static string GetFormatString(DependencyObject obj) =>
            (string)obj.GetValue(FormatStringProperty);

        public static void SetFormatString(DependencyObject obj, string value) =>
            obj.SetValue(FormatStringProperty, value);

        #endregion

        #region ValueRounding

        public static readonly DependencyProperty ValueRoundingProperty =
            DependencyProperty.RegisterAttached(
                "ValueRounding",
                typeof(int),
                typeof(SettingsBinder),
                new PropertyMetadata(-1));

        public static int GetValueRounding(DependencyObject obj) =>
            (int)obj.GetValue(ValueRoundingProperty);

        public static void SetValueRounding(DependencyObject obj, int value) =>
            obj.SetValue(ValueRoundingProperty, value);

        #endregion

        #region TargetTextBlock

        public static readonly DependencyProperty TargetTextBlockProperty =
            DependencyProperty.RegisterAttached(
                "TargetTextBlock",
                typeof(TextBlock),
                typeof(SettingsBinder),
                new PropertyMetadata(null));

        public static TextBlock GetTargetTextBlock(DependencyObject obj) =>
            (TextBlock)obj.GetValue(TargetTextBlockProperty);

        public static void SetTargetTextBlock(DependencyObject obj, TextBlock value) =>
            obj.SetValue(TargetTextBlockProperty, value);

        #endregion

        private static void OnPropertyPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element)) return;
            string propertyPath = e.NewValue as string;
            if (string.IsNullOrEmpty(propertyPath)) return;

            RoutedEventHandler loadedHandler = null;
            loadedHandler = (s, args) =>
            {
                element.Loaded -= loadedHandler;
                try
                {
                    _isInitializing = true;
                    object currentValue = GetValueFromSettings(propertyPath);
                    ApplyValueToControl(element, currentValue);
                    _isInitializing = false;
                }
                catch (Exception ex)
                {
                    _isInitializing = false;
                    System.Diagnostics.Debug.WriteLine($"SettingsBinder load error: {ex.Message}");
                }

                RegisterControlListener(element, propertyPath);
            };
            element.Loaded += loadedHandler;
        }

        private static object GetValueFromSettings(string propertyPath)
        {
            object current = SettingsManager.Settings;
            foreach (string part in propertyPath.Split('.'))
            {
                if (current == null) return null;
                PropertyInfo prop = current.GetType().GetProperty(part);
                if (prop == null) return null;
                current = prop.GetValue(current);
            }
            return current;
        }

        private static void SetValueToSettings(string propertyPath, object value)
        {
            object current = SettingsManager.Settings;
            string[] parts = propertyPath.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current == null) return;
                PropertyInfo prop = current.GetType().GetProperty(parts[i]);
                if (prop == null) return;
                current = prop.GetValue(current);
            }
            if (current == null) return;
            PropertyInfo targetProp = current.GetType().GetProperty(parts[^1]);
            if (targetProp == null) return;

            if (targetProp.PropertyType == typeof(double) && value is int intValue)
                value = (double)intValue;
            else if (targetProp.PropertyType == typeof(int) && value is double doubleValue)
                value = (int)Math.Round(doubleValue);

            targetProp.SetValue(current, value);
        }

        private static bool IsIsOnControl(FrameworkElement element)
        {
            return element is LabeledSettingsCard
                || element is iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
        }

        private static bool GetIsOn(FrameworkElement element)
        {
            if (element is LabeledSettingsCard lsc) return lsc.IsOn;
            if (element is iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ts) return ts.IsOn;
            return false;
        }

        private static void SetIsOn(FrameworkElement element, bool value)
        {
            if (element is LabeledSettingsCard lsc) lsc.IsOn = value;
            else if (element is iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ts) ts.IsOn = value;
        }

        private static void ApplyValueToControl(FrameworkElement element, object value)
        {
            if (value == null) return;

            if (IsIsOnControl(element))
            {
                SetIsOn(element, Convert.ToBoolean(value));
            }
            else if (element is ComboBox cb)
            {
                cb.SelectedIndex = Convert.ToInt32(value);
            }
            else if (element is Slider slider)
            {
                slider.Value = Convert.ToDouble(value);
            }
        }

        private static void RegisterControlListener(FrameworkElement element, string propertyPath)
        {
            if (IsIsOnControl(element))
            {
                if (element is LabeledSettingsCard lsc)
                {
                    lsc.Toggled += (s, e) =>
                    {
                        if (_isInitializing) return;
                        SetValueToSettings(propertyPath, lsc.IsOn);
                        SettingsManager.SaveSettingsToFile();
                        InvokeSettingsChanged(element, lsc.IsOn);
                    };
                }
                else if (element is iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ts)
                {
                    ts.Toggled += (s, e) =>
                    {
                        if (_isInitializing) return;
                        SetValueToSettings(propertyPath, ts.IsOn);
                        SettingsManager.SaveSettingsToFile();
                        InvokeSettingsChanged(element, ts.IsOn);
                    };
                }
            }
            else if (element is ComboBox cb)
            {
                cb.SelectionChanged += (s, e) =>
                {
                    if (_isInitializing) return;
                    SetValueToSettings(propertyPath, cb.SelectedIndex);
                    SettingsManager.SaveSettingsToFile();
                    InvokeSettingsChanged(element, cb.SelectedIndex);
                };
            }
            else if (element is Slider slider)
            {
                slider.ValueChanged += (s, e) =>
                {
                    int rounding = GetValueRounding(slider);
                    double val = e.NewValue;

                    if (rounding >= 0)
                    {
                        val = Math.Round(val, rounding);
                        if (Math.Abs(slider.Value - val) > 0.0001)
                        {
                            slider.Value = val;
                            return;
                        }
                    }

                    UpdateTargetTextBlock(slider, val);

                    if (_isInitializing) return;
                    SetValueToSettings(propertyPath, val);
                    SettingsManager.SaveSettingsToFile();
                    InvokeSettingsChanged(element, val);
                };
            }
        }

        private static void UpdateTargetTextBlock(FrameworkElement element, double value)
        {
            var textBlock = GetTargetTextBlock(element);
            string format = GetFormatString(element);
            if (textBlock != null && !string.IsNullOrEmpty(format))
            {
                textBlock.Text = string.Format(format, value);
            }
        }

        private static void InvokeSettingsChanged(FrameworkElement element, object value)
        {
            string methodName = GetSettingsChanged(element);
            if (string.IsNullOrEmpty(methodName)) return;

            SettingsChangeRegistry.Invoke(methodName, value);
        }

        public static void BeginInit()
        {
            _isInitializing = true;
        }

        public static void EndInit()
        {
            _isInitializing = false;
        }
    }

    public static class SettingsChangeRegistry
    {
        public static void Invoke(string methodName, params object[] args)
        {
            if (string.IsNullOrEmpty(methodName)) return;

            Type hubType = typeof(SettingsActionHub);
            MethodInfo method = hubType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Static);

            if (method == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SettingsChangeRegistry: '{methodName}' not found in SettingsActionHub");
                return;
            }

            try
            {
                ParameterInfo[] parameters = method.GetParameters();
                object[] invokeArgs = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i < args.Length && args[i] != null)
                    {
                        try
                        {
                            invokeArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
                        }
                        catch
                        {
                            invokeArgs[i] = GetDefault(parameters[i].ParameterType);
                        }
                    }
                    else
                    {
                        invokeArgs[i] = GetDefault(parameters[i].ParameterType);
                    }
                }

                method.Invoke(null, invokeArgs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SettingsChangeRegistry.Invoke('{methodName}') error: {ex.Message}");
            }
        }

        private static object GetDefault(Type type)
        {
            if (type == typeof(bool)) return false;
            if (type == typeof(int)) return 0;
            if (type == typeof(double)) return 0.0;
            if (type == typeof(string)) return string.Empty;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
