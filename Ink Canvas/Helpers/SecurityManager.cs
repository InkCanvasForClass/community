using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Helpers
{
    internal static class SecurityManager
    {
        private const int Pbkdf2Iterations = 120_000;
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;

        /// <summary>
        /// 检查设置中是否启用了密码安全功能。
        /// </summary>
        /// <param name="settings">应用程序设置对象（可能为 null）。</param>
        /// <returns>`true` 当 settings 非 null 且其 Security 部分存在且已启用密码功能；`false` 否则。</returns>
        public static bool IsPasswordFeatureEnabled(Settings settings)
        => settings?.Security != null && settings.Security.PasswordEnabled;

        /// <summary>
        /// 确定给定设置中是否已配置密码（存在非空的密码盐和密码哈希）。
        /// </summary>
        /// <param name="settings">应用的设置；为 null 或未包含 Security 部分时视为未配置密码。</param>
        /// <returns>`true` 如果设置包含非空的 PasswordSalt 和 PasswordHash，否则 `false`。</returns>
        public static bool HasPasswordConfigured(Settings settings)
            => settings?.Security != null
                && !string.IsNullOrWhiteSpace(settings.Security.PasswordSalt)
                && !string.IsNullOrWhiteSpace(settings.Security.PasswordHash);

        /// <summary>
        /// 确定在退出应用时是否需要输入密码。
        /// </summary>
        /// <param name="settings">应用配置；如果为 null，则视为未启用或未配置密码。</param>
        /// <returns>`true` 当密码功能已启用、已配置密码且设置要求在退出时需要密码，`false` 否则。</returns>
        public static bool IsPasswordRequiredForExit(Settings settings)
            => IsPasswordFeatureEnabled(settings) && HasPasswordConfigured(settings) && settings.Security.RequirePasswordOnExit;

        /// <summary>
        /// 确定在进入设置界面时是否需要输入密码。
        /// </summary>
        /// <param name="settings">应用配置；为 null 或未启用密码功能时视为未配置密码。</param>
        /// <returns>`true` 如果已启用密码功能、已配置密码且已设置为在进入设置时要求密码，`false` 否则。</returns>
        public static bool IsPasswordRequiredForEnterSettings(Settings settings)
            => IsPasswordFeatureEnabled(settings) && HasPasswordConfigured(settings) && settings.Security.RequirePasswordOnEnterSettings;

        /// <summary>
        /// 指示在重置配置时是否需要输入密码。
        /// </summary>
        /// <param name="settings">应用设置对象；如果为 null 或未启用密码功能，则视为不需要密码。</param>
        /// <returns>`true` 如果已启用密码功能、已有配置的密码且设置要求在重置配置时进行密码验证；`false` 否则。</returns>
        public static bool IsPasswordRequiredForResetConfig(Settings settings)
            => IsPasswordFeatureEnabled(settings) && HasPasswordConfigured(settings) && settings.Security.RequirePasswordOnResetConfig;

        /// <summary>
        /// 将提供的明文密码与 Settings 中存储的密码散列进行比对以验证密码是否正确。
        /// </summary>
        /// <param name="settings">包含存储的密码盐和哈希的设置对象（使用 Base64 编码的 PasswordSalt 和 PasswordHash）。</param>
        /// <param name="password">要验证的明文密码。</param>
        /// <returns>`true` 如果密码与存储的哈希匹配，`false` 否则（包括未配置密码、password 为 null 或在解析/派生过程中发生错误）。</returns>
        public static bool VerifyPassword(Settings settings, string password)
        {
            if (!HasPasswordConfigured(settings)) return false;
            if (password == null) return false;

            try
            {
                var salt = Convert.FromBase64String(settings.Security.PasswordSalt);
                var expected = Convert.FromBase64String(settings.Security.PasswordHash);

                var actual = DeriveKey(password, salt, expected.Length);
                return FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 如果已配置密码，显示一个对话框提示用户输入密码并验证；如果未配置密码则直接允许通过。
        /// </summary>
        /// <returns>`true` 如果未配置密码或用户确认并输入了正确的密码，`false` 如果用户取消或验证失败。</returns>
        public static async Task<bool> PromptAndVerifyAsync(Settings settings, Window owner, string title, string message)
        {
            if (!HasPasswordConfigured(settings)) return true;

            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消"
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            var passwordBox = new PasswordBox
            {
                Height = 32
            };

            panel.Children.Add(textBlock);
            panel.Children.Add(passwordBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return false;

            return VerifyPassword(settings, passwordBox.Password);
        }

        /// <summary>
        /// 显示一个对话框让用户输入并确认新密码，成功时返回该密码。
        /// </summary>
        /// <param name="owner">对话框的所属窗口（用于指定父窗口）。</param>
        /// <returns>用户输入的新密码；如果用户取消或输入无效（长度不足或两次不匹配），则返回 <c>null</c>。</returns>
        public static async Task<string> PromptSetNewPasswordAsync(Window owner)
        {
            var dialog = new ContentDialog
            {
                Title = "设置安全密码",
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消"
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var tipText = new TextBlock
            {
                Text = "请输入新密码",
                TextWrapping = TextWrapping.Wrap
            };

            var newPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            var confirmPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };

            panel.Children.Add(tipText);
            panel.Children.Add(new TextBlock { Text = "新密码", Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(newPwdBox);
            panel.Children.Add(new TextBlock { Text = "确认新密码", Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(confirmPwdBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return null;

            var pwd = newPwdBox.Password ?? "";
            var confirm = confirmPwdBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 4)
            {
                MessageBox.Show("密码长度过短。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!string.Equals(pwd, confirm, StringComparison.Ordinal))
            {
                MessageBox.Show("两次输入的密码不一致。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return pwd;
        }

        /// <summary>
        /// 弹出对话框以更改已配置的安全密码；如果尚未配置密码则转而提示设置新密码。
        /// </summary>
        /// <param name="settings">应用配置对象，包含当前存储的密码信息。</param>
        /// <param name="owner">对话框的父窗口（用于定位/所有权）。</param>
        /// <returns>用户成功更改后返回新的密码字符串；当用户取消、验证失败或校验不通过时返回 <c>null</c>。</returns>
        public static async Task<string> PromptChangePasswordAsync(Settings settings, Window owner)
        {
            if (!HasPasswordConfigured(settings))
            {
                return await PromptSetNewPasswordAsync(owner);
            }

            var dialog = new ContentDialog
            {
                Title = "修改安全密码",
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消"
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var tipText = new TextBlock
            {
                Text = "请输入当前密码，并设置新密码。",
                TextWrapping = TextWrapping.Wrap
            };

            var currentBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            var newPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            var confirmPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };

            panel.Children.Add(tipText);
            panel.Children.Add(new TextBlock { Text = "当前密码", Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(currentBox);
            panel.Children.Add(new TextBlock { Text = "新密码", Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(newPwdBox);
            panel.Children.Add(new TextBlock { Text = "确认新密码", Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(confirmPwdBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return null;

            var current = currentBox.Password ?? "";
            var newPwd = newPwdBox.Password ?? "";
            var confirm = confirmPwdBox.Password ?? "";

            if (!VerifyPassword(settings, current))
            {
                MessageBox.Show("当前密码错误。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 4)
            {
                MessageBox.Show("新密码长度过短。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!string.Equals(newPwd, confirm, StringComparison.Ordinal))
            {
                MessageBox.Show("两次输入的新密码不一致。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return newPwd;
        }

        /// <summary>
        /// 为指定 Settings 生成并存储新的密码盐与哈希到 settings.Security 中。
        /// </summary>
        /// <param name="settings">要更新的设置对象；如果为 null 或其 Security 为 null 则不执行任何操作。</param>
        /// <param name="password">用于派生哈希的原始密码字符串。</param>
        public static void SetPassword(Settings settings, string password)
        {
            if (settings?.Security == null) return;

            var salt = new byte[SaltSizeBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            var hash = DeriveKey(password, salt, HashSizeBytes);

            settings.Security.PasswordSalt = Convert.ToBase64String(salt);
            settings.Security.PasswordHash = Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 清除设置中存储的密码信息。
        /// </summary>
        /// <param name="settings">要更新的设置对象；将把其 Security.PasswordSalt 和 Security.PasswordHash 设为空字符串。若 <paramref name="settings"/> 为 null 或其 Security 为 null 则不执行任何操作。</param>
        public static void ClearPassword(Settings settings)
        {
            if (settings?.Security == null) return;
            settings.Security.PasswordSalt = "";
            settings.Security.PasswordHash = "";
        }

        /// <summary>
        /// 使用 PBKDF2（Rfc2898）从给定的密码和盐派生指定长度的密钥字节。
        /// </summary>
        /// <param name="password">用于派生的密码字符串。</param>
        /// <param name="salt">用于派生的盐字节数组（不可为 null）。</param>
        /// <param name="keyBytes">要返回的密钥字节长度（以字节为单位）。</param>
        /// <returns>派生出的密钥字节数组，长度等于 <paramref name="keyBytes"/>。</returns>
        private static byte[] DeriveKey(string password, byte[] salt, int keyBytes)
        {
            // 注意：Rfc2898DeriveBytes 在 net472 默认 HMACSHA1
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations))
            {
                return kdf.GetBytes(keyBytes);
            }
        }

        /// <summary>
        /// 以固定时间方式比较两个字节数组的内容是否完全相同，防止基于时序的比对攻击。
        /// </summary>
        /// <param name="a">要比较的第一个字节数组。</param>
        /// <param name="b">要比较的第二个字节数组。</param>
        /// <returns>`true` 如果两个数组长度相同且所有字节相等，`false` 否则。</returns>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
