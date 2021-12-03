using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlInt32Converter : SqlPrimitiveConverterBase<SqlInt32>
    {
        protected override object GetValue(SqlInt32 sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlInt32.Null;
            return (SqlInt32)serializer.Deserialize<int>(reader);
        }
    }
}
