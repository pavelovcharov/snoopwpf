// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Reflection;
using System.Windows;
using System.Windows.Shell;
using CommandLine;
using Snoop.Properties;
using Snoop.Startup;

namespace Snoop {    
    public partial class App : Application {
//        static readonly Action<StartupEventArgs, bool> set_PerformDefaultAction = ReflectionHelper.CreateInstanceMethodHandler<StartupEventArgs, Action<StartupEventArgs, bool>>(null, "set_PerformDefaultAction", BindingFlags.NonPublic | BindingFlags.Instance);
//        static readonly Action<StartupEventArgs, bool> set_PerformDefaultAction = ReflectionHelper.CreateFieldSetter<StartupEventArgs, bool>(typeof(StartupEventArgs), "_performDefaultAction", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            var sOptions = new StartupOptions();
            new Parser().ParseArguments(e.Args, sOptions);

            if (sOptions.ShowOptions) {
//                set_PerformDefaultAction(e, false);
                new OptionsDialog().ShowDialog();
                Shutdown();
				return;
            }

            if (!RegistrySettings.PinnedView) {
//                set_PerformDefaultAction(e, false);
                new QuickWindowChooser();
//                set_PerformDefaultAction(e, false);
//                Shutdown();
//				return;
            } else {
                StartupUri = new System.Uri("Views/Windows/JLWindow.xaml", System.UriKind.Relative);
            }
            
            JumpTask task = new JumpTask
            {
                Title = "Options",
                Arguments = "--options",
                Description = "Show Options dialog",
                CustomCategory = "Actions",
                IconResourcePath = Assembly.GetEntryAssembly().CodeBase,
                ApplicationPath = Assembly.GetEntryAssembly().CodeBase 
            };
 
            JumpList jumpList = new JumpList();
            jumpList.JumpItems.Add(task);
            jumpList.ShowFrequentCategory = false;
            jumpList.ShowRecentCategory = false;
 
            JumpList.SetJumpList(Application.Current, jumpList);
                        

        }

        void Application_Exit(object sender, ExitEventArgs e) {
//            Settings.Default.Save();
        }
    }
}