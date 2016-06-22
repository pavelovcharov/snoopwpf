using System.Reflection;
using System.Windows.Diagnostics;
using DevExpress.Xpf.Core.Internal;

namespace Snoop {
    public interface IVisualDiagnostics {
        bool Enabled { get; set; }
    }

    public static class VisualDiagnosticsExtensions {
        static bool enabled;
        static readonly IVisualDiagnostics instanceVisualDiagnostics;

        static VisualDiagnosticsExtensions() {
            instanceVisualDiagnostics = typeof(VisualDiagnostics).DefineWrapper<IVisualDiagnostics>()
                .DefineProperty(x => x.Enabled)
                .FieldAccessor()
                .BindingFlags(BindingFlags.NonPublic)
                .Name("s_isDebuggerCheckDisabledForTestPurposes")
                .EndMember()
                .Create();
        }

        public static bool Enabled {
            get { return enabled; }
            set { enabled = instanceVisualDiagnostics.Enabled = value; }
        }
    }
}