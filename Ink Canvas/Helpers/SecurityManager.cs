using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

        public static bool HasTotpConfigured(Settings settings)
            => settings?.Security != null
               && settings.Security.TotpEnabled
               && !string.IsNullOrWhiteSpace(settings.Security.TotpSecret);

        public static bool IsTotpOnlyMode(Settings settings)
            => settings?.Security != null
               && settings.Security.TotpOnlyMode
               && HasTotpConfigured(settings);

        public static bool IsSecurityFeatureEnabled(Settings settings)
            => IsPasswordFeatureEnabled(settings) || HasTotpConfigured(settings);

        public static bool IsSecurityConfigured(Settings settings)
            => HasPasswordConfigured(settings) || HasTotpConfigured(settings);

        /// <summary>
        /// 确定在退出应用时是否需要输入密码或 TOTP 验证码。
        /// </summary>
        public static bool IsPasswordRequiredForExit(Settings settings)
            => IsSecurityFeatureEnabled(settings) && IsSecurityConfigured(settings) && settings.Security.RequirePasswordOnExit;

        /// <summary>
        /// 确定在进入设置界面时是否需要输入密码或 TOTP 验证码。
        /// </summary>
        public static bool IsPasswordRequiredForEnterSettings(Settings settings)
            => IsSecurityFeatureEnabled(settings) && IsSecurityConfigured(settings) && settings.Security.RequirePasswordOnEnterSettings;

        /// <summary>
        /// 指示在重置配置时是否需要输入密码或 TOTP 验证码。
        /// </summary>
        public static bool IsPasswordRequiredForResetConfig(Settings settings)
            => IsSecurityFeatureEnabled(settings) && IsSecurityConfigured(settings) && settings.Security.RequirePasswordOnResetConfig;

        /// <summary>
        /// 指示在修改或清空点名名单前是否需要输入安全密码或 TOTP 验证码。
        /// </summary>
        public static bool IsPasswordRequiredForModifyOrClearNameList(Settings settings)
            => IsSecurityFeatureEnabled(settings)
               && IsSecurityConfigured(settings)
               && settings.Security.RequirePasswordOnModifyOrClearNameList;

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

            // 1. Check if USB verification is enabled and active
            bool usbVerified = false;
            DispatcherTimer usbCheckTimer = null;
            if (settings?.Security?.UsbVerificationEnabled == true)
            {
                if (UsbSecurityManager.VerifyCurrentUsbDrives(settings))
                {
                    return true;
                }
            }

            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = CommonStrings.Common_OK,
                SecondaryButtonText = CommonStrings.Common_Cancel
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Enhance message if USB verification is enabled
            string finalMessage = message;
            if (settings?.Security?.UsbVerificationEnabled == true)
            {
                finalMessage += SecurityStrings.Security_UsbBypassDialogHint;
            }

            var textBlock = new TextBlock
            {
                Text = finalMessage,
                TextWrapping = TextWrapping.Wrap
            };

            var passwordBox = new PasswordBox
            {
                Height = 32
            };

            panel.Children.Add(textBlock);
            panel.Children.Add(passwordBox);
            dialog.Content = panel;

            if (settings?.Security?.UsbVerificationEnabled == true)
            {
                usbCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                usbCheckTimer.Tick += (s, e) =>
                {
                    if (UsbSecurityManager.VerifyCurrentUsbDrives(settings))
                    {
                        usbVerified = true;
                        usbCheckTimer.Stop();
                        dialog.Hide();
                    }
                };
                usbCheckTimer.Start();
            }

            try
            {
                var result = await dialog.ShowAsync();
                if (usbVerified) return true;
                if (result != ContentDialogResult.Primary) return false;

                return VerifyPassword(settings, passwordBox.Password);
            }
            finally
            {
                if (usbCheckTimer != null)
                {
                    usbCheckTimer.Stop();
                }
            }
        }

        public static async Task<bool> PromptAndVerifyPasswordOrTotpAsync(Settings settings, Window owner, string title, string message)
        {
            bool hasPassword = IsPasswordFeatureEnabled(settings) && HasPasswordConfigured(settings);
            bool hasTotp = HasTotpConfigured(settings);
            bool totpOnlyMode = IsTotpOnlyMode(settings);
            if (!hasPassword && !hasTotp) return true;

            if (totpOnlyMode)
            {
                hasPassword = false;
            }

            // 1. Check if USB verification is enabled and active
            bool usbVerified = false;
            DispatcherTimer usbCheckTimer = null;
            if (settings?.Security?.UsbVerificationEnabled == true)
            {
                if (UsbSecurityManager.VerifyCurrentUsbDrives(settings))
                {
                    return true;
                }
            }

            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = CommonStrings.Common_OK,
                SecondaryButtonText = CommonStrings.Common_Cancel
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var inputBox = new PasswordBox
            {
                Height = 32
            };
            panel.Children.Add(inputBox);

            string hintText;
            if (totpOnlyMode)
            {
                hintText = MainWindowStrings.Main_Security_TotpOnlyHint;
            }
            else if (hasPassword && hasTotp)
            {
                hintText = MainWindowStrings.Main_Security_PasswordOrTotpHint;
            }
            else if (hasTotp)
            {
                hintText = MainWindowStrings.Main_Security_TotpOnlyHint;
            }
            else
            {
                hintText = MainWindowStrings.Main_Security_PasswordOnlyHint;
            }

            if (settings?.Security?.UsbVerificationEnabled == true)
            {
                hintText += SecurityStrings.Security_UsbBypassDialogHintShort;
            }

            panel.Children.Add(new TextBlock
            {
                Text = hintText,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72
            });

            dialog.Content = panel;

            bool noFocusModeWasTemporarilyDisabled = false;
            if (owner != null && owner.IsVisible && settings?.Advanced?.IsNoFocusMode == true)
            {
                WindowSettingsHelper.IsTemporarilyDisablingNoFocusMode = true;
                WindowSettingsHelper.ApplyNoFocusMode(owner);
                noFocusModeWasTemporarilyDisabled = true;
            }

            if (settings?.Security?.UsbVerificationEnabled == true)
            {
                usbCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                usbCheckTimer.Tick += (s, e) =>
                {
                    if (UsbSecurityManager.VerifyCurrentUsbDrives(settings))
                    {
                        usbVerified = true;
                        usbCheckTimer.Stop();
                        dialog.Hide();
                    }
                };
                usbCheckTimer.Start();
            }

            try
            {
                dialog.Opened += (s, e) =>
                {
                    inputBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        inputBox.Focus();
                        Keyboard.Focus(inputBox);
                    }), DispatcherPriority.Input);
                };

                var result = await dialog.ShowAsync();
                if (usbVerified) return true;
                if (result != ContentDialogResult.Primary) return false;

                string input = inputBox.Password ?? "";
                if (!totpOnlyMode && hasPassword && VerifyPassword(settings, input)) return true;
                return hasTotp && VerifyTotp(settings, input);
            }
            finally
            {
                if (usbCheckTimer != null)
                {
                    usbCheckTimer.Stop();
                }

                if (noFocusModeWasTemporarilyDisabled)
                {
                    WindowSettingsHelper.IsTemporarilyDisablingNoFocusMode = false;
                    WindowSettingsHelper.ApplyNoFocusMode(owner);
                }
            }
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
                Title = MainWindowStrings.Main_Security_SetPasswordTitle,
                PrimaryButtonText = CommonStrings.Common_OK,
                SecondaryButtonText = CommonStrings.Common_Cancel
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var tipText = new TextBlock
            {
                Text = MainWindowStrings.Main_Security_EnterNewPassword,
                TextWrapping = TextWrapping.Wrap
            };

            var newPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            var confirmPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };

            panel.Children.Add(tipText);
            panel.Children.Add(new TextBlock { Text = MainWindowStrings.Main_Security_NewPasswordLabel, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(newPwdBox);
            panel.Children.Add(new TextBlock { Text = MainWindowStrings.Main_Security_ConfirmNewPasswordLabel, Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(confirmPwdBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return null;

            var pwd = newPwdBox.Password ?? "";
            var confirm = confirmPwdBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 4)
            {
                MessageBox.Show(MainWindowStrings.Main_Security_PasswordTooShort, MainWindowStrings.Main_Security_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!string.Equals(pwd, confirm, StringComparison.Ordinal))
            {
                MessageBox.Show(MainWindowStrings.Main_Security_PasswordMismatch, MainWindowStrings.Main_Security_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                Title = MainWindowStrings.Main_Security_ChangePasswordTitle,
                PrimaryButtonText = CommonStrings.Common_OK,
                SecondaryButtonText = CommonStrings.Common_Cancel
            };

            var panel = new SimpleStackPanel
            {
                Spacing = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var tipText = new TextBlock
            {
                Text = MainWindowStrings.Main_Security_ChangePasswordHint,
                TextWrapping = TextWrapping.Wrap
            };

            var currentBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            var newPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            var confirmPwdBox = new PasswordBox { Height = 32, Margin = new Thickness(0, 4, 0, 0) };

            panel.Children.Add(tipText);
            panel.Children.Add(new TextBlock { Text = MainWindowStrings.Main_Security_CurrentPasswordLabel, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(currentBox);
            panel.Children.Add(new TextBlock { Text = MainWindowStrings.Main_Security_NewPasswordLabel, Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(newPwdBox);
            panel.Children.Add(new TextBlock { Text = MainWindowStrings.Main_Security_ConfirmNewPasswordLabel, Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(confirmPwdBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return null;

            var current = currentBox.Password ?? "";
            var newPwd = newPwdBox.Password ?? "";
            var confirm = confirmPwdBox.Password ?? "";

            if (!VerifyPassword(settings, current))
            {
                MessageBox.Show(MainWindowStrings.Main_Security_CurrentPasswordWrong, MainWindowStrings.Main_Security_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 4)
            {
                MessageBox.Show(MainWindowStrings.Main_Security_NewPasswordTooShort, MainWindowStrings.Main_Security_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!string.Equals(newPwd, confirm, StringComparison.Ordinal))
            {
                MessageBox.Show(MainWindowStrings.Main_Security_NewPasswordMismatch, MainWindowStrings.Main_Security_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
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

        public static string GenerateTotpSecret()
        {
            var bytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base32Encode(bytes);
        }

        public static bool VerifyTotp(Settings settings, string code)
        {
            if (!HasTotpConfigured(settings) || string.IsNullOrWhiteSpace(code)) return false;

            string normalized = new string(code.Where(char.IsDigit).ToArray());
            if (normalized.Length != 6) return false;

            try
            {
                var secret = Base32Decode(settings.Security.TotpSecret);
                long step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
                for (long offset = -1; offset <= 1; offset++)
                {
                    string expected = GenerateTotpCode(secret, step + offset);
                    if (FixedTimeEquals(Encoding.ASCII.GetBytes(normalized), Encoding.ASCII.GetBytes(expected)))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
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

        private static string GenerateTotpCode(byte[] secret, long timeStep)
        {
            var counter = BitConverter.GetBytes(timeStep);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counter);

            using (var hmac = new HMACSHA1(secret))
            {
                var hash = hmac.ComputeHash(counter);
                int offset = hash[hash.Length - 1] & 0x0f;
                int binary =
                    ((hash[offset] & 0x7f) << 24)
                    | ((hash[offset + 1] & 0xff) << 16)
                    | ((hash[offset + 2] & 0xff) << 8)
                    | (hash[offset + 3] & 0xff);
                return (binary % 1_000_000).ToString("D6");
            }
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            if (data == null || data.Length == 0) return "";

            var result = new StringBuilder();
            int buffer = data[0];
            int next = 1;
            int bitsLeft = 8;
            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < 5)
                {
                    if (next < data.Length)
                    {
                        buffer <<= 8;
                        buffer |= data[next++] & 0xff;
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = 5 - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }

                int index = 0x1f & (buffer >> (bitsLeft - 5));
                bitsLeft -= 5;
                result.Append(alphabet[index]);
            }

            return result.ToString();
        }

        private static byte[] Base32Decode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

            string normalized = input.Trim().Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
            var bytes = new System.Collections.Generic.List<byte>();
            int buffer = 0;
            int bitsLeft = 0;

            foreach (char c in normalized)
            {
                int value;
                if (c >= 'A' && c <= 'Z') value = c - 'A';
                else if (c >= '2' && c <= '7') value = c - '2' + 26;
                else continue;

                buffer = (buffer << 5) | value;
                bitsLeft += 5;

                if (bitsLeft >= 8)
                {
                    bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                    bitsLeft -= 8;
                }
            }

            return bytes.ToArray();
        }
    }
}