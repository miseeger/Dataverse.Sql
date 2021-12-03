using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using MarkMpn.Sql4Cds.Engine;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace Dataverse.Sql
{
    public class QueryExecutionOptions: IQueryExecutionOptions
    {
        private readonly IOrganizationService _org;
        private readonly List<JoinOperator> _joinOperators;
        private int _localeId; 
        private string _primaryDataSource;
        private Guid? _userId;

        // The language code to retrieve results in (e.g.: en-us = 1033 / de-de = 1031)
        public int LocaleId
        {
            get
            {
                if (_localeId != 0)
                    return _localeId;

                var qry = new QueryExpression("usersettings");
                qry.TopCount = 1;
                qry.ColumnSet = new ColumnSet("localeid");
                qry.Criteria.AddCondition("systemuserid", ConditionOperator.EqualUserId);
                var userLink = qry.AddLink("systemuser", "systemuserid", "systemuserid");
                var orgLink = userLink.AddLink("organization", "organizationid", "organizationid");
                orgLink.EntityAlias = "org";
                orgLink.Columns = new ColumnSet("localeid");
                var locale = _org.RetrieveMultiple(qry).Entities.Single();

                if (locale.Contains("localeid"))
                    _localeId = locale.GetAttributeValue<int>("localeid");
                else
                    _localeId = (int)locale.GetAttributeValue<AliasedValue>("org.localeid").Value;

                return _localeId;
            }
        }

        // Indicates if date/time values should be interpreted in the local timezone or in UTC
        public bool UseLocalTimeZone { get; set; } = 
            DataverseSql.Config["useLocalTimeZone"] == String.Empty 
                ? true 
                : bool.Parse(DataverseSql.Config["useLocalTimeZone"]);
        
        // Indicates that the query should be cancelled
        public bool Cancelled { get; set; } =
            DataverseSql.Config["cancelled"] == String.Empty
                ? false
                : bool.Parse(DataverseSql.Config["cancelled"]);

        // Indicates if an UPDATE statement cannot be executed unless it has a WHERE clause
        public bool BlockUpdateWithoutWhere { get; set; } =
            DataverseSql.Config["blockUpdateWithoutWhere"] == String.Empty
                ? true
                : bool.Parse(DataverseSql.Config["blockUpdateWithoutWhere"]);
        
        // Indicates if a DELETE statement cannot be execyted unless it has a WHERE clause
        public bool BlockDeleteWithoutWhere { get; set; } =
            DataverseSql.Config["blockDeleteWithoutWhere"] == String.Empty
                ? false
                : bool.Parse(DataverseSql.Config["blockDeleteWithoutWhere"]);
        
        // Indicates if DELETE queries should be executed with a bulk delete job
        public bool UseBulkDelete { get; set; } =
            DataverseSql.Config["useBulkDelete"] == String.Empty
                ? false
                : bool.Parse(DataverseSql.Config["useBulkDelete"]);
    
        // The number of records that should be inserted, updated or deleted in a single batch
        public int BatchSize { get; set; } =
            DataverseSql.Config["batchSize"] == String.Empty
                ? 1
                : int.Parse(DataverseSql.Config["batchSize"]);

        // Indicates if the TDS Endpoint should be used for query execution where possible
        public bool UseTDSEndpoint { get; set; } =
            DataverseSql.Config["useTDSEndpoint"] == String.Empty
                ? false
                : bool.Parse(DataverseSql.Config["useTDSEndpoint"]);

        // Indicates if a <see cref="Microsoft.Crm.Sdk.Messages.RetrieveTotalRecordCountRequest"/>
        // should be used for simple SELECT count(*) FROM table queries
        public bool UseRetrieveTotalRecordCount { get; set; } =
            DataverseSql.Config["useRetrieveTotalRecordCount"] == String.Empty
                ? true
                : bool.Parse(DataverseSql.Config["useRetrieveTotalRecordCount"]);

        // The maximum number of worker threads to use to execute DML queries
        public int MaxDegreeOfParallelism { get; set; } =
            DataverseSql.Config["maxDegreeOfParallelism"] == String.Empty
                ? 10
                : int.Parse(DataverseSql.Config["maxDegreeOfParallelism"]);

        // Indicates if the server supports column comparison conditions in FetchXML
        public bool ColumnComparisonAvailable { get; set; } =
            DataverseSql.Config["columnComparisonAvailable"] == String.Empty
                ? true
                : bool.Parse(DataverseSql.Config["columnComparisonAvailable"]);

        // Indicates if plugins should be bypassed when executing DML operations
        public bool BypassCustomPlugins { get; set; } =
            DataverseSql.Config["bypassCustomPlugins"] == String.Empty
                ? false
                : bool.Parse(DataverseSql.Config["bypassCustomPlugins"]);

        // Returns a list of join operators that are supported by the server
        public List<JoinOperator> JoinOperatorsAvailable => _joinOperators;

        // Returns the primary datasource
        public string PrimaryDataSource => _primaryDataSource;

        // Returns the calling user's Id
        public Guid UserId {
            get
            {
                if (_userId != null)
                    return _userId.Value;

                if (_org is ServiceClient svc && svc.CallerId != Guid.Empty)
                    _userId = svc.CallerId;
                else
                    _userId = ((WhoAmIResponse)_org.Execute(new WhoAmIRequest())).UserId;

                return _userId.Value;
            }
        }

        // Checks if an INSERT query should be executed
        public Func<int, EntityMetadata, bool> OnConfirmInsert { get; set; } = (count, meta) =>
        {
            return true;
        };

        // Checks if a DELETE query should be executed
        public Func<int, EntityMetadata, bool> OnConfirmDelete { get; set; } = (count, meta) =>
        {
            return true;
        };

        //Checks if an UPDATE query should be executed
        public Func<int, EntityMetadata, bool> OnConfirmUpdate { get; set; } = (count, meta) =>
        {
            return true;
        };

        // Checks if the query should continue to retrieve more records
        public Func<int, bool> OnContinueRetrieve { get; set; } = (count) =>
        {
            return true;
        };

        // Allows the query execution to report progress
        public Action<double?, string> OnProgress { get; set; } = (progress, message) => {};

        // A notification that the query is about to retrieve another page of data
        public Action OnRetrievingNextPage { get; set; } = () => {};

        internal QueryExecutionOptions()
        {
        }

        public QueryExecutionOptions(DataSource dataSource)
        {
            _org = dataSource.Connection;
            _primaryDataSource = dataSource.Name;

            _joinOperators = new List<JoinOperator>
            {
                JoinOperator.Inner,
                JoinOperator.LeftOuter
            };

            if (new Version(((ServiceClient)dataSource.Connection).OrganizationDetail.OrganizationVersion) 
                >= new Version("9.1.0.17461"))
            {
                // First documented in SDK Version 9.0.2.25: Updated for 9.1.0.17461 CDS release
                _joinOperators.Add(JoinOperator.Any);
                _joinOperators.Add(JoinOperator.Exists);
            }
        }

        public bool ConfirmInsert(int count, EntityMetadata meta)
        {
            return OnConfirmInsert(count, meta);
        }
        
        public bool ConfirmDelete(int count, EntityMetadata meta)
        {
            return OnConfirmDelete(count, meta);
        }

        public bool ConfirmUpdate(int count, EntityMetadata meta)
        {
            return OnConfirmUpdate(count, meta);
        }

        public bool ContinueRetrieve(int count)
        {
            return OnContinueRetrieve(count);
        }

        public void Progress(double? progress, string message)
        {
            OnProgress(progress, message);
        }

        public void RetrievingNextPage()
        {
            OnRetrievingNextPage();
        }
    }
}
