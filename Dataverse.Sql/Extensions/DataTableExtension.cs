using System;
using Newtonsoft.Json;
using System.Data;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using Dataverse.Sql.Converters;

namespace Dataverse.Sql.Extensions
{

    public static class DataTableExtension
    {

        public static string ToJson(this DataTable dataTable)
        {
            if (dataTable == null) return string.Empty;

            ITraceWriter traceWriter = new MemoryTraceWriter();

            try
            {
                return JsonConvert.SerializeObject(dataTable,
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        TraceWriter = traceWriter,

                    }.AddSqlConverters()
                );
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public  static IList<T> ToList<T>(this DataTable dataTable)
        {
            if (dataTable == null) return new List<T>();

            var json = dataTable.ToJson();

            try
            {
                return JsonConvert.DeserializeObject<List<T>>(json);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
