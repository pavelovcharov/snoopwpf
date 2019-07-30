using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Snoop {
    public static class RegistryHelper {
        const string subKeyName = @"SOFTWARE\Snoop";

        static RegistryHelper() {
            using (var key = GetKey()) {
                if (key == null)
                    Registry.CurrentUser.CreateSubKey(subKeyName).Close();
            }
        }

        static RegistryKey GetKey() {
            return Registry.CurrentUser.OpenSubKey(subKeyName, true);
        }

        static void SetValue(string valueName, object value, RegistryValueKind kind) {
            using (var key = GetKey()) {
                key.SetValue(valueName, value, kind);
            }
        }
        public static void SetDouble(double value, [CallerMemberName]string valueName = null) {
            SetString(Convert.ToString(value), valueName);
        }
        public static void SetString(string value, [CallerMemberName]string valueName = null) {            
            SetValue(valueName, value, RegistryValueKind.String);
        }

        public static void SetInt(int value, [CallerMemberName]string valueName = null) {
            SetValue(valueName, value, RegistryValueKind.DWord);
        }
        public static void SetBool(bool value, [CallerMemberName]string valueName = null) {
            SetInt(value ? 1 : 0, valueName);
        }

        public static double? GetDouble([CallerMemberName]string valueName = null) {
            var res = GetString(valueName);
            return res == null ? null : (double?)Double.Parse(res);
        }
        public static string GetString([CallerMemberName]string valueName = null) {
            return (string)GetKey().GetValue(valueName);
        }
        public static int? GetInt([CallerMemberName]string valueName = null) {
            return (int?)GetKey().GetValue(valueName);
        }
        public static bool? GetBool([CallerMemberName]string valueName = null) {
            var iVal = GetInt(valueName);
            if (iVal.HasValue)
                return iVal == 1;
            return null;
        }
    }
}
