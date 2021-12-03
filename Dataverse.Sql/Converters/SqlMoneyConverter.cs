using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlMoneyConverter : SqlPrimitiveConverterBase<SqlMoney>
    {
        protected override object GetValue(SqlMoney sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlMoney.Null;
            return (SqlMoney)serializer.Deserialize<decimal>(reader);
        }
    }
}
