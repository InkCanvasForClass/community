using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 反馈数据脱敏处理器。
    /// 对上传到 pastebin 的 JSON 数据进行脱敏，移除敏感配置。
    /// 脱敏规则：
    /// - 设备 ID：保留原样
    /// - WebDAV 配置（URL、用户名、密码、根目录）：完全移除
    /// - DlassSettings 的 token 和 API 地址：移除
    /// - 密码哈希/盐值：移除
    /// </summary>
    public static class FeedbackSanitizer
    {
        /// <summary>
        /// 需要从反馈 JSON 中移除的敏感字段名（不区分大小写匹配）。
        /// </summary>
        private static readonly string[] SensitiveFieldNames = new[]
        {
            "webDavUrl", "webDavUsername", "webDavPassword", "webDavRootDirectory",
            "userToken", "savedTokens", "apiBaseUrl",
            "passwordEnabled", "passwordSalt", "passwordHash",
            "requirePasswordOnExit", "requirePasswordOnEnterSettings",
            "requirePasswordOnResetConfig", "requirePasswordOnModifyOrClearNameList",
            "hasAcceptedTelemetryPrivacy", "telemetryUploadLevel"
        };

        /// <summary>
        /// 将 Settings 对象序列化为脱敏后的 JSON。
        /// 设备 ID 保留原样，移除 WebDAV、token、密码等敏感字段。
        /// </summary>
        public static string BuildSanitizedSettingsJson(Settings settings, string deviceId)
        {
            if (settings == null)
                return "{}";

            try
            {
                // 序列化整个 Settings
                var json = JObject.FromObject(settings);

                // 递归移除敏感字段
                SanitizeToken(json);

                // 添加设备 ID（原样，不脱敏）
                json["deviceId"] = deviceId ?? "";

                // 格式化输出
                return json.ToString(Formatting.Indented);
            }
            catch
            {
                return "{}";
            }
        }

        /// <summary>
        /// 递归遍历 JSON 对象，移除敏感字段。
        /// </summary>
        private static void SanitizeToken(JToken token)
        {
            if (token is JObject obj)
            {
                // 收集要移除的属性名
                var toRemove = new System.Collections.Generic.List<JProperty>();
                foreach (var prop in obj.Properties())
                {
                    bool isSensitive = false;
                    foreach (var sensitiveName in SensitiveFieldNames)
                    {
                        if (string.Equals(prop.Name, sensitiveName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            isSensitive = true;
                            break;
                        }
                    }

                    if (isSensitive)
                    {
                        toRemove.Add(prop);
                    }
                    else
                    {
                        // 递归处理子对象
                        SanitizeToken(prop.Value);
                    }
                }

                foreach (var prop in toRemove)
                {
                    prop.Remove();
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    SanitizeToken(item);
                }
            }
        }
    }
}
