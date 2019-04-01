﻿using System;
using System.Collections;
using System.Collections.Async;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Mighty.Generic.Tests.SqlServer.TableClasses;
using NUnit.Framework;

namespace Mighty.Generic.Tests.SqlServer
{
	[TestFixture]
	public class WriteTests
	{
		[Test]
		public async Task Insert_SingleRow()
		{
			var categories = new Categories();
			var inserted = await categories.InsertAsync(new {CategoryName = "Cool stuff", Description = "You know... cool stuff! Cool. n. stuff."});
			int insertedCategoryID = inserted.CategoryID;
			Assert.IsTrue(insertedCategoryID > 0);
		}


		[Test]
		public async Task Insert_MultipleRows()
		{
			var categories = new Categories();
			var toInsert = new List<dynamic>();
			toInsert.Add(new { CategoryName = "Cat Insert_MR", Description = "cat 1 desc" });
			toInsert.Add(new { CategoryName = "Cat Insert_MR", Description = "cat 2 desc" });
			Assert.AreEqual(2, await categories.InsertAsync(toInsert.ToArray()));
			var inserted = await (await categories.AllAsync(where: "CategoryName=@0", args: (string)toInsert[0].CategoryName)).ToListAsync();
			Assert.AreEqual(2, inserted.Count);
			foreach(var c in inserted)
			{
				Assert.IsTrue(c.CategoryID > 0);
				Assert.AreEqual("Cat Insert_MR", c.CategoryName);
			}
		}


		[Test]
		public async Task Update_SingleRow()
		{
			dynamic categories = new Categories();
			// insert something to update first. 
			Category inserted = await categories.InsertAsync(new { CategoryName = "Cool stuff", Description = "You know... cool stuff! Cool. n. stuff." });
			int insertedCategoryID = inserted.CategoryID;
			Assert.IsTrue(insertedCategoryID > 0);
			// update it, with a better description
			inserted.Description = "This is all jolly marvellous";
			Assert.AreEqual(1, categories.Update(inserted), "Update should have affected 1 row");
			Category updatedRow = categories.Find(CategoryID: inserted.CategoryID);
			Assert.IsNotNull(updatedRow);
			Assert.AreEqual(inserted.CategoryID, updatedRow.CategoryID);
			Assert.AreEqual(inserted.Description, updatedRow.Description);
			// reset description to NULL
			updatedRow.Description = null;
			Assert.AreEqual(1, categories.Update(updatedRow), "Update should have affected 1 row");
			var newUpdatedRow = categories.Find(CategoryID: updatedRow.CategoryID);
			Assert.IsNotNull(newUpdatedRow);
			Assert.AreEqual(updatedRow.CategoryID, newUpdatedRow.CategoryID);
			Assert.AreEqual(updatedRow.Description, newUpdatedRow.Description);
		}


		[Test]
		public async Task Update_MultipleRows()
		{
			// first insert 2 categories and 4 products, one for each category
			var categories = new Categories();
			var insertedCategory1 = await categories.InsertAsync(new {CategoryName = "Category 1", Description = "Cat 1 desc"});
			int category1ID = insertedCategory1.CategoryID;
			Assert.IsTrue(category1ID > 0);
			var insertedCategory2 = await categories.InsertAsync(new { CategoryName = "Category 2", Description = "Cat 2 desc" });
			int category2ID = insertedCategory2.CategoryID;
			Assert.IsTrue(category2ID > 0);

			var products = new Products();
			for(int i = 0; i < 4; i++)
			{
				var category = i % 2 == 0 ? insertedCategory1 : insertedCategory2;
				var p = await products.InsertAsync(new {ProductName = "Prod" + i, CategoryID = category.CategoryID});
				Assert.IsTrue(p.ProductID > 0);
			}
			var allCat1Products = await (await products.AllAsync(where:"WHERE CategoryID=@0", args:category1ID)).ToArrayAsync();
			Assert.AreEqual(2, allCat1Products.Length);
			foreach(var p in allCat1Products)
			{
				Assert.AreEqual(category1ID, p.CategoryID);
				p.CategoryID = category2ID;
			}
			Assert.AreEqual(2, products.SaveAsync(allCat1Products));
		}


		[Test]
		public async Task Delete_SingleRow()
		{
			// first insert 2 categories
			var categories = new Categories();
			var insertedCategory1 = await categories.InsertAsync(new { CategoryName = "Cat Delete_SR", Description = "cat 1 desc" });
			int category1ID = insertedCategory1.CategoryID;
			Assert.IsTrue(category1ID > 0);
			var insertedCategory2 = await categories.InsertAsync(new { CategoryName = "Cat Delete_SR", Description = "cat 2 desc" });
			int category2ID = insertedCategory2.CategoryID;
			Assert.IsTrue(category2ID > 0);

			Assert.AreEqual(1, await categories.DeleteAsync(category1ID), "Delete should affect 1 row");
			var categoriesFromDB = await (await categories.AllAsync(where:"CategoryName=@0", args:(string)insertedCategory2.CategoryName)).ToListAsync();
			Assert.AreEqual(1, categoriesFromDB.Count);
			Assert.AreEqual(category2ID, categoriesFromDB[0].CategoryID);
		}


		[Test]
		public async Task Delete_MultiRow()
		{
			// first insert 2 categories
			var categories = new Categories();
			var insertedCategory1 = await categories.InsertAsync(new { CategoryName = "Cat Delete_MR", Description = "cat 1 desc" });
			int category1ID = insertedCategory1.CategoryID;
			Assert.IsTrue(category1ID > 0);
			var insertedCategory2 = await categories.InsertAsync(new { CategoryName = "Cat Delete_MR", Description = "cat 2 desc" });
			int category2ID = insertedCategory2.CategoryID;
			Assert.IsTrue(category2ID > 0);

			Assert.AreEqual(2, await categories.DeleteAsync(where: "CategoryName=@0", args: (string)insertedCategory1.CategoryName), "Delete should affect 2 rows");
			var categoriesFromDB = await (await categories.AllAsync(where: "CategoryName=@0", args: (string)insertedCategory2.CategoryName)).ToListAsync();
			Assert.AreEqual(0, categoriesFromDB.Count);
		}


		[OneTimeTearDown]
		public async Task CleanUp()
		{
			var db = new MightyOrm(TestConstants.WriteTestConnection);
			await db.ExecuteProcedureAsync("pr_clearAll");
		}
	}
}
