using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Snoop {
    public static class RegistrySettings {
        public static bool Pinned {
            get { return RegistryHelper.GetBool() ?? false; }
            set { RegistryHelper.SetBool(value);}
        }
        public static int Left {
            get { return RegistryHelper.GetInt() ?? 0; }
            set { RegistryHelper.SetInt(value); }
        }
        public static int Top {
            get { return RegistryHelper.GetInt() ?? 0; }
            set { RegistryHelper.SetInt(value); }
        }

        public static Orientation Orientation {
            get { return (Orientation)Enum.Parse(typeof(Orientation), RegistryHelper.GetString() ?? "Horizontal"); }
            set { RegistryHelper.SetString(value.ToString()); }
        }
    }
}
