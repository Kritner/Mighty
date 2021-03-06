﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Mighty
{
    /// <summary>
    /// Specifies that this data member is a primary key. Compound - multiple - primary keys are supported.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DatabasePrimaryKeyAttribute : Attribute
    {
    }
}
