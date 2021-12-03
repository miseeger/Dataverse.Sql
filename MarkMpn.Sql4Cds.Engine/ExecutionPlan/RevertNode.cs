﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    class RevertNode : BaseNode, IImpersonateRevertExecutionPlanNode
    {
        private int _executionCount;
        private readonly Timer _timer = new Timer();

        public string DataSource { get; set; }

        /// <summary>
        /// The SQL string that the query was converted from
        /// </summary>
        [Browsable(false)]
        public string Sql { get; set; }

        /// <summary>
        /// The position of the SQL query within the entire query text
        /// </summary>
        [Browsable(false)]
        public int Index { get; set; }

        /// <summary>
        /// The length of the SQL query within the entire query text
        /// </summary>
        [Browsable(false)]
        public int Length { get; set; }

        public override int ExecutionCount => _executionCount;

        public override TimeSpan Duration => _timer.Duration;

        public override void AddRequiredColumns(IDictionary<string, DataSource> dataSources, IDictionary<string, Type> parameterTypes, IList<string> requiredColumns)
        {
        }

        public string Execute(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes, IDictionary<string, object> parameterValues)
        {
            _executionCount++;

            try
            {
                using (_timer.Run())
                {
                    if (!dataSources.TryGetValue(DataSource, out var dataSource))
                        throw new QueryExecutionException("Missing datasource " + DataSource);

                    if (dataSource.Connection is Microsoft.Xrm.Sdk.WebServiceClient.OrganizationWebProxyClient webProxy)
                        webProxy.CallerId = Guid.Empty;
                    else if (dataSource.Connection is ServiceClient svc)
                        svc.CallerId = Guid.Empty;
                    else
                        throw new QueryExecutionException("Unexpected organization service type") { Node = this };

                    return "Reverted impersonation";
                }
            }
            catch (QueryExecutionException ex)
            {
                if (ex.Node == null)
                    ex.Node = this;

                throw;
            }
            catch (Exception ex)
            {
                throw new QueryExecutionException(ex.Message, ex) { Node = this };
            }
        }

        public IRootExecutionPlanNode FoldQuery(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes)
        {
            return this;
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            return Array.Empty<IExecutionPlanNode>();
        }

        public override string ToString()
        {
            return "REVERT";
        }
    }
}
