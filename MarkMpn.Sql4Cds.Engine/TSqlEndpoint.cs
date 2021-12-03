using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace MarkMpn.Sql4Cds.Engine
{
    public static class TSqlEndpoint
    {
        private static IDictionary<string, bool> _cache = new Dictionary<string, bool>();

        public static bool IsEnabled(ServiceClient svc)
        {
            if (!svc.ConnectedOrgUriActual.Host.EndsWith(".dynamics.com"))
                return false;

            if (_cache.TryGetValue(svc.ConnectedOrgUriActual.Host, out var enabled))
                return enabled;

            var qry = new QueryExpression("organization");
            qry.ColumnSet = new ColumnSet("orgdborgsettings");
            var orgSettings = svc.RetrieveMultiple(qry).Entities.Single().GetAttributeValue<string>("orgdborgsettings");

            if (String.IsNullOrEmpty(orgSettings))
            {
                enabled = false;
            }
            else
            {
                var xml = new XmlDocument();
                xml.LoadXml(orgSettings);

                var enabledNode = xml.SelectSingleNode("//EnableTDSEndpoint/text()");

                enabled = enabledNode?.Value == "true";
            }

            _cache[svc.ConnectedOrgUriActual.Host] = enabled;

            return enabled;
        }

        public static void Enable(ServiceClient svc)
        {
            var qry = new QueryExpression("organization");
            qry.ColumnSet = new ColumnSet("orgdborgsettings");
            var org = svc.RetrieveMultiple(qry).Entities.Single();

            var xml = new XmlDocument();
            xml.LoadXml(org.GetAttributeValue<string>("orgdborgsettings"));

            var enabled = xml.SelectSingleNode("//EnableTDSEndpoint/text()");
            var updateRequired = false;

            if (enabled == null)
            {
                enabled = xml.CreateElement("EnableTDSEndpoint");
                enabled.InnerText = "true";
                xml.SelectSingleNode("/OrgSettings").AppendChild(enabled);
                updateRequired = true;
            }
            else if (enabled.InnerText != "true")
            {
                enabled.InnerText = "true";
                updateRequired = true;
            }

            if (updateRequired)
            {
                org["orgdborgsettings"] = xml.OuterXml;
                svc.Update(org);
            }

            _cache[svc.ConnectedOrgUriActual.Host] = true;
        }

        public static void Disable(ServiceClient svc)
        {
            var qry = new QueryExpression("organization");
            qry.ColumnSet = new ColumnSet("orgdborgsettings");
            var org = svc.RetrieveMultiple(qry).Entities.Single();

            var xml = new XmlDocument();
            xml.LoadXml(org.GetAttributeValue<string>("orgdborgsettings"));

            var enabled = xml.SelectSingleNode("//EnableTDSEndpoint/text()");
            var updateRequired = false;

            if (enabled?.InnerText == "true")
            {
                enabled.InnerText = "false";
                updateRequired = true;
            }

            if (updateRequired)
            {
                org["orgdborgsettings"] = xml.OuterXml;
                svc.Update(org);
            }

            _cache[svc.ConnectedOrgUriActual.Host] = true;
        }
    }
}
