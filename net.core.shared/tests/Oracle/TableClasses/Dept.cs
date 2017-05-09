﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mighty.Tests.Oracle.TableClasses
{
	public class Department : MightyORM
	{
		public Department(string providerName) : this(providerName, true)
		{
		}


		public Department(string providerName, bool includeSchema) 
			: base(string.Format(TestConstants.ReadWriteTestConnection, providerName), includeSchema ? "SCOTT.DEPT" : "DEPT", "DEPTNO", string.Empty, includeSchema ? "SCOTT.DEPT_SEQ" : "DEPT_SEQ")
		{
			
		}
	}
}