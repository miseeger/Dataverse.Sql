using System.Data.Common;
using System;
using MarkMpn.Sql4Cds.Engine;

namespace Dataverse.Sql
{
    public class DataverseSqlException : Sql4CdsException
    {
        public DataverseSqlException(string message) : base(message)
        {
        }

        public DataverseSqlException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
