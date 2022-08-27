using System;
using System.Linq;

namespace Dataverse.Sql.Utils
{
    /// <summary>
    /// Evil trick 🙈 Detects if we are running inside a unit test:
    /// https://stackoverflow.com/a/29805836
    /// </summary>
    public static class UnitTestDetector
    {
        public static bool IsInUnitTest { get; private set; }

        static UnitTestDetector()
        {
            const string testAssemblyName = "Microsoft.VisualStudio.TestPlatform.TestFramework";

            UnitTestDetector.IsInUnitTest = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.FullName != null && a.FullName.StartsWith(testAssemblyName));
        }
    }
}
