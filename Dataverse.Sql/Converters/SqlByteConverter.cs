using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlByteConverter : SqlPrimitiveConverterBase<SqlByte>
    {
        protected override object GetValue(SqlByte sqlValue) { return sqlValue.Value; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlByte.Null;
            return (SqlByte)serializer.Deserialize<byte>(reader);
        }
    }
}
