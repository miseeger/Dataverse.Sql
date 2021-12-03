using Newtonsoft.Json;

namespace Dataverse.Sql.Converters
{
    internal static class SqlPrimitiveConverters
    {
        internal static JsonSerializerSettings AddSqlConverters(this JsonSerializerSettings settings)
        {
            foreach (var converter in converters)
                settings.Converters.Add(converter);
            return settings;
        }

        static readonly JsonConverter[] converters = new JsonConverter[]
        {
            new SqlBinaryConverter(),
            new SqlBooleanConverter(),
            new SqlByteConverter(),
            new SqlDateTimeConverter(),
            new SqlDecimalConverter(),
            new SqlDoubleConverter(),
            new SqlGuidConverter(),
            new SqlInt16Converter(),
            new SqlInt32Converter(),
            new SqlInt64Converter(),
            new SqlMoneyConverter(),
            new SqlSingleConverter(),
            new SqlStringConverter(),
            new SqlEntityReferenceConverter(),
            
             // https://stackoverflow.com/questions/45848550/newtonsoft-json-jsonserializationexception-error-getting-value-from-value-on
            // Missing: Converters for primitives from System.Data.SqlTypes
            // that are classes not structs: SqlBytes, SqlChars, SqlXml and
            // maybe SqlFileStream
        };
    }
}
