using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlDoubleConverter : SqlPrimitiveConverterBase<SqlDouble>
    {
        protected override object GetValue(SqlDouble sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlDouble.Null;
            return (SqlDouble)serializer.Deserialize<double>(reader);
        }
    }
}
