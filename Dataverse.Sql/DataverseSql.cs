using MarkMpn.Sql4Cds.Engine;
using Dataverse.Sql.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Dataverse.Sql.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Microsoft.Crm.Sdk.Messages;

namespace Dataverse.Sql
{
    /// <summary>
    /// DataversSql Encapsulates the SQL4Cds Engine and adds some convenience to the standard ADO.NET
    /// connection interface. It implements IDisposable so it is recommended to use it with a use command.
    /// </summary>
    public class DataverseSql : IDisposable
    {
        private bool disposedValue;

        private int _localeId;
        private Guid? _userId;

        public Sql4CdsConnection Connection { get; set; }
        public bool IsReady => Connection is { State: ConnectionState.Open };

        public string[] ConnectionStrings { get; private set; }
        public string MainConnectionString => ConnectionStrings?[0];
        public IOrganizationService[] Services { get; private set; }
        public IOrganizationService MainService => Services?[0];
        public IDictionary<string, DataSource> DataSources { get; private set; }
        public DataSource MainDataSource => DataSources?.First().Value;

        public int LocaleId
        {
            get
            {
                if (_localeId != 0)
                    return _localeId;
                try
                {
                    var qry = new QueryExpression("usersettings");
                    qry.TopCount = 1;
                    qry.ColumnSet = new ColumnSet("localeid");
                    qry.Criteria.AddCondition("systemuserid", ConditionOperator.EqualUserId);
                    var userLink = qry.AddLink("systemuser", "systemuserid", "systemuserid");
                    var orgLink = userLink.AddLink("organization", "organizationid", "organizationid");
                    orgLink.EntityAlias = "org";
                    orgLink.Columns = new ColumnSet("localeid");
                    var locale = MainService.RetrieveMultiple(qry).Entities.Single();

                    _localeId = locale.Contains("localeid")
                        ? locale.GetAttributeValue<int>("localeid")
                        : (int)locale.GetAttributeValue<AliasedValue>("org.localeid").Value;

                    return _localeId;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public Guid UserId
        {
            get
            {
                if (_userId != null)
                    return _userId.Value;

                if (MainService is ServiceClient svc && svc.CallerId != Guid.Empty)
                    _userId = svc.CallerId;
                else
                    _userId = ((WhoAmIResponse)MainService.Execute(new WhoAmIRequest())).UserId;

                return _userId.Value;
            }
        }

        /// <summary>
        /// Creates a new <see cref="DataverseSql"/> wrapper using the specified XRM connection string(s)
        /// </summary>
        /// <param name="connectionStrings">The list of connection strings to use to connect to the Dataverse / Dynamics 365 instance(s)</param>
        public DataverseSql(params string[] connectionStrings): this(Connect(connectionStrings))
        {
            ConnectionStrings = connectionStrings;
        }

        /// <summary>
        /// Creates a new <see cref="DataverseSql"/> wrapper using the specified <see cref="IOrganizationService"/>(s)
        /// </summary>
        /// <param name="svc">The list of <see cref="IOrganizationService"/>s to use</param>
        public DataverseSql(params IOrganizationService[] svc) : this(BuildDataSources(svc))
        {
            Services = svc;
        }

        /// <summary>
        /// Creates a new <see cref="DataverseSql"/> wrapper using the specified list of data sources
        /// </summary>
        /// <param name="dataSources">The list of data sources to use, indexed by <see cref="DataSource.Name"/></param>
        public DataverseSql(IDictionary<string, DataSource> dataSources)
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


        private static IOrganizationService[] Connect(string[] connectionStrings)
        {
            var orgs = new List<IOrganizationService>();

            foreach (var connectionString in connectionStrings)
            {
                var org = new ServiceClient(connectionString);

                if (!org.IsReady)
                    throw new Sql4CdsException(org.LastError);

                orgs.Add(org);
            }

            return orgs.ToArray();
        }


        private static IDictionary<string, DataSource> BuildDataSources(IOrganizationService[] orgs)
        {
            var dataSources = new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);

            foreach (var org in orgs)
            {
                var ds = new DataSource(org);
                dataSources[ds.Name] = ds;
            }

            return dataSources;
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
        /// Alternatively retrieves the result of an SQL query and returns it as directly
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
                        var prop = type.GetProperty(reader.GetName(propIdx), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
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
        /// Retrieves the result of an SQL query from the alternative retrieval method.
        /// </summary>
        /// <param name="sql">The SQL query</param>
        /// <param name="cmdParams">Parameters as Key-Value-Pairs</param>
        /// <returns>JSON</returns>
        public string RetrieveJson2<T>(string sql, Dictionary<string, object> cmdParams = null) where T : new()
        {
            var result = Retrieve2<T>(sql, cmdParams);

            return JsonConvert.SerializeObject(result, Formatting.Indented);
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
