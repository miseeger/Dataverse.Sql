using System;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using Dataverse.Sql.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dataverse.Sql.Tests
{
    [TestClass]
    public class DataTableConversionTests
    {

        static DataTable GetDataTable()
        {
            var dataTable = new DataTable("testTable");

            dataTable.Columns.Add("Cboolean", typeof(SqlBoolean));
            dataTable.Columns.Add("Cbyte", typeof(SqlByte));
            dataTable.Columns.Add("CdateTime", typeof(SqlDateTime));
            dataTable.Columns.Add("Cdecimal", typeof(SqlDecimal));
            dataTable.Columns.Add("Cdouble", typeof(SqlDouble));
            dataTable.Columns.Add("Cguid", typeof(SqlGuid));
            dataTable.Columns.Add("Cint16", typeof(SqlInt16));
            dataTable.Columns.Add("Cint32", typeof(SqlInt32));
            dataTable.Columns.Add("Cint64", typeof(SqlInt64));
            dataTable.Columns.Add("Cmoney", typeof(SqlMoney));
            dataTable.Columns.Add("Csingle", typeof(SqlSingle));
            dataTable.Columns.Add("Cstring", typeof(SqlString));
            dataTable.Columns.Add("Cnull", typeof(SqlInt16));


            var newRow1 = dataTable.NewRow();
            newRow1["Cboolean"] = SqlBoolean.Parse("true");
            newRow1["Cbyte"] = SqlByte.Parse("12");
            newRow1["CdateTime"] = SqlDateTime.Parse("2021-01-12 12:00:00");
            newRow1["Cdecimal"] = SqlDecimal.Parse("12.345");
            newRow1["Cdouble"] = SqlDouble.Parse("12.345678");
            newRow1["Cguid"] = SqlGuid.Parse("9600eef9-b8f8-4fbc-a11c-61d417f7b42d");
            newRow1["Cint16"] = SqlInt16.Parse("12345");
            newRow1["Cint32"] = SqlInt32.Parse("1234567890");
            newRow1["Cint64"] = SqlInt64.Parse("1234567890123");
            newRow1["Cmoney"] = SqlMoney.Parse("12.34");
            newRow1["Csingle"] = SqlSingle.Parse("12.3");
            newRow1["Cstring"] = new SqlString("First Row");
            newRow1["Cnull"] = new SqlInt16();
            dataTable.Rows.Add(newRow1);

            var newRow2 = dataTable.NewRow();
            newRow2["Cboolean"] = SqlBoolean.Parse("false");
            newRow2["Cbyte"] = SqlByte.Parse("21");
            newRow2["CdateTime"] = SqlDateTime.Parse("2021-01-21 21:00:00");
            newRow2["Cdecimal"] = SqlDecimal.Parse("54.321");
            newRow2["Cdouble"] = SqlDouble.Parse("87.654321");
            newRow2["Cguid"] = SqlGuid.Parse("1200eef9-b8f8-4fbc-a11c-61d417f7b42d");
            newRow2["Cint16"] = SqlInt16.Parse("14326");
            newRow2["Cint32"] = SqlInt32.Parse("1876543210");
            newRow2["Cint64"] = SqlInt64.Parse("3210987654321");
            newRow2["Cmoney"] = SqlMoney.Parse("43.21");
            newRow2["Csingle"] = SqlSingle.Parse("32.1");
            newRow2["Cstring"] = new SqlString("Second Row");
            newRow2["Cnull"] = new SqlInt16();
            dataTable.Rows.Add(newRow2);

            return dataTable;
        }

        [TestMethod]
        public void ConvertsDataTableToJson()
        {
            var table = GetDataTable();
            var json = table.ToJson();

            Assert.AreNotEqual(string.Empty, json);
        }

        [TestMethod]
        public void ConvertsDataTableToPocoOfTestPoco()
        {
            var table = GetDataTable();
            var list = table.ToList<TestPoco>();

            Assert.IsTrue(list.Any());
        }

    }

    class TestPoco
    {
        public bool Cboolean { get; set; }
        public byte Cbyte { get; set; }
        public DateTime CdateTime { get; set; }
        public decimal Cdouble { get; set; }
        public decimal Cdecimal { get; set; }
        public Guid Cguid { get; set; }
        public Int16 Cint16 { get; set; }
        public Int32 Cint32 { get; set; }
        public Int64 Cint64 { get; set; }
        public decimal Cmoney { get; set; }
        public float Csingle { get; set; }
        public string Cstring { get; set; }
        public int? Cnull { get; set; } 
    }

}
