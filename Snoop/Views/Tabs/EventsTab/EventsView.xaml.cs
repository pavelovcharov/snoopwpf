// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Snoop.Infrastructure;

namespace Snoop {
    public partial class EventsView : INotifyPropertyChanged {
        public static readonly RoutedCommand ClearCommand = new RoutedCommand();


        static readonly List<RoutedEvent> defaultEvents =
            new List<RoutedEvent>
                (
                new[] {
                    Keyboard.KeyDownEvent,
                    Keyboard.KeyUpEvent,
                    TextCompositionManager.TextInputEvent,
                    Mouse.MouseDownEvent,
                    Mouse.PreviewMouseDownEvent,
                    Mouse.MouseUpEvent,
                    CommandManager.ExecutedEvent
                }
                );

        readonly ObservableCollection<TrackedEvent> interestingEvents = new ObservableCollection<TrackedEvent>();


        readonly ObservableCollection<EventTracker> trackers = new ObservableCollection<EventTracker>();


        public EventsView() {
            InitializeComponent();

            var sorter = new List<EventTracker>();

            foreach (var routedEvent in EventManager.GetRoutedEvents()) {
                var tracker = new EventTracker(typeof(UIElement), routedEvent);
                tracker.EventHandled += HandleEventHandled;
                sorter.Add(tracker);

                if (defaultEvents.Contains(routedEvent))
                    tracker.IsEnabled = true;
            }

            sorter.Sort();
            foreach (var tracker in sorter)
                trackers.Add(tracker);

            CommandBindings.Add(new CommandBinding(ClearCommand, HandleClear));
        }


        public IEnumerable InterestingEvents {
            get { return interestingEvents; }
        }

        public object AvailableEvents {
            get {
                var pgd = new PropertyGroupDescription();
                pgd.PropertyName = "Category";
                pgd.StringComparison = StringComparison.OrdinalIgnoreCase;

                var cvs = new CollectionViewSource();
                cvs.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
                cvs.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                cvs.GroupDescriptions.Add(pgd);

                cvs.Source = trackers;

                cvs.View.Refresh();
                return cvs.View;
            }
        }


        void HandleEventHandled(TrackedEvent trackedEvent) {
            var visual = trackedEvent.Originator.Handler as Visual;
            if (visual != null && !visual.IsPartOfSnoopVisualTree()) {
                Action action =
                    () => {
                        interestingEvents.Add(trackedEvent);

                        while (interestingEvents.Count > 100)
                            interestingEvents.RemoveAt(0);

                        var tvi = (TreeViewItem) EventTree.ItemContainerGenerator.ContainerFromItem(trackedEvent);
                        if (tvi != null)
                            tvi.BringIntoView();
                    };

                if (!Dispatcher.CheckAccess()) {
                    Dispatcher.BeginInvoke(action);
                }
                else {
                    action.Invoke();
                }
            }
        }

        void HandleClear(object sender, ExecutedRoutedEventArgs e) {
            interestingEvents.Clear();
        }

        void EventTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (e.NewValue != null) {
                if (e.NewValue is EventEntry)
                    SnoopUI.InspectCommand.Execute(((EventEntry) e.NewValue).Handler, this);
                else if (e.NewValue is TrackedEvent)
                    SnoopUI.InspectCommand.Execute(((TrackedEvent) e.NewValue).EventArgs, this);
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class InterestingEvent {
        public InterestingEvent(object handledBy, RoutedEventArgs eventArgs) {
            HandledBy = handledBy;
            TriggeredOn = null;
            EventArgs = eventArgs;
        }


        public RoutedEventArgs EventArgs { get; }


        public object HandledBy { get; }


        public object TriggeredOn { get; }


        public bool Handled {
            get { return HandledBy != null; }
        }
    }
}