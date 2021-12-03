using MarkMpn.Sql4Cds.Engine;
using Newtonsoft.Json;
using System;
using System.Data.SqlTypes;

namespace Dataverse.Sql.Converters
{
    internal class SqlEntityReferenceConverter : SqlPrimitiveConverterBase<SqlEntityReference>
    {
        protected override object GetValue(SqlEntityReference sqlValue)
        {
            return sqlValue.Id == Guid.Empty ? String.Empty : sqlValue.Id;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return SqlGuid.Null;
            return (SqlGuid)serializer.Deserialize<Guid>(reader);
        }
    }
}
