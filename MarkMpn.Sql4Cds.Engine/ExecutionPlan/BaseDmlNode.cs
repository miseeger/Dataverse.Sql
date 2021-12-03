﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using AuthenticationType = Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    /// <summary>
    /// A base class for execution plan nodes that implement a DML operation
    /// </summary>
    abstract class BaseDmlNode : BaseNode, IDmlQueryExecutionPlanNode
    {
        /// <summary>
        /// Temporarily applies global settings to improve the performance of parallel operations
        /// </summary>
        class ParallelConnectionSettings : IDisposable
        {
            private readonly int _connectionLimit;
            private readonly int _threadPoolThreads;
            private readonly int _iocpThreads;
            private readonly bool _expect100Continue;
            private readonly bool _useNagleAlgorithm;

            public ParallelConnectionSettings()
            {
                // Store the current settings
                _connectionLimit = System.Net.ServicePointManager.DefaultConnectionLimit;
                ThreadPool.GetMinThreads(out _threadPoolThreads, out _iocpThreads);
                _expect100Continue = System.Net.ServicePointManager.Expect100Continue;
                _useNagleAlgorithm = System.Net.ServicePointManager.UseNagleAlgorithm;

                // Apply the required settings
                System.Net.ServicePointManager.DefaultConnectionLimit = 65000;
                ThreadPool.SetMinThreads(100, 100);
                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.UseNagleAlgorithm = false;
            }

            public void Dispose()
            {
                // Restore the original settings
                System.Net.ServicePointManager.DefaultConnectionLimit = _connectionLimit;
                ThreadPool.SetMinThreads(_threadPoolThreads, _iocpThreads);
                System.Net.ServicePointManager.Expect100Continue = _expect100Continue;
                System.Net.ServicePointManager.UseNagleAlgorithm = _useNagleAlgorithm;
            }
        }

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

        [Browsable(false)]
        public IExecutionPlanNode Source { get; set; }

        /// <summary>
        /// The instance that this node will be executed against
        /// </summary>
        [Category("Data Source")]
        [Description("The data source this query is executed against")]
        public string DataSource { get; set; }

        /// <summary>
        /// Changes system settings to optimise for parallel connections
        /// </summary>
        /// <returns>An object to dispose of to reset the settings to their original values</returns>
        protected IDisposable UseParallelConnections() => new ParallelConnectionSettings();

        /// <summary>
        /// Executes the DML query and returns an appropriate log message
        /// </summary>
        /// <param name="org">The <see cref="IOrganizationService"/> to use to get the data</param>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExecutionOptions"/> to indicate how the query can be executed</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <param name="parameterValues">A mapping of parameter names to their current values</param>
        /// <returns>A log message to display</returns>
        public abstract string Execute(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes, IDictionary<string, object> parameterValues);

        /// <summary>
        /// Attempts to fold this node into its source to simplify the query
        /// </summary>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExecutionOptions"/> to indicate how the query can be executed</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <returns>The node that should be used in place of this node</returns>
        public virtual IRootExecutionPlanNode FoldQuery(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes)
        {
            if (Source is IDataExecutionPlanNode dataNode)
                Source = dataNode.FoldQuery(dataSources, options, parameterTypes);
            else if (Source is IDataSetExecutionPlanNode dataSetNode)
                Source = dataSetNode.FoldQuery(dataSources, options, parameterTypes);

            return this;
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            yield return Source;
        }

        /// <summary>
        /// Gets the records to perform the DML operation on
        /// </summary>
        /// <param name="org">The <see cref="IOrganizationService"/> to use to get the data</param>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExecutionOptions"/> to indicate how the query can be executed</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <param name="parameterValues">A mapping of parameter names to their current values</param>
        /// <param name="schema">The schema of the data source</param>
        /// <returns>The entities to perform the DML operation on</returns>
        protected List<Entity> GetDmlSourceEntities(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes, IDictionary<string, object> parameterValues, out NodeSchema schema)
        {
            List<Entity> entities;

            if (Source is IDataExecutionPlanNode dataSource)
            {
                schema = dataSource.GetSchema(dataSources, parameterTypes);
                entities = dataSource.Execute(dataSources, options, parameterTypes, parameterValues).ToList();
            }
            else if (Source is IDataSetExecutionPlanNode dataSetSource)
            {
                var dataTable = dataSetSource.Execute(dataSources, options, parameterTypes, parameterValues);

                // Store the values under the column index as well as name for compatibility with INSERT ... SELECT ...
                schema = new NodeSchema();

                for (var i = 0; i < dataTable.Columns.Count; i++)
                {
                    var col = dataTable.Columns[i];
                    schema.Schema[col.ColumnName] = col.DataType;
                    schema.Schema[i.ToString()] = col.DataType;
                }

                entities = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row =>
                    {
                        var entity = new Entity();

                        for (var i = 0; i < dataTable.Columns.Count; i++)
                        {
                            entity[dataTable.Columns[i].ColumnName] = row[i];
                            entity[i.ToString()] = row[i];
                        }

                        return entity;
                    })
                    .ToList();
            }
            else
            {
                throw new QueryExecutionException("Unexpected data source") { Node = this };
            }

            return entities;
        }

        /// <summary>
        /// Compiles methods to access the data required for the DML operation
        /// </summary>
        /// <param name="mappings">The mappings of attribute name to source column</param>
        /// <param name="schema">The schema of data source</param>
        /// <param name="attributes">The attributes in the target metadata</param>
        /// <param name="dateTimeKind">The time zone that datetime values are supplied in</param>
        /// <returns></returns>
        protected Dictionary<string, Func<Entity, object>> CompileColumnMappings(EntityMetadata metadata, IDictionary<string,string> mappings, NodeSchema schema, IDictionary<string, AttributeMetadata> attributes, DateTimeKind dateTimeKind)
        {
            var attributeAccessors = new Dictionary<string, Func<Entity, object>>();
            var entityParam = Expression.Parameter(typeof(Entity));

            foreach (var mapping in mappings)
            {
                var sourceColumnName = mapping.Value;
                var destAttributeName = mapping.Key;

                if (!schema.ContainsColumn(sourceColumnName, out sourceColumnName))
                    throw new QueryExecutionException($"Missing source column {mapping.Value}") { Node = this };

                // We might be using a virtual ___type attribute that has a different name in the metadata. We can safely
                // ignore these attributes - the attribute names have already been validated in the ExecutionPlanBuilder
                if (!attributes.TryGetValue(destAttributeName, out var attr) || attr.AttributeOf != null)
                    continue;

                var sourceType = schema.Schema[sourceColumnName];
                var destType = attr.GetAttributeType();
                var destSqlType = SqlTypeConverter.NetToSqlType(destType);

                var expr = (Expression)Expression.Property(entityParam, typeof(Entity).GetCustomAttribute<DefaultMemberAttribute>().MemberName, Expression.Constant(sourceColumnName));
                var originalExpr = expr;

                if (sourceType == typeof(object))
                {
                    // null literal
                    expr = Expression.Constant(null, destType);
                    expr = Expr.Box(expr);
                }
                else
                {
                    expr = SqlTypeConverter.Convert(expr, sourceType);
                    expr = SqlTypeConverter.Convert(expr, destSqlType);
                    var convertedExpr = SqlTypeConverter.Convert(expr, destType);

                    if (attr is LookupAttributeMetadata lookupAttr && lookupAttr.AttributeType != AttributeTypeCode.PartyList)
                    {
                        // Special case: intersect attributes can be simple guids
                        if (metadata.IsIntersect != true)
                        {
                            if (sourceType == typeof(SqlEntityReference))
                            {
                                expr = SqlTypeConverter.Convert(originalExpr, sourceType);
                                convertedExpr = SqlTypeConverter.Convert(expr, typeof(EntityReference));
                            }
                            else
                            {
                                Expression targetExpr;

                                if (lookupAttr.Targets.Length == 1)
                                {
                                    targetExpr = Expression.Constant(lookupAttr.Targets[0]);
                                }
                                else
                                {
                                    var sourceTargetColumnName = mappings[destAttributeName + "type"];
                                    var sourceTargetType = schema.Schema[sourceTargetColumnName];
                                    targetExpr = Expression.Property(entityParam, typeof(Entity).GetCustomAttribute<DefaultMemberAttribute>().MemberName, Expression.Constant(sourceTargetColumnName));
                                    targetExpr = SqlTypeConverter.Convert(targetExpr, sourceTargetType);
                                    targetExpr = SqlTypeConverter.Convert(targetExpr, typeof(SqlString));
                                    targetExpr = SqlTypeConverter.Convert(targetExpr, typeof(string));
                                }

                                convertedExpr = Expression.New(
                                    typeof(EntityReference).GetConstructor(new[] { typeof(string), typeof(Guid) }),
                                    targetExpr,
                                    Expression.Convert(convertedExpr, typeof(Guid))
                                );
                            }

                            destType = typeof(EntityReference);
                        }
                    }
                    else if (attr is EnumAttributeMetadata && !(attr is MultiSelectPicklistAttributeMetadata))
                    {
                        convertedExpr = Expression.New(
                            typeof(OptionSetValue).GetConstructor(new[] { typeof(int) }),
                            Expression.Convert(convertedExpr, typeof(int))
                        );
                        destType = typeof(OptionSetValue);
                    }
                    else if (attr is MoneyAttributeMetadata)
                    {
                        convertedExpr = Expression.New(
                            typeof(Money).GetConstructor(new[] { typeof(decimal) }),
                            Expression.Convert(expr, typeof(decimal))
                        );
                        destType = typeof(Money);
                    }
                    else if (attr is DateTimeAttributeMetadata)
                    {
                        convertedExpr = Expression.Convert(
                            Expr.Call(() => DateTime.SpecifyKind(Expr.Arg<DateTime>(), Expr.Arg<DateTimeKind>()),
                                expr,
                                Expression.Constant(dateTimeKind)
                            ),
                            typeof(DateTime?)
                        );
                    }

                    // Check for null on the value BEFORE converting from the SQL to BCL type to avoid e.g. SqlDateTime.Null being converted to 1900-01-01
                    expr = Expression.Condition(
                        SqlTypeConverter.NullCheck(expr),
                        Expression.Constant(null, destType),
                        convertedExpr);

                    if (expr.Type.IsValueType)
                        expr = SqlTypeConverter.Convert(expr, typeof(object));
                }

                attributeAccessors[destAttributeName] = Expression.Lambda<Func<Entity, object>>(expr, entityParam).Compile();
            }

            return attributeAccessors;
        }

        /// <summary>
        /// Provides values to include in log messages
        /// </summary>
        protected class OperationNames
        {
            /// <summary>
            /// The name of the operation to include at the start of a log message, e.g. "Updating"
            /// </summary>
            public string InProgressUppercase { get; set; }

            /// <summary>
            /// The name of the operation to include in the middle of a log message, e.g. "updating"
            /// </summary>
            public string InProgressLowercase { get; set; }

            /// <summary>
            /// The completed name of the operation to include in the middle of a log message, e.g. "updated"
            /// </summary>
            public string CompletedLowercase { get; set; }
        }

        /// <summary>
        /// Executes the DML operations required for a set of input records
        /// </summary>
        /// <param name="org">The <see cref="IOrganizationService"/> to use to get the data</param>
        /// <param name="options"><see cref="IQueryExecutionOptions"/> to indicate how the query can be executed</param>
        /// <param name="entities">The data source entities</param>
        /// <param name="meta">The metadata of the entity that will be affected</param>
        /// <param name="requestGenerator">A function to generate a DML request from a data source entity</param>
        /// <param name="operationNames">The constant strings to use in log messages</param>
        /// <returns>The final log message</returns>
        protected string ExecuteDmlOperation(IOrganizationService org, IQueryExecutionOptions options, List<Entity> entities, EntityMetadata meta, Func<Entity,OrganizationRequest> requestGenerator, OperationNames operationNames)
        {
            var inProgressCount = 0;
            var count = 0;

            var maxDop = options.MaxDegreeOfParallelism;
            var svc = org as ServiceClient;

            if (maxDop <= 1 || svc == null || svc.ActiveAuthenticationType != AuthenticationType.OAuth)
            {
                maxDop = 1;
                svc = null;
            }

            var useAffinityCookie = maxDop == 1 || entities.Count < 100;

            try
            {
                using (UseParallelConnections())
                {
                    Parallel.ForEach(entities,
                        new ParallelOptions { MaxDegreeOfParallelism = maxDop },
                        () =>
                        {
                            var service = svc?.Clone() ?? org;

                            if (!useAffinityCookie && service is ServiceClient crmService)
                                crmService.EnableAffinityCookie = false;

                            return new { Service = service, EMR = default(ExecuteMultipleRequest) };
                        },
                        (entity, loopState, index, threadLocalState) =>
                        {
                            if (options.Cancelled)
                            {
                                loopState.Stop();
                                return threadLocalState;
                            }

                            var request = requestGenerator(entity);

                            if (options.BypassCustomPlugins)
                                request.Parameters["BypassCustomPluginExecution"] = true;

                            if (options.BatchSize == 1)
                            {
                                var newCount = Interlocked.Increment(ref inProgressCount);
                                var progress = (double)newCount / entities.Count;
                                options.Progress(progress, $"{operationNames.InProgressUppercase} {newCount:N0} of {entities.Count:N0} {GetDisplayName(0, meta)} ({progress:P0})...");
                                threadLocalState.Service.Execute(request);
                                Interlocked.Increment(ref count);
                            }
                            else
                            {
                                if (threadLocalState.EMR == null)
                                {
                                    threadLocalState = new
                                    {
                                        threadLocalState.Service,
                                        EMR = new ExecuteMultipleRequest
                                        {
                                            Requests = new OrganizationRequestCollection(),
                                            Settings = new ExecuteMultipleSettings
                                            {
                                                ContinueOnError = false,
                                                ReturnResponses = false
                                            }
                                        }
                                    };
                                }

                                threadLocalState.EMR.Requests.Add(request);

                                if (threadLocalState.EMR.Requests.Count == options.BatchSize)
                                {
                                    var newCount = Interlocked.Add(ref inProgressCount, threadLocalState.EMR.Requests.Count);
                                    var progress = (double)newCount / entities.Count;
                                    options.Progress(progress, $"{operationNames.InProgressUppercase} {GetDisplayName(0, meta)} {newCount + 1 - threadLocalState.EMR.Requests.Count:N0} - {newCount:N0} of {entities.Count:N0}...");
                                    var resp = (ExecuteMultipleResponse)threadLocalState.Service.Execute(threadLocalState.EMR);

                                    if (resp.IsFaulted)
                                    {
                                        var error = resp.Responses[0];
                                        Interlocked.Add(ref count, error.RequestIndex);
                                        throw new ApplicationException($"Error {operationNames.InProgressLowercase} {GetDisplayName(0, meta)} - " + error.Fault.Message);
                                    }
                                    else
                                    {
                                        Interlocked.Add(ref count, threadLocalState.EMR.Requests.Count);
                                    }

                                    threadLocalState = new { threadLocalState.Service, EMR = default(ExecuteMultipleRequest) };
                                }
                            }

                            return threadLocalState;
                        },
                        (threadLocalState) =>
                        {
                            if (threadLocalState.EMR != null)
                            {
                                var newCount = Interlocked.Add(ref inProgressCount, threadLocalState.EMR.Requests.Count);
                                var progress = (double)newCount / entities.Count;
                                options.Progress(progress, $"{operationNames.InProgressUppercase} {GetDisplayName(0, meta)} {newCount + 1 - threadLocalState.EMR.Requests.Count:N0} - {newCount:N0} of {entities.Count:N0}...");
                                var resp = (ExecuteMultipleResponse)threadLocalState.Service.Execute(threadLocalState.EMR);

                                if (resp.IsFaulted)
                                {
                                    var error = resp.Responses[0];
                                    Interlocked.Add(ref count, error.RequestIndex);
                                    throw new ApplicationException($"Error {operationNames.InProgressLowercase} {GetDisplayName(0, meta)} - " + error.Fault.Message);
                                }
                                else
                                {
                                    Interlocked.Add(ref count, threadLocalState.EMR.Requests.Count);
                                }
                            }

                            if (threadLocalState.Service != org)
                                ((ServiceClient)threadLocalState.Service)?.Dispose();
                        });
                }
            }
            catch (Exception ex)
            {
                if (count == 0)
                    throw;

                throw new PartialSuccessException($"{count:N0} {GetDisplayName(count, meta)} {operationNames.CompletedLowercase}", ex);
            }

            return $"{count:N0} {GetDisplayName(count, meta)} {operationNames.CompletedLowercase}";
        }
    }
}
