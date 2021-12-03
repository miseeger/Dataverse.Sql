using MarkMpn.Sql4Cds.Engine;
using MarkMpn.Sql4Cds.Engine.ExecutionPlan;
using Dataverse.Sql.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;

namespace Dataverse.Sql
{
    public class DataverseSql : IDisposable
    {
        private bool disposedValue;
        private const string primaryDataSource = "local"; 

        IDictionary<string, DataSource> DataSources { get; set; }
        public ServiceClient Client => (ServiceClient)DataSources[primaryDataSource].Connection;
        public AttributeMetadataCache Metadata => (AttributeMetadataCache)DataSources[primaryDataSource].Metadata;
        public TableSizeCache TableSizes => (TableSizeCache)DataSources[primaryDataSource].TableSizeCache;
        public QueryExecutionOptions Options { get; set; }
        public static IConfiguration Config { get; set; }
        public bool IsReady => Client != null && Client.IsReady;

        
        public DataverseSql()
        {
            DataSources = new Dictionary<string, DataSource>();
            Config = new ConfigurationBuilder().AddJsonFile("dataversesql.json", optional: false).Build();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public DataverseSql(string Connectionstring)
        {
            DataSources = new Dictionary<string, DataSource>();
            Config = new ConfigurationBuilder().AddJsonFile("dataversesql.json", optional: false).Build();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Connect(Connectionstring);
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
        public void Connect(string connectionstring, bool requireNewInstance = false)
        {
            try
            {
                var client = new ServiceClient(connectionstring +
                    $"; RequireNewInstance={(requireNewInstance ? "true" : "false")}");
                var metadata = new AttributeMetadataCache(client);

                DataSources.Clear();
                DataSources[primaryDataSource] = new DataSource
                    {
                        Connection = client,
                        Metadata = new AttributeMetadataCache(client),
                        TableSizeCache = new TableSizeCache(client, metadata),
                        Name = primaryDataSource
                };

                Options = new QueryExecutionOptions(DataSources[primaryDataSource]);
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
            var planBuilder = new ExecutionPlanBuilder(Metadata, TableSizes, Options);
            var query = planBuilder.Build(Sql)[0];

            if (query is IDataSetExecutionPlanNode selectQuery)
            {
                try
                {
                    return selectQuery.Execute(DataSources, Options, null, null);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.ToString());
                }
            }

            return new DataTable();
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
        public string Execute(string Sql)
        {
            var planBuilder = new ExecutionPlanBuilder(Metadata, TableSizes, Options);
            var query = planBuilder.Build(Sql)[0];

            if (query is IDmlQueryExecutionPlanNode dmlQuery)
            {
                try
                {
                    return dmlQuery.Execute(DataSources, Options, null, null);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.ToString());
                }
                
            }

            return $"A {query.GetType()} cannot be executed as DML Query!";
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Client.Dispose();
                }

                DataSources = null;
                Options = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
