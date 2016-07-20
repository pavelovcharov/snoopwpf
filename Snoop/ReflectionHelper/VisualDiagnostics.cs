using System.Reflection;
using System.Windows.Diagnostics;
using ReflectionFramework;

namespace Snoop {
    public interface IVisualDiagnostics {
        [ReflectionHelperAttributes.FieldAccessor]
        [ReflectionHelperAttributes.Name("s_isDebuggerCheckDisabledForTestPurposes")]
        [ReflectionHelperAttributes.BindingFlags(BindingFlags.NonPublic)]
        bool Enabled { get; set; }
    }

    public static class VisualDiagnosticsExtensions {
        static bool enabled;
        static readonly IVisualDiagnostics instanceVisualDiagnostics;

        static VisualDiagnosticsExtensions() {
            instanceVisualDiagnostics = typeof(VisualDiagnostics).Wrap<IVisualDiagnostics>();
        }

        public static bool Enabled {
            get { return enabled; }
            set { enabled = instanceVisualDiagnostics.Enabled = value; }
        }
    }
}