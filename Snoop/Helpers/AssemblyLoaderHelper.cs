using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Snoop.Helpers
{
    public class AssemblyLoaderHelper {
        static AssemblyLoaderHelper() {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            var directory = GetDirectory();
            foreach (var assembly in Assembly.GetCallingAssembly().GetReferencedAssemblies()) {
                var assemblyPath = Path.Combine(directory, $"{assembly.Name}.dll");
                var absoluteAssemblyPath = new Uri(assemblyPath).AbsolutePath;
                if(!File.Exists(absoluteAssemblyPath))
                    continue;
                Assembly.LoadFile(absoluteAssemblyPath);
            }            
        }

        static string GetDirectory() {
            try {
                var codeBase = Assembly.GetCallingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            } catch {
                return Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
            }
        }

        public static void Initialize() { }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.RequestingAssembly == null)
                return null;
            var file = Path.Combine(Path.GetDirectoryName(args.RequestingAssembly.Location),
                                    new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        }
    }
}
