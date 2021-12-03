using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dataverse.Sql.Tests
{
    [TestClass]
    public class DataverseSqlConfigTests
    {
        [TestMethod]
        public void LoadsConfig()
        {
            _ = new DataverseSql();

            Assert.AreEqual("true", DataverseSql.Config["useLocalTimeZone"]);
            Assert.AreEqual("false", DataverseSql.Config["cancelled"]);
            Assert.AreEqual("true", DataverseSql.Config["blockUpdateWithoutWhere"]);
            Assert.AreEqual("false", DataverseSql.Config["blockDeleteWithoutWhere"]);
            Assert.AreEqual("false", DataverseSql.Config["useBulkDelete"]);
            Assert.AreEqual("1", DataverseSql.Config["batchSize"]);
            Assert.AreEqual("false", DataverseSql.Config["useTdsEndpoint"]);
            Assert.AreEqual("true", DataverseSql.Config["useRetrieveTotalRecordCount"]);
            Assert.AreEqual("10", DataverseSql.Config["maxDegreeOfParallelism"]);
            Assert.AreEqual("true", DataverseSql.Config["columnComparisonAvailable"]);
            Assert.AreEqual("false", DataverseSql.Config["bypassCustomPlugins"]);
        }

        [TestMethod]
        public void CreatesDataverseSqlWithOptionsFromConfig()
        {
            var dSql = new DataverseSql
            {
                Options = new QueryExecutionOptions()
            };

            Assert.AreEqual(true, dSql.Options.UseLocalTimeZone);
            Assert.AreEqual(false, dSql.Options.Cancelled);
            Assert.AreEqual(true, dSql.Options.BlockUpdateWithoutWhere);
            Assert.AreEqual(false, dSql.Options.BlockDeleteWithoutWhere);
            Assert.AreEqual(false, dSql.Options.UseBulkDelete);
            Assert.AreEqual(1, dSql.Options.BatchSize);
            Assert.AreEqual(false, dSql.Options.UseTDSEndpoint);
            Assert.AreEqual(true, dSql.Options.UseRetrieveTotalRecordCount);
            Assert.AreEqual(10, dSql.Options.MaxDegreeOfParallelism);
            Assert.AreEqual(true, dSql.Options.ColumnComparisonAvailable);
            Assert.AreEqual(false, dSql.Options.BypassCustomPlugins);
        }
    }
}
