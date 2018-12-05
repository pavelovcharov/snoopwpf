// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Reflection;
using System.Windows;
using System.Windows.Shell;
using CommandLine;
using ReflectionFramework.Attributes;
using ReflectionFramework.Extensions;
using Snoop.Properties;
using Snoop.Startup;

namespace Snoop {
    public interface IStartupEventArgsWrapper {
        [BindingFlags(BindingFlags.Instance|BindingFlags.NonPublic)]
        bool PerformDefaultAction { get; set; }
    }
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            
            var sOptions = new StartupOptions();
            new Parser().ParseArguments(e.Args, sOptions);

            if (sOptions.ShowOptions) {
                e.Wrap<IStartupEventArgsWrapper>().PerformDefaultAction = false;
                new OptionsDialog().ShowDialog();
                Shutdown();
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
            Settings.Default.Save();
        }
    }
}