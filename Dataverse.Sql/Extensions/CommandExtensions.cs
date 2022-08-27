using System.Collections.Generic;
using MarkMpn.Sql4Cds.Engine;
using System.Data.Common;

namespace Dataverse.Sql.Extensions
{

    public static class CommandExtensions
    {

        public static void AddParameterWithValue(this DbCommand cmd, string param, object value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = param;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }


        public static void AddParams(this Sql4CdsCommand cmd, Dictionary<string, object> cmdParams)
        {
            if (cmdParams == null)
                return;

            foreach (var param in cmdParams)
            {
                cmd.AddParameterWithValue(param.Key, param.Value);
            }
        }

    }

}
