using System;
using System.ComponentModel;

namespace Snoop.DebugListenerTab {
    [Serializable]
    public abstract class SnoopFilter : INotifyPropertyChanged {
        protected string _groupId = string.Empty;
        protected bool _isDirty;
        protected bool _isGrouped;
        protected bool _isInverse;

        public bool IsDirty {
            get { return _isDirty; }
        }

        public virtual bool SupportsGrouping {
            get { return true; }
        }

        public bool IsInverse {
            get { return _isInverse; }
            set {
                if (value != _isInverse) {
                    _isInverse = value;
                    RaisePropertyChanged("IsInverse");
                    RaisePropertyChanged("IsInverseText");
                }
            }
        }

        public string IsInverseText {
            get { return _isInverse ? "NOT" : string.Empty; }
        }

        public bool IsGrouped {
            get { return _isGrouped; }
            set {
                _isGrouped = value;
                RaisePropertyChanged("IsGrouped");
                GroupId = string.Empty;
            }
        }

        public virtual string GroupId {
            get { return _groupId; }
            set {
                _groupId = value;
                RaisePropertyChanged("GroupId");
            }
        }

        //protected string _isInverseText = string.Empty;

        public void ResetDirtyFlag() {
            _isDirty = false;
        }

        public abstract bool FilterMatches(string debugLine);

        protected void RaisePropertyChanged(string propertyName) {
            _isDirty = true;
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}