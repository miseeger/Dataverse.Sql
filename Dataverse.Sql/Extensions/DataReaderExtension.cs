using System;
using System.Data.SqlClient;
using System.Linq;


namespace Dataverse.Sql.Extensions
{

    public static class DataReaderExtension
    {

        // https://stackoverflow.com/questions/41040189/fastest-way-to-map-result-of-sqldatareader-to-object

        /// <summary>
        /// Maps a SqlDataReader record to an object. Ignoring case.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataReader"></param>
        /// <param name="newObject"></param>
        /// <remarks>https://stackoverflow.com/a/52918088</remarks>
        public static void MapDataToObject<T>(this SqlDataReader dataReader, T newObject)
        {
            if (newObject == null) throw new ArgumentNullException(nameof(newObject));

            // Fast Member Usage
            var objectMemberAccessor = FastMember.TypeAccessor.Create(newObject.GetType());
            var propertiesHashSet =
                objectMemberAccessor
                    .GetMembers()
                    .Select(mp => mp.Name)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            for (int i = 0; i < dataReader.FieldCount; i++)
            {
                var name = propertiesHashSet.FirstOrDefault(a =>
                    a.Equals(dataReader.GetName(i), StringComparison.InvariantCultureIgnoreCase));
                if (!String.IsNullOrEmpty(name))
                {
                    objectMemberAccessor[newObject, name]
                        = dataReader.IsDBNull(i) ? null : dataReader.GetValue(i);
                }
            }
        }

        public static T ConvertToObject<T>(this SqlDataReader rd) where T : class, new()
        {
            Type type = typeof(T);
            var accessor = FastMember.TypeAccessor.Create(type);
            var members = accessor.GetMembers();
            var t = new T();

            for (int i = 0; i < rd.FieldCount; i++)
            {
                if (!rd.IsDBNull(i))
                {
                    string fieldName = rd.GetName(i);

                    if (members.Any(m => string.Equals(m.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
                    {
                        accessor[t, fieldName] = rd.GetValue(i);
                    }
                }
            }

            return t;
        }

        // Anwendungsbeispiel
        //public async Task<List<T>> ExecuteReaderAsync<T>(string storedProcedureName, SqlParameter[] sqlParameters = null) where T : class, new()
        //{
        //    var newListObject = new List<T>();
        //    using (var conn = new SqlConnection(_connectionString))
        //    {
        //        using (SqlCommand sqlCommand = GetSqlCommand(conn, storedProcedureName, sqlParameters))
        //        {
        //            await conn.OpenAsync();
        //            using (var dataReader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default))
        //            {
        //                if (dataReader.HasRows)
        //                {
        //                    while (await dataReader.ReadAsync())
        //                    {
        //                        var newObject = new T();
        //                        dataReader.MapDataToObject(newObject);
        //                        newListObject.Add(newObject);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    return newListObject;
        //}
    }
}
