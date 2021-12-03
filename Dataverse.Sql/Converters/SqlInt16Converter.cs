using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlInt16Converter : SqlPrimitiveConverterBase<SqlInt16>
    {
        protected override object GetValue(SqlInt16 sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlInt16.Null;
            return (SqlInt16)serializer.Deserialize<short>(reader);
        }
    }
}
