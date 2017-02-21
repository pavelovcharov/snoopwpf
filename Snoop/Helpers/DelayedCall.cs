// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;
using System.Windows.Threading;
using Snoop.Infrastructure;

namespace Snoop {
    public delegate void DelayedHandler();

    public class DelayedCall {
        readonly DelayedHandler handler;
        readonly DispatcherPriority priority;

        bool queued;

        public DelayedCall(DelayedHandler handler, DispatcherPriority priority) {
            this.handler = handler;
            this.priority = priority;
        }

        public void Enqueue() {
            if (!queued) {
                queued = true;

                Dispatcher dispatcher = null;
                if (Application.Current == null || SnoopModes.MultipleDispatcherMode)
                    dispatcher = Dispatcher.CurrentDispatcher;
                else
                    dispatcher = Application.Current.Dispatcher;

                dispatcher.BeginInvoke(priority, new DispatcherOperationCallback(Process), null);
            }
        }


        object Process(object arg) {
            queued = false;

            handler();

            return null;
        }
    }
}