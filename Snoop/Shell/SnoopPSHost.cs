// (c) Copyright Bailey Ling.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;
using System.Threading;

namespace Snoop.Shell {
    internal class SnoopPSHost : PSHost {
        readonly Guid id = Guid.NewGuid();
        readonly Hashtable privateHashtable;
        readonly SnoopPSHostUserInterface ui;

        public SnoopPSHost(Action<string> onOutput) {
            ui = new SnoopPSHostUserInterface();
            ui.OnDebug += onOutput;
            ui.OnError += onOutput;
            ui.OnVerbose += onOutput;
            ui.OnWarning += onOutput;
            ui.OnWrite += onOutput;

            privateHashtable = new Hashtable();
            PrivateData = new PSObject(privateHashtable);
        }

        public override CultureInfo CurrentCulture {
            get { return Thread.CurrentThread.CurrentCulture; }
        }

        public override CultureInfo CurrentUICulture {
            get { return Thread.CurrentThread.CurrentUICulture; }
        }

        public override Guid InstanceId {
            get { return id; }
        }

        public override string Name {
            get { return id.ToString(); }
        }

        public override PSHostUserInterface UI {
            get { return ui; }
        }

        public override Version Version {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override PSObject PrivateData { get; }

        public object this[string name] {
            get { return privateHashtable[name]; }
            set { privateHashtable[name] = value; }
        }

        public override void SetShouldExit(int exitCode) {}

        public override void EnterNestedPrompt() {}

        public override void ExitNestedPrompt() {}

        public override void NotifyBeginApplication() {}

        public override void NotifyEndApplication() {}
    }
}