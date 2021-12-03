using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlBooleanConverter : SqlPrimitiveConverterBase<SqlBoolean>
    {
        protected override object GetValue(SqlBoolean sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlBoolean.Null;
            return (SqlBoolean)serializer.Deserialize<bool>(reader);
        }
    }
}
