using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Core.Internal;

namespace Snoop {
    public interface IThemeManager {
        void SetThemeName(DependencyObject dObj, string themeName);
    }

    public static class ThemeManagerHelper {
        public static void SetThemeName(DependencyObject dObj, string themeName) {
            var tThemeManager = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(
                    x => x.FullName?.Contains("DevExpress.Xpf.Core") ?? false)
                ?.GetType("DevExpress.Xpf.Utils.Themes.ThemeManager");
            if (tThemeManager != null)
                tThemeManager.Wrap<IThemeManager>().SetThemeName(dObj, themeName);
        }
    }
}
