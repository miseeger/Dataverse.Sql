using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlInt64Converter : SqlPrimitiveConverterBase<SqlInt64>
    {
        protected override object GetValue(SqlInt64 sqlValue) { return sqlValue.Value; }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return SqlInt64.Null;
        return (SqlInt64)serializer.Deserialize<long>(reader);
    }
}
}
