using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class StringUtil
    {
        private static readonly object[] DefaultFormatArgs = new object[] { null };
        private static Dictionary<string, object> _locStrings;

        public static string Format(string format, params object[] args)
        {
            return Format(CultureInfo.InvariantCulture, format, args);
        }

        // Do not combine the non-format overload with the format overload.
        public static string Loc(string locKey)
        {
            //
            // TODO: Replace this custom little loc impl with proper one after confirming OSX/Linux support
            //
            string locStr = locKey;
            
            try
            {
                EnsureLoaded();

                if (_locStrings.ContainsKey(locKey))
                {
                    Object item = _locStrings[locKey];

                    Type t = item.GetType();

                    if (t == typeof(string))
                    {
                        locStr = _locStrings[locKey].ToString();
                    }
                    else if (t == typeof(JArray))
                    {
                        string[] lines = ((JArray)item).ToObject<string[]>();
                        StringBuilder sb = new StringBuilder();
                        foreach (string line in lines)
                        {
                            sb.Append(line);
                            sb.Append(Environment.NewLine);
                        }

                        locStr = sb.ToString();
                    }
                }
                else
                {
                    locStr = StringUtil.Format("notFound:{0}", locKey);
                }
            }
            catch (Exception)
            {
                // loc strings shouldn't take down agent.  any failures returns loc key
            }
            
            return locStr;
        }

        // Do not combine the non-format overload with the format overload.
        public static string Loc(string locKey, params object[] args)
        {
            return Format(CultureInfo.CurrentCulture, Loc(locKey), args);
        }

        /// <summary>
        /// Convert String to boolean, valid true string: "1", "true", "$true", valid false string: "0", "false", "$false".
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue">default result when value is null or empty or not a valid true/false string.</param>
        /// <returns></returns>
        public static bool ConvertToBoolean(string value, bool? defaultValue = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue ?? false;
            }

            switch (value.ToLowerInvariant())
            {
                case "1":
                case "true":
                case "$true":
                    return true;
                case "0":
                case "false":
                case "$false":
                    return false;
                default:
                    return defaultValue ?? false;
            }
        }

        private static string Format(CultureInfo culture, string format, params object[] args)
        {
            try
            {
                // 1) Protect against argument null exception for the format parameter.
                // 2) Protect against argument null exception for the args parameter.
                // 3) Coalesce null or empty args with an array containing one null element.
                //    This protects against format exceptions where string.Format thinks
                //    that not enough arguments were supplied, even though the intended arg
                //    literally is null or an empty array.
                return string.Format(
                    culture,
                    format ?? string.Empty,
                    args == null || args.Length == 0 ? DefaultFormatArgs : args);
            }
            catch (FormatException)
            {
                // TODO: Log that string format failed. Consider moving this into a context base class if that's the only place it's used. Then the current trace scope would be available as well.
                if (args != null)
                {
                    return string.Format(culture, "{0} {1}", format, string.Join(", ", args));
                }

                return format;
            }
        }

        private static void EnsureLoaded()
        {
            if (_locStrings == null)
            {
                string stringsPath = Path.Combine(IOUtil.GetBinPath(), 
                                                CultureInfo.CurrentCulture.Name,
                                                "strings.json");
                                                
                _locStrings = IOUtil.LoadObject<Dictionary<string, Object>>(stringsPath);
            }
        }
    }
}