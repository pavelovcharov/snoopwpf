// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.ComponentModel;
using System.Windows;

namespace Snoop {
    public class PertinentPropertyFilter {
        readonly FrameworkElement element;


        readonly object target;

        public PertinentPropertyFilter(object target) {
            this.target = target;
            element = this.target as FrameworkElement;
        }


        public bool Filter(PropertyDescriptor property) {
            if (this.element == null)
                return true;

            // Filter the 20 stylistic set properties that I've never seen used.
            if (property.Name.Contains("Typography.StylisticSet"))
                return false;

            var attachedPropertyForChildren =
                (AttachedPropertyBrowsableForChildrenAttribute)
                    property.Attributes[typeof(AttachedPropertyBrowsableForChildrenAttribute)];
            var attachedPropertyForType =
                (AttachedPropertyBrowsableForTypeAttribute)
                    property.Attributes[typeof(AttachedPropertyBrowsableForTypeAttribute)];
            var attachedPropertyForAttribute =
                (AttachedPropertyBrowsableWhenAttributePresentAttribute)
                    property.Attributes[typeof(AttachedPropertyBrowsableWhenAttributePresentAttribute)];

            if (attachedPropertyForChildren != null) {
                var dpd = DependencyPropertyDescriptor.FromProperty(property);
                if (dpd == null)
                    return false;

                var element = this.element;
                do {
                    element = element.Parent as FrameworkElement;
                    if (element != null && dpd.DependencyProperty.OwnerType.IsInstanceOfType(element))
                        return true;
                } while (attachedPropertyForChildren.IncludeDescendants && element != null);
                return false;
            }
            if (attachedPropertyForType != null) {
                // when using [AttachedPropertyBrowsableForType(typeof(IMyInterface))] and IMyInterface is not a DependencyObject, Snoop crashes.
                // see http://snoopwpf.codeplex.com/workitem/6712

                if (attachedPropertyForType.TargetType.IsSubclassOf(typeof(DependencyObject))) {
                    var doType = DependencyObjectType.FromSystemType(attachedPropertyForType.TargetType);
                    if (doType != null && doType.IsInstanceOfType(element))
                        return true;
                }

                return false;
            }
            if (attachedPropertyForAttribute != null) {
                var dependentAttribute =
                    TypeDescriptor.GetAttributes(target)[attachedPropertyForAttribute.AttributeType];
                if (dependentAttribute != null)
                    return !dependentAttribute.IsDefaultAttribute();
                return false;
            }

            return true;
        }
    }
}