using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlDecimalConverter : SqlPrimitiveConverterBase<SqlDecimal>
    {
        protected override object GetValue(SqlDecimal sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlDecimal.Null;
            return (SqlDecimal)serializer.Deserialize<decimal>(reader);
        }
    }
}
