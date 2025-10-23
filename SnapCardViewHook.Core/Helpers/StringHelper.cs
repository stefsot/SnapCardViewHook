using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

namespace SnapCardViewHook.Core.Helpers
{
    internal static class StringHelper
    {
        public static string Format(string template, object context, string name = "context")
        {
            return Regex.Replace(template, $@"\{{{name}\.(\w+)\}}", match =>
            {
                string propName = match.Groups[1].Value;
                PropertyInfo prop = context.GetType().GetProperty(propName, (BindingFlags)~0);
                object value = prop?.GetValue(context);
                return value?.ToString() ?? string.Empty;
            });
        }

        public static string CleanupHmtl(string s)
        {
            return Regex.Replace(s, "<.*?>", string.Empty);
        }
    }
}
