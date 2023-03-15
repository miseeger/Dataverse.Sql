# ![DataverseLogo](Assets/DataverseLogo_xs.png) Dataverse.Sql
[![Nuget](https://img.shields.io/nuget/v/Dataverse.Sql)](https://www.nuget.org/packages/Dataverse.Sql) [![lic](https://img.shields.io/badge/license-MIT-blue)](https://github.com/miseeger/Dataverse.Sql/blob/main/LICENSE)

This project is a dotNET (Core) wrapper around [Mark Carrington's](https://markcarrington.dev/sql-4-cds/) [SQL 4 Cds](https://github.com/MarkMpn/Sql4Cds) engine.

Dataverse.Sql adds convenience methods to retrieve data from your Dataverse Environment via SQL, using the `Sql4CdsConnection` with its standard ADO.NET connection interface. It also provides the opportunity to set the engine options in a settings file (`dataversesql.json`) which is delivered with the package.

This library will continuously be synchonized with the latest SQL 4 CDS engine. Version numbers will be accordingly updated and synchronized.

The SQL engine is able to convert the provided SQL query into the corresponding [FetchXML](https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/fetchxml-schema) syntax and allows the associated query to be executed, including the following types of query:

* `SELECT`
* `INSERT`
* `UPDATE`
* `DELETE`

For example:

```sql
-- Get contact details
SELECT
    c.firstname,
    c.lastname,
    a.telephone1
FROM
    contact AS c
    JOIN account AS a ON (c.parentcustomerid = a.accountid)
WHERE
    c.firstname = 'Mark'
    AND a.statecode = 0
ORDER BY
    c.createdon DESC

-- Deactivate contacts without an email address
UPDATE
    contact
SET
    statecode = 1, statuscode = 2
WHERE
    emailaddress1 IS NULL
```

> ✅ Although you are writing SQL, you are not directly running the queries against the back-end database. All data retrieval and modification is done through the supported Dataverse API. Running an UPDATE/INSERT/DELETE command against the underlying SQL database is unsafe, but the same query in SQL 4 CDS is translated to safe & supported API requests.

The engine converts all the SQL syntax that has a direct equivalent in FetchXML. It also attempts to support some more SQL features that do not have an equivalent in FetchXML, such as calculated fields, `HAVING` clauses and more.

When executing a query it will take into account specific Dataverse features to improve the performance or results compared to
simply executing the FetchXML directly, e.g.:

* Faster `SELECT count(*) FROM entity` query execution using [RetrieveTotalRecordCountRequest](https://docs.microsoft.com/dotnet/api/microsoft.crm.sdk.messages.retrievetotalrecordcountrequest)
* Automatically retrieving multiple pages of large result sets
* Work around `AggregateQueryRecordLimit` errors by retrieving all the individual records and applying the aggregation in-memory.

As well as querying data with FetchXML, Dataverse.Sql can also query metadata by translating the SQL query into a
[RetrieveMetadataChangesRequest](https://docs.microsoft.com/dotnet/api/microsoft.xrm.sdk.messages.retrievemetadatachangesrequest) or [RetrieveAllOptionSetsRequest](https://docs.microsoft.com/dotnet/api/microsoft.xrm.sdk.messages.retrievealloptionsetsrequest):

```sql
-- Find attributes without a description
SELECT
    entity.logicalname,
    attribute.logicalname
FROM
    metadata.entity
    JOIN metadata.attribute ON (entity.logicalname = attribute.entitylogicalname)
WHERE  attribute.description IS NULL
```

## Getting started with `DataverseSql` to do all the work

The `DataverseSql` class provides all the functionality needed to connect to, retrieve data from a Dataverse Environment and execute DML statements on a Dataverse Environment.

### Settings File

The file which holds the engine's options must be named `dataversesql.json` and contain the following properties:
```json
{
    "useLocalTimeZone": "true",
    "blockUpdateWithoutWhere": "true",
    "blockDeleteWithoutWhere": "true",
    "returnEntityReferenceAsGuid": "true",
    "_useBulkDelete_": "true",
    "_batchSize_": "100",
    "_useTdsEndpoint_": "true",
    "_maxDegreeOfParallelism_": "10",
    "_bypassCustomPlugins_": "false",
    "_quotedIdentifiers_": "true"
}
```
It must be provided in the project's folder that uses Dataverse.Sql and "copied if newer".

> All entries in the above given JSON that are prefixed and suffixed by an underscore are set by default with the given values so it is not needed to override if you agree with the defaults. If you disagree with the defaults then delete the underscores of the apropriate option and set your value.

Hiere are the properties available on the `Sql4CdsConnection` class that you can use to control exactly how your queries are executed. Most of them can be set from the .json-File described above. Those marked with "*" will not be set from this file.

| Property                       | Description                                                  |
| ------------------------------ | ------------------------------------------------------------ |
| BatchSize`                     | When executing DML operations, how many requests should be sent to the server at once? |
| `BypassCustomPlugins`          | When executing DML operations, should custom plugins be bypassed? |
| `UseLocalTimeZone`             | When working with date values, this property indicates whether the local or UTC time zone should be used. |
| `MaxDegreeOfParallelism`       | How many requests can be made in parallel? Currently used for DML and partitioned aggregate queries. |
| `UseTDSEndpoint`               | Indicates if the preview TDS Endpoint should be used where possible to execute SELECT queries. |
| `BlockDeleteWithoutWhere`      | Indicates if an error should be produced if running a DELETE query without a corresponding WHERE clause. |
| `BlockUpdateWithoutWhere`      | Indicates if an error should be produced if running a UPDATE query without a corresponding WHERE clause. |
| `UseBulkDelete`                | Set to `true` to use a bulk delete job instead of deleting individual records for a DELETE query. |
| `ReturnEntityReferenceAsGuid`  | Indicates if lookup values should be returned as simple `Guid` values rather than the default `SqlEntityReference` type. |
| `UseRetrieveTotalRecordCount`* | Indicates if a [RetrieveTotalRecordCountRequest](https://docs.microsoft.com/dotnet/api/microsoft.crm.sdk.messages.retrievetotalrecordcountrequest?WT.mc_id=DX-MVP-5004203) request should be used for simple `COUNT(*)` queries. This lets the query run faster but may produce out-of-date results. |
| `QuotedIdentifiers`*           | Indicates if `"` can be used to quote identifiers such as column and table names. Equivalent to `SET QUOTED_IDENTIFIERS ON`. |

### Events

There are also events that you can attach to to receive notifications while a query is executing. The `InfoMessage` and `StatementCompleted` events follow the pattern provided by the SqlClient classes for SQL Server, but add extra data specific to the underlaying `Sql4CdsConnection`.

| Event                                         | Description                                                  |
| --------------------------------------------- | ------------------------------------------------------------ |
| `PreDelete`<br />`PreInsert`<br />`PreUpdate` | These events on the connection are raised just before an INSERT/DELETE/UPDATE command is about to be executed. The event argument includes the metadata of the entity type that will be affected along with the number of rows. The event handler can prevent the operation by setting the `Cancel` property of the event argument to `true`. Cancelling the operation will also cancel the entire batch. |
| `PreRetrieve`                                 | This event on the connection is raised just before more data is about to be retrieved from the server. The event argument contains the number of rows already retrieved so far. The event handler can prevent the retrieval from continuing by setting the `Cancel` property of the event argument to `true`. Cancelling a data retrieval will not cancel the entire batch, but will cause it to operate only on partial results. |
| `Progress`                                    | This event on the connection is raised when there is some update to the internal progress of executing a query, and can be used to provide feedback to the user that their query is progressing. |
| `InfoMessage`                                 | This event on the connection is raised when there is some textual output from the query available. |
| `StatementCompleted`                          | This event on the command is raised when a statement within the current query has completed successfully. The event arguments show the number of records that were affected by the query as well as the details of the internal query plan that was executed for the statement. |

### Connecting to the Dataverse

To connect to a Dataverse Environment you just have to instantiate a `Dataverse.Sql.Environment` object and provide a valid connection string to the constructor.

```c#
using var dvSql = new DataverseSql(DataverseSql.GetClientSecretConnectionString(
    "https://myTestEnv.crm.microsoft.com", "51f81489-12ee-4a9e-aaae-a2591f45987d", "TopSecret")))
{
    if (dvSql.IsReady)
    {
        // ... your actions
    }
}
```

Please be aware that only OAuth and ClientSecret AuthTypes are provided by the current version of the Microsoft.PowerPlatform.Dataverse.Client library. To generate an appropriate connection string you can use the static `GetClientSecretConnectionString` or `GetOAuthConnectionString` methods of the `Dataverse.Sql.DataverseSql` class.

### Retrieving/fetching Data

> A tipp for creating your SQL queries before put them into program code is to firstly develop ant test them using the CDS 4 SQL plugin for XrmToolbox because the engine of Dataverse.Sql is identically with the CDS 4 SQL engine, as mentioned at the beginning. So you can be sure that your query works right away if you use it in your application. but be aware to use the according versions of the engines.

Here you'll find how to use the various methods in order to query your Environment:

```c#
// Retrieve() -> DataTable
var retrieveResult = DataverseSqlTest.Retrieve("SELECT accountid, name FROM account");
Console.WriteLine($"Retrieving Accounts: {retrieveResult.Rows.Count} Rows.\r\n");

var sql =
    @"SELECT TOP 1
        so.OrderNumber
        ,so.PriceLevelIdName
        ,so.CustomerIdName
        ,so.CustomerIdType
        ,c.Firstname
        ,c.Lastname
        ,suc.fullname AS ContactOwner
        ,sua.fullname AS AccountOwner
    FROM
        salesorder so
        LEFT JOIN contact c ON (c.contactid = so.customerid)
        LEFT JOIN account a ON (a.accountid = so.customerid)
        LEFT JOIN systemuser suc ON (suc.systemuserid = c.ownerid)
        LEFT JOIN systemuser sua ON (sua.systemuserid = a.ownerid)";

// RetrieveJson() -> JSON String
var jsonResult = DataverseSqlTest.RetrieveJson(sql);
Console.WriteLine($"Retrieving JSON:\r\n{jsonResult}\r\n");

// Retrieve<T> -> List<T>
var salesorderResult = DataverseSqlTest.Retrieve<Salesorder>(sql);
Console.WriteLine($"Retrieving Salesorders: {salesorderResult.Count} Order retrieved.\r\n");
```

### Executing DML Statements

For executing DML statements you have to use the  `Execute()` by just providing your CUD command. The Result of the method is a result message.

```c#
var execResult = DataverseSqlTest.Execute(
    "UPDATE contact SET statecode = 1, statuscode = 2 WHERE emailaddress1 IS NULL"
);
Console.WriteLine($"Update done: {execResult}\r\n");
```

### Using Sql4CdsConnection from DataverseSql

Even if the `Sql4CdsConnection` Instance is encapsulated in `DataverseSql` it is still possible to get direct access to it. It is available by accessing the ´Connection´ property:

```c#
using var dvSql = new DataverseSql(DataverseSql.GetClientSecretConnectionString(
    "https://myTestEnv.crm.microsoft.com", "51f81489-12ee-4a9e-aaae-a2591f45987d", "TopSecret")))

using var cmd = dvSql.Connection.CreateCommand();

cmd.CommandText = "SELECT DISTINCT lastname, firstname FROM contact ORDER BY 1,2 WHERE lastname LIKE @param1";
cmd.AddParams(new Dictionary<string, object> { {"@param1", "See%"} });

using var reader = cmd.ExecuteReader();

var table = new DataTable();
table.Load(reader);

var jsonResult = table.ToJson()
```

 
