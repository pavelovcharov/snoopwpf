﻿// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;

namespace Snoop.Infrastructure {
    public interface ISkipDelve {
        object NextValue { get; }

        Type NextValueType { get; }
    }
}