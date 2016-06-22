using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Diagnostics;
using DevExpress.Xpf.Core.Internal;

namespace Snoop {
    public interface IVisualDiagnostics {
        bool Enabled { get; set; }
    }

    public static class VisualDiagnosticsExtensions {
        static VisualDiagnosticsExtensions() {
            instanceVisualDiagnostics = typeof(VisualDiagnostics).DefineWrapper<IVisualDiagnostics>()
                .DefineProperty(x => x.Enabled)
                .FieldAccessor()
                .BindingFlags(BindingFlags.NonPublic)
                .Name("s_isDebuggerCheckDisabledForTestPurposes")
                .EndMember()
                .Create();
        }
        private static bool enabled;
        private static IVisualDiagnostics instanceVisualDiagnostics;
        public static bool Enabled {
            get { return enabled; }
            set { enabled = instanceVisualDiagnostics.Enabled = value; }
        }
    }
}
