// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace Snoop {
    public delegate void EventTrackerHandler(TrackedEvent newEvent);


    /// <summary>
    ///     Random class that tries to determine what element handled a specific event.
    ///     Doesn't work too well in the end, because the static ClassHandler doesn't get called
    ///     in a consistent order.
    /// </summary>
    public class EventTracker : INotifyPropertyChanged, IComparable {
        readonly Type targetType;
        TrackedEvent currentEvent;
        bool everEnabled;
        bool isEnabled;

        public EventTracker(Type targetType, RoutedEvent routedEvent) {
            this.targetType = targetType;
            RoutedEvent = routedEvent;
        }


        public bool IsEnabled {
            get { return isEnabled; }
            set {
                if (isEnabled != value) {
                    isEnabled = value;
                    if (isEnabled && !everEnabled) {
                        everEnabled = true;
                        EventManager.RegisterClassHandler(targetType, RoutedEvent, new RoutedEventHandler(HandleEvent),
                            true);
                    }
                    OnPropertyChanged("IsEnabled");
                }
            }
        }

        public RoutedEvent RoutedEvent { get; }

        public string Category {
            get { return RoutedEvent.OwnerType.Name; }
        }

        public string Name {
            get { return RoutedEvent.Name; }
        }


        public event EventTrackerHandler EventHandled;


        void HandleEvent(object sender, RoutedEventArgs e) {
            // Try to figure out what element handled the event. Not precise.
            if (isEnabled) {
                var entry = new EventEntry(sender, e.Handled);
                if (currentEvent != null && currentEvent.EventArgs == e) {
                    currentEvent.AddEventEntry(entry);
                }
                else {
                    currentEvent = new TrackedEvent(e, entry);
                    EventHandled(currentEvent);
                }
            }
        }

        #region IComparable Members

        public int CompareTo(object obj) {
            var otherTracker = obj as EventTracker;
            if (otherTracker == null)
                return 1;

            if (Category == otherTracker.Category)
                return RoutedEvent.Name.CompareTo(otherTracker.RoutedEvent.Name);
            return Category.CompareTo(otherTracker.Category);
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }


    [DebuggerDisplay("TrackedEvent: {EventArgs}")]
    public class TrackedEvent : INotifyPropertyChanged {
        bool handled;
        object handledBy;

        public TrackedEvent(RoutedEventArgs routedEventArgs, EventEntry originator) {
            EventArgs = routedEventArgs;
            AddEventEntry(originator);
        }


        public RoutedEventArgs EventArgs { get; }

        public EventEntry Originator {
            get { return Stack[0]; }
        }

        public bool Handled {
            get { return handled; }
            set {
                handled = value;
                OnPropertyChanged("Handled");
            }
        }

        public object HandledBy {
            get { return handledBy; }
            set {
                handledBy = value;
                OnPropertyChanged("HandledBy");
            }
        }

        public ObservableCollection<EventEntry> Stack { get; } = new ObservableCollection<EventEntry>();


        public void AddEventEntry(EventEntry eventEntry) {
            Stack.Add(eventEntry);
            if (eventEntry.Handled && !Handled) {
                Handled = true;
                HandledBy = eventEntry.Handler;
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


    public class EventEntry {
        public EventEntry(object handler, bool handled) {
            Handler = handler;
            Handled = handled;
        }

        public bool Handled { get; }

        public object Handler { get; }
    }
}