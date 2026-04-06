using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ink_Canvas;
using Ink_Canvas.Services;

namespace Ink_Canvas
{
    public static class SettingsServiceDemo
    {
        public static void ListAllSettings()
        {
            var settingsType = typeof(Settings);
            var allProperties = new List<string>();

            Console.WriteLine("=== Settings 所有属性列表 ===\n");

            foreach (var categoryProp in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (categoryProp.PropertyType.IsClass && categoryProp.PropertyType != typeof(string) && categoryProp.PropertyType != typeof(List<string>) && categoryProp.PropertyType != typeof(List<CustomFloatingBarIcon>) && categoryProp.PropertyType != typeof(List<CustomPickNameBackground>) && categoryProp.PropertyType != typeof(Dictionary<string, bool>) && categoryProp.PropertyType != typeof(List<string>) && categoryProp.PropertyType != typeof(DateTime))
                {
                    Console.WriteLine($"\n--- 分类: {categoryProp.Name} ---\n");
                    
                    foreach (var settingProp in categoryProp.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var propName = $"{categoryProp.Name}.{settingProp.Name}";
                        allProperties.Add(propName);
                        Console.WriteLine($"  {settingProp.Name,-60} ({settingProp.PropertyType.Name})");
                    }
                }
                else
                {
                    Console.WriteLine($"{categoryProp.Name,-60} ({categoryProp.PropertyType.Name})");
                    allProperties.Add(categoryProp.Name);
                }
            }

            Console.WriteLine($"\n\n总计: {allProperties.Count} 个设置项\n");

            Console.WriteLine("=== SettingsService 访问示例 ===\n");

            Console.WriteLine("// 方式1: 泛型 Set/Get");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Canvas, c => c.InkWidth, 3.5);");
            Console.WriteLine("double inkWidth = SettingsService.Instance.Get(s => s.Canvas, c => c.InkWidth);");
            Console.WriteLine();

            Console.WriteLine("// 方式2: 便捷方法");
            Console.WriteLine("SettingsService.Instance.SetTheme(1);");
            Console.WriteLine("SettingsService.Instance.SetNoFocusMode(true);");
            Console.WriteLine();

            Console.WriteLine("// 方式3: 直接访问 Settings 对象");
            Console.WriteLine("var settings = SettingsService.Instance.Settings;");
            Console.WriteLine("if (settings != null) {");
            Console.WriteLine("    settings.Canvas.InkWidth = 3.5;");
            Console.WriteLine("    settings.Appearance.Theme = 1;");
            Console.WriteLine("    SettingsService.Instance.Save();");
            Console.WriteLine("}");
            Console.WriteLine();

            Console.WriteLine("// 任意设置项示例:");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Automation, a => a.IsAutoFoldInEasiNote, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.PowerPointSettings, p => p.PowerPointSupport, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Gesture, g => g.IsEnableTwoFingerZoom, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Advanced, a => a.IsAlwaysOnTop, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Startup, s => s.IsAutoUpdate, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Security, s => s.PasswordEnabled, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Camera, c => c.RotationAngle, 90);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.InkToShape, i => i.IsInkToShapeEnabled, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.RandSettings, r => r.EnableQuickDraw, true);");
            Console.WriteLine("SettingsService.Instance.Set(s => s.Appearance, a => a.ViewboxFloatingBarOpacityValue, 0.8);");
        }
    }
}
