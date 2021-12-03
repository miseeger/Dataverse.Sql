using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlBinaryConverter : SqlPrimitiveConverterBase<SqlBinary>
    {
        protected override object GetValue(SqlBinary sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlBinary.Null;
            return (SqlBinary)serializer.Deserialize<byte[]>(reader);
        }
    }
}
