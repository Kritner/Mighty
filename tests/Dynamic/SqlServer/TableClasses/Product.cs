﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mighty.Dynamic.Tests.SqlServer.TableClasses
{
    public class Product : MightyOrm
    {
        public Product() : this(includeSchema:true)
        {
        }


        public Product(bool includeSchema) :
            base(TestConstants.WriteTestConnection, includeSchema ? "dbo.Products" : "Products", "ProductID")
        {
        }
    }
}
