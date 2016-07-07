// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Snoop.MethodsTab {
    public class SnoopMethodInformation : IComparable, IEquatable<SnoopMethodInformation> {
        public SnoopMethodInformation(MethodInfo methodInfo) {
            MethodInfo = methodInfo;
        }

        public string MethodName { get; set; }

        public MethodInfo MethodInfo { get; }

        public override string ToString() {
            return MethodName;
        }

        public IList<SnoopParameterInformation> GetParameters(Type declaringType) {
            if (MethodInfo == null)
                return new List<SnoopParameterInformation>();

            var parameterInfos = MethodInfo.GetParameters();


            var parametersToReturn = new List<SnoopParameterInformation>();

            foreach (var parameterInfo in parameterInfos) {
                var snoopParameterInfo = new SnoopParameterInformation(parameterInfo, declaringType);
                parametersToReturn.Add(snoopParameterInfo);
            }

            return parametersToReturn;
        }

        #region IComparable Members

        public int CompareTo(object obj) {
            return MethodName.CompareTo(((SnoopMethodInformation) obj).MethodName);
        }

        #endregion

        #region IEquatable<SnoopMethodInformation> Members

        public bool Equals(SnoopMethodInformation other) {
            if (other == null)
                return false;

            if (other.MethodName != MethodName)
                return false;

            if (!other.MethodInfo.ReturnType.Equals(MethodInfo.ReturnType))
                return false;

            var thisParameterInfos = MethodInfo.GetParameters();
            var otherParameterInfos = other.MethodInfo.GetParameters();

            if (thisParameterInfos.Length != otherParameterInfos.Length)
                return false;

            for (var i = 0; i < thisParameterInfos.Length; i++) {
                var thisParameterInfo = thisParameterInfos[i];
                var otherParameterInfo = otherParameterInfos[i];

                //if (!thisParameterInfo.Equals(otherParameterInfo))
                if (!ParameterInfosEqual(thisParameterInfo, otherParameterInfo))
                    return false;
            }

            return true;
        }

        bool ParameterInfosEqual(ParameterInfo parm1, ParameterInfo parm2) {
            if (!parm1.Name.Equals(parm2.Name))
                return false;

            if (!parm1.ParameterType.Equals(parm2.ParameterType))
                return false;

            return parm1.Position == parm2.Position;
        }

        #endregion
    }
}