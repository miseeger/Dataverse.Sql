using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace Dataverse.Sql.Tests
{

    [TestClass]
    public class DataverseSqlTests : FakeXrmEasyTestsBase
    {

        [TestMethod]
        public void SelectArithmetic()
        {
            var query =
                @"SELECT
                    employees + 1 AS a
                    ,employees * 2 AS b
                    ,turnover / 3 AS c
                    ,turnover - 4 AS d
                    ,turnover / employees AS e
                FROM
                    account";

            var id = Guid.NewGuid();
            _context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [id] = new Entity("account", id)
                {
                    ["accountid"] = id,
                    ["employees"] = 2,
                    ["turnover"] = new Money(9)
                }
            };

            using var dvSql = new DataverseSql(_localDataSource);

            var result = dvSql.RetrieveJson(query);

            Assert.AreEqual(
                @"[
  {
    ""a"": 3,
    ""b"": 4,
    ""c"": 3.0000,
    ""d"": 5.0000,
    ""e"": 4.5000
  }
]", result);
        }
    }

}
