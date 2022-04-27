using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace Dataverse.Sql.Tests.Metadata
{
    [EntityLogicalName("systemuser")]
    class SystemUser
    {
        [AttributeLogicalName("systemuserid")]
        public Guid Id { get; set; }

        [AttributeLogicalName("systemuserid")]
        public Guid SystemUserId { get; set; }

        [AttributeLogicalName("domainname")]
        public string DomainName { get; set; }
    }
}
