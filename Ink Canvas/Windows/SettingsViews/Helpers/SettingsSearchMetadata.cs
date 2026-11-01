using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    // Read declarations, not controls: indexing must never instantiate settings
    // pages, enumerate devices, apply templates or install page event handlers.
    internal static class SettingsSearchMetadata
    {
        internal sealed record Entry(string PageType, string Text, string SettingKey);
        internal static IReadOnlyList<Entry> Read(Assembly assembly, CancellationToken token)
        {
            var entries = new List<Entry>();
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            foreach (string resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("SettingsSearch.", StringComparison.Ordinal)))
            {
                token.ThrowIfCancellationRequested();
                using var stream = assembly.GetManifestResourceStream(resource);
                var document = XDocument.Load(stream);
                string page = (string)document.Root.Attribute(x + "Class");
                foreach (var element in document.Descendants().Where(e => e.Name.LocalName is "SettingsCard" or "LabeledSettingsCard" or "SettingsExpander"))
                {
                    token.ThrowIfCancellationRequested();
                    string text = Resolve(element, (string)element.Attribute("Header"), assembly);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        var header = element.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Header", StringComparison.Ordinal));
                        text = header == null ? null : string.Join(" ", header.Descendants().Where(e => e.Name.LocalName == "TextBlock")
                            .Select(e => Resolve(e, (string)e.Attribute("Text"), assembly)).Where(t => !string.IsNullOrWhiteSpace(t)));
                    }
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var key = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "SettingsNavigator.SettingsKey")?.Value;
                    entries.Add(new Entry(page, text.Trim(), string.IsNullOrWhiteSpace(key) ? text.Trim() : key));
                }
            }
            return entries;
        }

        private static string Resolve(XElement element, string expression, Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;
            if (!expression.StartsWith("{", StringComparison.Ordinal)) return expression;
            if (!expression.StartsWith("{x:Static ", StringComparison.Ordinal) || !expression.EndsWith("}", StringComparison.Ordinal)) return null;
            string member = expression.Substring(10, expression.Length - 11).Trim();
            int colon = member.IndexOf(':'), dot = member.LastIndexOf('.');
            if (colon < 0 || dot <= colon) return null;
            string ns = element.GetNamespaceOfPrefix(member.Substring(0, colon))?.NamespaceName;
            const string prefix = "clr-namespace:";
            if (ns == null || !ns.StartsWith(prefix, StringComparison.Ordinal)) return null;
            ns = ns.Substring(prefix.Length).Split(';')[0];
            // Only application resource properties may be evaluated.
            if (ns != "Ink_Canvas.Properties") return null;
            var type = assembly.GetType(ns + "." + member.Substring(colon + 1, dot - colon - 1));
            var property = type?.GetProperty(member.Substring(dot + 1), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.PropertyType == typeof(string) ? (string)property.GetValue(null) : null;
        }
    }
}
