using System;
using System.Collections.Generic;
using System.Linq;
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
            const string query = @"SELECT
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


        [TestMethod]
        public void SelectParameters()
        {
            const string query = "SELECT @param1, @param2";
            var queryParams = new Dictionary<string, object>
            {
                {"@param1", 1},
                {"@param2", "text"}
            };

            using var dvSql = new DataverseSql(_localDataSource);

            var result = dvSql.RetrieveJson(query, queryParams);

            Assert.AreEqual(
                @"[
  {
    ""Expr1"": 1,
    ""Expr2"": ""text""
  }
]", result);
        }


        [TestMethod]
        public void InsertRecordsAffected()
        {
            const string command = "INSERT INTO account (name) VALUES (@name)";
            var commandParams = new Dictionary<string, object>
            {
                {"@name", "'501st Legion'"}
            };

            using var dvSql = new DataverseSql(_localDataSource);
            var result = dvSql.Execute(command, commandParams);

            Assert.AreEqual("1 row successfully inserted", result);
            Assert.AreEqual(1, _context.Data["account"].Count);
            Assert.AreEqual("'501st Legion'", _context.Data["account"].Values.Single().GetAttributeValue<string>("name"));
        }


        [TestMethod]
        public void InsertRecordsAffectedMultipleCommands()
        {
            const string command = "INSERT INTO account (name) VALUES (@name); INSERT INTO account (name) VALUES (@name)";
            var commandParams = new Dictionary<string, object>
            {
                {"@name", "'501st Legion'"}
            };

            using var dvSql = new DataverseSql(_localDataSource);
            var result = dvSql.Execute(command, commandParams);

            Assert.AreEqual("2 rows successfully inserted", result);
            Assert.AreEqual(2, _context.Data["account"].Count);
            CollectionAssert.AreEqual(new[] { "'501st Legion'", "'501st Legion'" },
                _context.Data["account"].Values.Select(a => a.GetAttributeValue<string>("name")).ToArray());
        }


        [TestMethod]
        public void UpdateOneRow()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            _context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [id1] = new Entity("account", id1)
                {
                    ["accountid"] = id1,
                    ["name"] = "Test1"
                },
                [id2] = new Entity("account", id2)
                {
                    ["accountid"] = id2,
                    ["name"] = "Test2"
                }
            };

            const string command = "UPDATE account SET name = @param1 WHERE name = @param2";
            var commandParams = new Dictionary<string, object>
            {
                {"@param1", "Testx"},
                {"@param2", "Test1"}
            };

            using var dvSql = new DataverseSql(_localDataSource);
            var result = dvSql.Execute(command, commandParams);

            Assert.AreEqual("1 row successfully updated", result);
            CollectionAssert.AreEqual(new[] { "Testx", "Test2" },
                _context.Data["account"].Values.Select(a => a.GetAttributeValue<string>("name")).ToArray());
        }


        [TestMethod]
        public void UpdateMultipleRows()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            _context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [id1] = new Entity("account", id1)
                {
                    ["accountid"] = id1,
                    ["name"] = "Test1"
                },
                [id2] = new Entity("account", id2)
                {
                    ["accountid"] = id2,
                    ["name"] = "Test2"
                }
            };

            const string command = "UPDATE account SET name = @param1 WHERE 1 = 1";
            var commandParams = new Dictionary<string, object>
            {
                {"@param1", "Testx"}
            };

            using var dvSql = new DataverseSql(_localDataSource);
            var result = dvSql.Execute(command, commandParams);

            Assert.AreEqual("2 rows successfully updated", result);
            CollectionAssert.AreEqual(new[] { "Testx", "Testx" },
                _context.Data["account"].Values.Select(a => a.GetAttributeValue<string>("name")).ToArray());
        }


        [TestMethod]
        public void RetrieveScalar()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            _context.Data["account"] = new Dictionary<Guid, Entity>
            {
                [id1] = new Entity("account", id1)
                {
                    ["accountid"] = id1,
                    ["name"] = "Test1"
                },
                [id2] = new Entity("account", id2)
                {
                    ["accountid"] = id2,
                    ["name"] = "Test2"
                }
            };

            const string command = "SELECT COUNT(*) FROM account";
            
            using var dvSql = new DataverseSql(_localDataSource);
            var result = dvSql.RetrieveScalar<Int64>(command);

            Assert.AreEqual(2, result);
        }


        [TestMethod]
        public void ScriptControlOfFlow()
        {
            const string script = @"
IF @param1 = 1
    SELECT 'a'

IF @param1 = 2
    SELECT 'b'

WHILE @param1 < 10
BEGIN
    SELECT @param1
    SET @param1 += 1
END";
            var scriptParams = new Dictionary<string, object>
            {
                {"@param1", 1}
            };

            using var dvSql = new DataverseSql(_localDataSource);
            var results = dvSql.ExecuteScript(script, scriptParams);

            Assert.AreEqual(@"{
  ""ScriptResults"": [
    {
      ""Result0"": [
        {
          ""Expr1"": ""a""
        }
      ]
    },
    {
      ""Result1"": [
        {
          ""Expr3"": 1
        }
      ]
    },
    {
      ""Result2"": [
        {
          ""Expr3"": 2
        }
      ]
    },
    {
      ""Result3"": [
        {
          ""Expr3"": 3
        }
      ]
    },
    {
      ""Result4"": [
        {
          ""Expr3"": 4
        }
      ]
    },
    {
      ""Result5"": [
        {
          ""Expr3"": 5
        }
      ]
    },
    {
      ""Result6"": [
        {
          ""Expr3"": 6
        }
      ]
    },
    {
      ""Result7"": [
        {
          ""Expr3"": 7
        }
      ]
    },
    {
      ""Result8"": [
        {
          ""Expr3"": 8
        }
      ]
    },
    {
      ""Result9"": [
        {
          ""Expr3"": 9
        }
      ]
    }
  ]
}", results);
        }


        [TestMethod]
        public void ScriptGoTo()
        {
            const string script = @"
declare @param1 int = 1

goto label1

while @param1 < 10
begin
    select @param1

label2:
    set @param1 += 1
end

goto label3

label1:
    set @param1 = 2
    goto label2

label3:
    select 'end'";

            using var dvSql = new DataverseSql(_localDataSource);
            var results = dvSql.ExecuteScript(script);

            Assert.AreEqual(@"{
  ""ScriptResults"": [
    {
      ""Result0"": [
        {
          ""Expr2"": 3
        }
      ]
    },
    {
      ""Result1"": [
        {
          ""Expr2"": 4
        }
      ]
    },
    {
      ""Result2"": [
        {
          ""Expr2"": 5
        }
      ]
    },
    {
      ""Result3"": [
        {
          ""Expr2"": 6
        }
      ]
    },
    {
      ""Result4"": [
        {
          ""Expr2"": 7
        }
      ]
    },
    {
      ""Result5"": [
        {
          ""Expr2"": 8
        }
      ]
    },
    {
      ""Result6"": [
        {
          ""Expr2"": 9
        }
      ]
    },
    {
      ""Result7"": [
        {
          ""Expr5"": ""end""
        }
      ]
    }
  ]
}", results);
        }

    }
}
