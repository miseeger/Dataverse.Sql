using System;

namespace Dataverse.Sql.Tests.TestModels
{
    internal class TestAccount
    {
        public Guid? AccountId { get; set; }
        public string Name { get; set; }
        public int Employees { get; set; }
    }
}
