using MarkMpn.Sql4Cds.Engine;
using Dataverse.Sql.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Dataverse.Sql.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dataverse.Sql
{
    public class DataverseSql : IDisposable
    {
        private bool disposedValue;

        public Sql4CdsConnection Connection { get; set; }
        public bool IsReady => Connection is { State: ConnectionState.Open };
        public ServiceClient Service { get; private set; }
        public IDictionary<string, DataSource> DataSources { get; private set; }


        public DataverseSql()
        {
        }

        public DataverseSql(string connectionString)
        {
            Connect(connectionString);
        }

        public DataverseSql(IOrganizationService service)
        {
            Connect(service);
        }

        public DataverseSql(IDictionary<string, DataSource> dataSources)
        {
            Connect(dataSources);
        }


        private void setQueryOptionsFromSettingsFile()
        {
            var settings = new ConfigurationBuilder().AddJsonFile("dataversesql.json", optional: true).Build();

            if (!string.IsNullOrEmpty(settings["useLocalTimeZone"]))
                Connection.UseLocalTimeZone = settings["useLocalTimeZone"] == "true";

            if (!string.IsNullOrEmpty(settings["blockUpdateWithoutWhere"]))
                Connection.BlockUpdateWithoutWhere = settings["blockUpdateWithoutWhere"] == "true";

            if (!string.IsNullOrEmpty(settings["blockDeleteWithoutWhere"]))
                Connection.BlockDeleteWithoutWhere = settings["blockDeleteWithoutWhere"] == "true";

            if (!string.IsNullOrEmpty(settings["useBulkDelete"]))
                Connection.UseBulkDelete = settings["blockDeleteWithoutWhere"] == "true";

            if (!string.IsNullOrEmpty(settings["batchSize"]))
                Connection.BatchSize = int.Parse(settings["batchSize"]);

            if (!string.IsNullOrEmpty(settings["useTDSEndpoint"]))
                Connection.UseTDSEndpoint = settings["useTDSEndpoint"] == "true";

            if (!string.IsNullOrEmpty(settings["maxDegreeOfParallelism"]))
                Connection.MaxDegreeOfParallelism = int.Parse(settings["maxDegreeOfParallelism"]);

            if (!string.IsNullOrEmpty(settings["bypassCustomPlugins"]))
                Connection.BypassCustomPlugins = settings["bypassCustomPlugins"] == "true";

            if (!string.IsNullOrEmpty(settings["quotedIdentifiers"]))
                Connection.QuotedIdentifiers = settings["quotedIdentifiers"] == "true";

            if (!string.IsNullOrEmpty(settings["returnEntityReferenceAsGuid"]))
                Connection.ReturnEntityReferenceAsGuid = settings["returnEntityReferenceAsGuid"] == "true";
        }


        /// <summary>
        /// Creates a ClientSecret connection string to use with the Connect() method.
        /// </summary>
        /// <param name="url">URL to your Dataverse Environment.</param>
        /// <param name="clientId">The ClientId of your App, registered with AD</param>
        /// <param name="secret">The Client Secret provided by AD</param>
        /// <returns>Connection string</returns>
        public static string GetClientSecretConnectionString(string url, string clientId, string secret)
        {
            return $"AuthType=ClientSecret; ServiceUri={url}; ClientId={clientId}; ClientSecret={secret};";
        }


        // ** When using the OAuth AuthType\AuthenticationType **
        // For development and prototyping purposes Microsoft provides the following AppId or 
        // ClientId and Redirect URI for use in OAuth Flows. For production use, you should 
        // create an AppId or ClientId that is specific to your tenant in the Azure Management 
        // portal.
        //
        //    Sample AppId or ClientId = 51f81489-12ee-4a9e-aaae-a2591f45987d
        //    Sample RedirectUri = app://58145B91-0C36-4500-8554-080854F2AC97
        //
        // !!! Microsoft.PowerPlatform.Dataverse.Client curently only supports
        // http://localhost{:[Port]} as valid RedirectUri.
        // See: https://docs.microsoft.com/en-us/powerapps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect

        /// <summary>
        /// Creates an OAuth connection string to use with the Connect() method.
        /// </summary>
        /// <param name="url">URL to your Dataverse Environment.</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password (empty if omitted)</param>
        /// <param name="clientId">If omitted the Id 51f81489-12ee-4a9e-aaae-a2591f45987d is used 
        /// which is given by Microsoft in order to use it for developing and prototyping purposes.</param>
        /// <returns></returns>
        public static string GetOAuthConnectionString(string url, string username, string password = "", string clientId = "51f81489-12ee-4a9e-aaae-a2591f45987d")
        {
            // The Password can be left blank if the User is already logged in with AD (kinda WinAuth)
            return
                $"AuthType=OAuth; Url={url}; Username={username}; Password={password}; " +
                $"ClientId={clientId}; LoginPrompt=Auto; RedirectUri=http://localhost; " +
                $@"TokenCacheStorePath=%appdata%\..\local\Temp\{AppDomain.CurrentDomain.FriendlyName}\dataverseoauthcache.txt";
        }


        /// <summary>
        /// Connects to an Environment by using the provided ConnectionString 
        /// and sets the Service property, establishes the Sql4Cds ADO Connection
        /// using this ServiceClient (if ready).
        /// </summary>
        /// <param name="connectionString">Only connection strings for AuthType OAuth 
        /// and ClientSecret are supported currently.</param>
        /// <exception cref="DataverseSqlException">DataverseSqlException is thrown when
        /// the ADO connection fails.</exception>
        public void Connect(string connectionString)
        {
            Service = new ServiceClient(connectionString);

            if (!Service.IsReady)
                throw new DataverseSqlException(Service.LastError);

            try
            {
                Connection = new Sql4CdsConnection(Service);
                setQueryOptionsFromSettingsFile();
            }
            catch (Exception ex)
            {
                throw new DataverseSqlException(ex.ToString());
            }
        }


        /// <summary>
        /// Establishes the Sql4Cds ADO Connection using an already active (connected)
        /// ServiceClient (IOrganizationService) and sets the Service property if
        /// it differs from the currently set Service.
        /// </summary>
        /// <param name="service">ServiceClient (IOrganizationService) used to
        /// establish the ADO connection.</param>
        /// <exception cref="DataverseSqlException">DataverseSqlException is thrown when
        /// the ADO connection fails.</exception>
        public void Connect(IOrganizationService service)
        {
            try
            {
                Connection = new Sql4CdsConnection(service);

                Service = Service != (ServiceClient)service
                    ? (ServiceClient)service
                    : Service;

                setQueryOptionsFromSettingsFile();
            }
            catch (Exception ex)
            {
                throw new DataverseSqlException(ex.ToString());
            }
        }


        /// <summary>
        /// Establishes the Sql4Cds ADO Connection using the given DataSources which
        /// are stored in property DataSources for further usage.
        /// </summary>
        /// <param name="dataSources">DataSources to connect to, but only the
        /// first one is used, here!</param>
        /// <exception cref="DataverseSqlException">DataverseSqlException is thrown when
        /// the ADO connection fails.</exception>
        public void Connect(IDictionary<string, DataSource> dataSources)
        {
            try
            {
                Connection = new Sql4CdsConnection(dataSources);
                DataSources = dataSources;
                setQueryOptionsFromSettingsFile();
            }
            catch (Exception ex)
            {
                throw new DataverseSqlException(ex.ToString());
            }
        }


        /// <summary>
        /// Retrieves the result of an SQL query and returns it as DataTable.
        /// </summary>
        /// <param name="sql">The SQL query</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>Query result as DataTable</returns>
        /// <exception cref="DataverseSqlException"></exception>
        public DataTable Retrieve(string sql, Dictionary<string, object> cmdParams = null)
        {
            using var cmd = Connection.CreateCommand();

            cmd.CommandText = sql;
            cmd.AddParams(cmdParams);

            try
            {
                var reader = cmd.ExecuteReader();

                var table = new DataTable();

                // https://stackoverflow.com/a/45850097
                if (!sql.ToUpper().Contains("FROM"))
                {
                    if (reader.Read())
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var col = new DataColumn(reader.GetName(i), reader.GetValue(i).GetType());
                            col.AllowDBNull = true;
                            col.Unique = false;
                            col.AutoIncrement = false;
                            table.Columns.Add(col);
                        }
                    }

                    reader = cmd.ExecuteReader();
                }

                if (reader.HasRows)
                {
                    table.Load(reader);
                }

                reader.Dispose();

                return table;
            }
            catch (Exception e)
            {
                throw new DataverseSqlException(
                    $"{e.Message}\r\n... while executing the following SQL statement:\r\n\r\n{sql}\r\n\r\n" +
                    $"Parameters: {JsonConvert.SerializeObject(cmdParams, Formatting.Indented)}", e);
            }
        }


        /// <summary>
        /// Retrieves the result of an SQL query and returns it as generic List.
        /// </summary>
        /// <param name="sql">The SQL query</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>Generic List</returns>
        /// <exception cref="DataverseSqlException"></exception>
        public IList<T> Retrieve<T>(string sql, Dictionary<string, object> cmdParams = null)
        {
            var result = Retrieve(sql, cmdParams);

            if (result.Rows.Count == 0)
                return new List<T>();

            try
            {
                return result.ToList<T>();
            }
            catch (Exception ex)
            {
                throw new DataverseSqlException(ex.ToString());
            }
        }


        /// <summary>
        /// Retrieves the result of an SQL query and returns it as directly
        /// mapped generic List.
        /// </summary>
        /// <param name="sql">The SQL query</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>Generic List</returns>
        /// <exception cref="DataverseSqlException"></exception>
        public List<T> Retrieve2<T>(string sql, Dictionary<string, object> cmdParams = null) where T : new()
        {
            var result = new List<T>();

            using var cmd = Connection.CreateCommand();

            cmd.CommandText = sql;
            cmd.AddParams(cmdParams);

            try
            {
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var t = new T();
                    var type = t.GetType();

                    for (var propIdx = 0; propIdx < reader.FieldCount; propIdx++)
                    {
                        var prop = type.GetProperty(reader.GetName(propIdx));
                        prop?.SetValue(t, Convert.ChangeType(reader.GetValue(propIdx), prop.PropertyType), null);
                    }

                    result.Add(t);
                }

                return result;
            }
            catch (Exception e)
            {
                throw new DataverseSqlException(
                    $"{e.Message}\r\n... while executing the following SQL statement:\r\n\r\n{sql}\r\n\r\n" +
                    $"Parameters: {JsonConvert.SerializeObject(cmdParams, Formatting.Indented)}", e);
            }
        }


        /// <summary>
        /// Retrieves a scalar value.
        /// </summary>
        /// <typeparam name="T">Type of result</typeparam>
        /// <param name="sql">Query statement</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <param name="addAsResult">Adds "AS Result" automatically to the sole value to be retrieved</param>
        /// <returns>Scalar value of the given type</returns>
        /// <exception cref="DataverseSqlException"></exception>
        public T RetrieveScalar<T>(string sql, Dictionary<string, object> cmdParams = null, bool addAsResult = true)
        {
            if (Regex.Match(sql, "SELECT(.*)FROM").Groups[1].ToString().Split(",").Count() > 1)
            {
                throw new DataverseSqlException(
                    "The SQL statement retrieving a scalar value contains more than one field.\r\n" +
                    $"Executing the following SQL statement:\r\n\r\n{sql}\r\n\r\n" +
                    $"Parameters: {JsonConvert.SerializeObject(cmdParams, Formatting.Indented)}");
            }

            if (addAsResult)
            {
                sql = sql.Replace("FROM", "AS Result FROM");
            }

            try
            {
                return (T)Retrieve<ScalarResult>(sql, cmdParams).FirstOrDefault()?.Result;
            }
            catch (Exception e)
            {
                throw new DataverseSqlException(
                    $"{e.Message}\r\n... while executing the following SQL statement:\r\n\r\n{sql}\r\n\r\n" +
                    $"Parameters: {JsonConvert.SerializeObject(cmdParams, Formatting.Indented)}", e);
            }
        }


        /// <summary>
        /// Retrieves the result of an SQL query and returns it as JSON string.
        /// </summary>
        /// <param name="sql">The SQL query</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>JSON</returns>
        public string RetrieveJson(string sql, Dictionary<string, object> cmdParams = null)
        {
            return Retrieve(sql, cmdParams).ToJson();
        }


        /// <summary>
        /// Retrieves the result of an SQL query from a directly mapped generic List and returns it as JSON string.
        /// </summary>
        /// <param name="sql">The SQL query</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>JSON</returns>
        public string RetrieveJson2<T>(string sql, Dictionary<string, object> cmdParams = null) where T : new()
        {
            return JsonConvert.SerializeObject(Retrieve2<T>(sql, cmdParams), Formatting.Indented);
        }


        /// <summary>
        /// Executes a DML Command
        /// </summary>
        /// <param name="sql">Thq DML command</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>Result string describing the type of executed command and shows the count of rows.</returns>
        /// <exception cref="DataverseSqlException"></exception>
        public string Execute(string sql, Dictionary<string, object> cmdParams = null)
        {
            using var cmd = Connection.CreateCommand();

            cmd.CommandText = sql;
            cmd.AddParams(cmdParams);

            try
            {
                var rowCount = cmd.ExecuteNonQuery();

                return
                    $"{rowCount} row{(rowCount != 1 ? "s" : string.Empty)} successfully {(sql.ToUpper().StartsWith("UPDATE") ? "updated" : sql.ToUpper().StartsWith("INSERT") ? "inserted" : "processed")}";
            }
            catch (Exception e)
            {
                throw new DataverseSqlException(
                    $"{e.Message}\r\n... while executing the following SQL statement:\r\n\r\n{sql}\r\n\r\n" +
                    $"Parameters: {JsonConvert.SerializeObject(cmdParams, Formatting.Indented)}", e);
            }
        }


        /// <summary>
        /// Executes an SQL script
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="cmdParams"></param>
        /// <returns>Results of script artifacts as pretty printed JSON string.</returns>
        public string ExecuteScript(string sql, Dictionary<string, object> cmdParams = null)
        {
            var jsonResults = new List<string>();

            using var cmd = Connection.CreateCommand();

            cmd.CommandText = sql;
            cmd.AddParams(cmdParams);

            using var reader = cmd.ExecuteReader();

            var resIndex = 0;
            while (!reader.IsClosed)
            {
                var table = new DataTable();
                table.Load(reader);
                jsonResults.Add($"{{\r\n\"Result{resIndex++}\":  {table.ToJson()}\r\n}}");
            }

            return JToken
                .Parse($"{{\r\n\"ScriptResults\":[{string.Join(",\r\n", jsonResults)}]\r\n}}")
                .ToString();
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
                return;

            if (disposing)
            {
                Connection.Close();
                Connection.Dispose();
            }

            disposedValue = true;
        }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
