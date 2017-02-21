// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Snoop {
    /// <summary>
    ///     Class that shows all the routed events occurring on a visual.
    ///     VERY dangerous (cannot unregister for the events) and doesn't work all that great.
    ///     Stay far away from this code :)
    /// </summary>
    public class EventsListener {
        static EventsListener current;

        static readonly Dictionary<Type, Type> registeredTypes = new Dictionary<Type, Type>();
        public static string filter;
        readonly Visual visual;

        public EventsListener(Visual visual) {
            current = this;
            this.visual = visual;

            var type = visual.GetType();

            // Cannot unregister for events once we've registered, so keep the registration simple and only do it once.
            for (var baseType = type; baseType != null; baseType = baseType.BaseType) {
                if (!registeredTypes.ContainsKey(baseType)) {
                    registeredTypes[baseType] = baseType;

                    var routedEvents = EventManager.GetRoutedEventsForOwner(baseType);
                    if (routedEvents != null) {
                        foreach (var routedEvent in routedEvents)
                            EventManager.RegisterClassHandler(baseType, routedEvent, new RoutedEventHandler(HandleEvent),
                                true);
                    }
                }
            }
        }

        public ObservableCollection<EventInformation> Events { get; } = new ObservableCollection<EventInformation>();

        public static string Filter {
            get { return filter; }
            set {
                filter = value;
                if (filter != null)
                    filter = filter.ToLower();
            }
        }

        public static void Stop() {
            current = null;
        }


        static void HandleEvent(object sender, RoutedEventArgs e) {
            if (current != null && sender == current.visual) {
                if (string.IsNullOrEmpty(Filter) || e.RoutedEvent.Name.ToLower().Contains(Filter)) {
                    current.Events.Add(new EventInformation(e));

                    while (current.Events.Count > 100)
                        current.Events.RemoveAt(0);
                }
            }
        }
    }

    public class EventInformation {
        readonly RoutedEventArgs evt;

        public EventInformation(RoutedEventArgs evt) {
            this.evt = evt;
        }

        public IEnumerable Properties {
            get { return PropertyInformation.GetProperties(evt); }
        }

        public override string ToString() {
            return string.Format("{0} Handled: {1} OriginalSource: {2}", evt.RoutedEvent.Name, evt.Handled,
                evt.OriginalSource);
        }
    }
}