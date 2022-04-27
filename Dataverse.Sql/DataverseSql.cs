using MarkMpn.Sql4Cds.Engine;
using Dataverse.Sql.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;

namespace Dataverse.Sql
{
    public class DataverseSql : IDisposable
    {
        private bool disposedValue;

        public Sql4CdsConnection Connection { get; set; }
        public bool IsReady => Connection is {State: ConnectionState.Open};


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
            // The Passwort can be left blank if the User is already logged in with AD (kinda WinAuth)
            return
                $"AuthType=OAuth; Url={url}; Username={username}; Password={password}; " +
                $"ClientId={clientId}; LoginPrompt=Auto; RedirectUri=http://localhost; " +
                $@"TokenCacheStorePath=%appdata%\..\local\Temp\{AppDomain.CurrentDomain.FriendlyName}\oauthcache.txt";
        }

        /// <summary>
        /// Connects to an Environment by using the provided Connectionstring, 
        /// sets the Client and initializes Metadata and TableSizes.
        /// </summary>
        /// <param name="connectionstring">Only connection strings for AuthType OAuth 
        /// and ClientSecret are supported currently.</param>
        /// <param name="requireNewInstance">Specifies whether to reuse an existing 
        /// connection if recalled while the connection is still active. If set to true, 
        /// will force the system to create a unique connection. If set to false the 
        /// existing connection can be reused. Default value is false, if omitted.</param>
        /// <exception cref="Exception">Exception is thrown when either connectoion to
        /// the Environment fails or Metadata and TableSizes cannot be retrieved.</exception>
        public void Connect(string connectionString, bool requireNewInstance = false)
        {
            try
            {
                Connection = new Sql4CdsConnection(connectionString +
                    $"; RequireNewInstance={(requireNewInstance ? "true" : "false")}");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public void Connect(IOrganizationService service)
        {
            try
            {
                Connection = new Sql4CdsConnection(service);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public void Connect(IDictionary<string, DataSource> dataSources)
        {
            try
            {
                Connection = new Sql4CdsConnection(dataSources);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        /// <summary>
        /// Retrieves the result of an SQL query and returns it as DataTable.
        /// </summary>
        /// <param name="Sql">The SQL query</param>
        /// <returns>Query result as DataTable</returns>
        /// <exception cref="Exception"></exception>
        public DataTable Retrieve(string Sql)
        {
            using var cmd = Connection.CreateCommand();

            cmd.CommandText = Sql;

            using var reader = cmd.ExecuteReader();

            var table = new DataTable();
            table.Load(reader);

            return table;
        }

        /// <summary>
        /// Retrieves the result of an SQL query and returns it as generic List.
        /// </summary>
        /// <param name="Sql">The SQL query</param>
        /// <returns>Generic List</returns>
        /// <exception cref="Exception"></exception>
        public IList<T> Retrieve<T>(string Sql)
        {
            var result = Retrieve(Sql);

            if (result.Rows.Count > 0)
            {
                try
                {
                    return result.ToList<T>();
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.ToString());
                }
            }

            return new List<T>();
        }


        /// <summary>
        /// Retrieves the result of an SQL query and returns it as directly
        /// mapped generic List.
        /// </summary>
        /// <param name="Sql">The SQL query</param>
        /// <returns>Generic List</returns>
        /// <exception cref="Exception"></exception>
        public IList<T> Retrieve2<T>(string Sql) where T : new()
        {
            var result = new List<T>();

            using var cmd = Connection.CreateCommand();

            cmd.CommandText = Sql;

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

        /// <summary>
        /// Retrieves a scalar value.
        /// </summary>
        /// <typeparam name="T">Type of result</typeparam>
        /// <param name="sql">Query statement</param>
        /// <param name="addAsResult">Adds "AS Result" automatically to the sole vale to be retrieved</param>
        /// <returns>Scalar value of the given type</returns>
        /// <exception cref="Exception"></exception>
        public T RetrieveScalar<T>(string sql, bool addAsResult = true)
        {
            if (Regex.Match(sql, "SELECT(.*)FROM").Groups[1].ToString().Split(",").Count() > 1)
            {
                throw new Exception(
                    "The SQL statement retrieving a scalar value contains more than one field.\r\n" +
                    $"Trying to execute the following SQL statement:\r\n\r\n{sql}\r\n\r\n");
            }

            if (addAsResult)
            {
                sql = sql.Replace("FROM", "AS Result FROM");
            }

            try
            {
                return (T)Retrieve<ScalarResult>(sql).FirstOrDefault()?.Result;
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"{e.Message}\r\nTrying to execute the following SQL statement:\r\n\r\n{sql}\r\n\r\n", e);
            }
        }

        /// <summary>
        /// Retrieves the result of an SQL query and returns it as JSON string.
        /// </summary>
        /// <param name="Sql">The SQL query</param>
        /// <returns>JSON</returns>
        public string RetrieveJson(string Sql)
        {
            return Retrieve(Sql).ToJson();
        }

        /// <summary>
        /// Executes a DML Command
        /// </summary>
        /// <param name="Sql">Thq DML command</param>
        /// <returns>Result string returned by the Server</returns>
        /// <exception cref="Exception"></exception>
        public int Execute(string Sql)
        {
            using var cmd = Connection.CreateCommand();
            
            cmd.CommandText = Sql;
            return cmd.ExecuteNonQuery();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Connection.Close();
                    Connection.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    internal class ScalarResult
    {
        public object Result { get; set; }
    }
}
