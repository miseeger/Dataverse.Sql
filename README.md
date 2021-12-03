# Dataverse.Sql (Version 5.3.1.510)

This project is uses the code of [Mark Carrington's](https://markcarrington.dev/sql-4-cds/) sole [SQL 4 CDS](https://github.com/MarkMpn/Sql4Cds) engine which originally targets only full framework.The original engine and their tests were ported to .NET5 (Core) and keep their namespace. Dataverse.Sql uses the [Microsoft.PowerPlatform.Dataverse.Client](https://github.com/microsoft/PowerPlatform-DataverseServiceClient) library/package.

Dataverse.Sql wraps around the SQL 4 CDS engine and exposes the methods to retrieve data from your Dataverse Environment via SQL. It also provides the opportunity to set the engine options in a settings file (`dataversesql.json`) which is delivered with the package.

Dataverse.Sql will continuously be synchonized with changes in the SQL 4 CDS engine. Version numbers will be accordingly updated. The Revision number of the Dataverse.Sql version will hereby be taken from the Version number of the used Dataverse Client (currently using `0.5.10` which becomes `.510`)

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
    "cancelled": "false",
    "blockUpdateWithoutWhere": "true",
    "blockDeleteWithoutWhere": "false",
    "useBulkDelete": "false",
    "batchSize": "1",
    "useTdsEndpoint": "false",
    "useRetrieveTotalRecordCount": "true",
    "maxDegreeOfParallelism": "10",
    "columnComparisonAvailable": "true",
    "bypassCustomPlugins": "false"
}
```
It must be provided in the project's folder that uses Dataverse.Sql and "copied if newer". 

### Connecting to the Dataverse

To connect to a Dataverse Environment you just have to instantiate a `Dataverse.Sql.Environment` object and provide a valid connection string to the constructor.

```c#
using (DataverseSqlTest = new DataverseSql(DataverseSql.GetClientSecretConnectionString(
	"https://myTestEnv.crm.microsoft.com", "51f81489-12ee-4a9e-aaae-a2591f45987d", "TopSecret")))
{
	if (DataverseTestEnv.IsReady)
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
// Fetch (obsolete) -> EntityCollection
var fetchResult = DataverseSqlTest.Fetch("SELECT accountid, name FROM account");
Console.WriteLine($"Fetching Accounts: {fetchResult.Entities.Count} Entities.\r\n");

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
        ,a.name AS InstName
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

For executing DML statements you have to use the  `Execute()` by just providing your CUD command. The Result of the method is the result message returned by the server.

```c#
var execResult = DataverseSqlTest.Execute(
    "UPDATE contact SET statecode = 1, statuscode = 2 WHERE emailaddress1 IS NULL"
);
Console.WriteLine($"Update done: {execResult}\r\n");
```