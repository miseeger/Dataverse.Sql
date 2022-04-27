﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dataverse.Sql.Tests.Stubs;
using FakeXrmEasy;
using MarkMpn.Sql4Cds.Engine;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace Dataverse.Sql.Tests
{
    public class FakeXrmEasyTestsBase
    {
        protected readonly IOrganizationService _service;
        protected readonly XrmFakedContext _context;
        protected readonly DataSource _dataSource;
        protected readonly IOrganizationService _service2;
        protected readonly XrmFakedContext _context2;
        protected readonly DataSource _dataSource2;
        protected readonly IDictionary<string, DataSource> _dataSources;
        protected readonly IDictionary<string, DataSource> _localDataSource;

        public FakeXrmEasyTestsBase()
        {
            _context = new XrmFakedContext();
            _context.InitializeMetadata(Assembly.GetExecutingAssembly());
            _context.CallerId = new EntityReference("systemuser", Guid.NewGuid());

            _service = _context.GetOrganizationService();
            _dataSource = new DataSource { Name = "uat", Connection = _service, Metadata = new AttributeMetadataCache(_service), TableSizeCache = new StubTableSizeCache() };

            _context2 = new XrmFakedContext();
            _context2.InitializeMetadata(Assembly.GetExecutingAssembly());
            _context2.CallerId = _context.CallerId;

            _service2 = _context2.GetOrganizationService();
            _dataSource2 = new DataSource { Name = "prod", Connection = _service2, Metadata = new AttributeMetadataCache(_service2), TableSizeCache = new StubTableSizeCache() };

            _dataSources = new[] { _dataSource, _dataSource2 }.ToDictionary(ds => ds.Name);
            _localDataSource = new Dictionary<string, DataSource>
            {
                ["local"] = new DataSource { Name = "local", Connection = _service, Metadata = _dataSource.Metadata, TableSizeCache = _dataSource.TableSizeCache }
            };

            SetPrimaryIdAttributes(_context);
            SetPrimaryIdAttributes(_context2);

            SetLookupTargets(_context);
            SetLookupTargets(_context2);
        }

        private void SetPrimaryIdAttributes(XrmFakedContext context)
        {
            foreach (var entity in context.CreateMetadataQuery())
            {
                typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.PrimaryIdAttribute)).SetValue(entity, entity.LogicalName + "id");
                var attr = entity.Attributes.Single(a => a.LogicalName == entity.LogicalName + "id");
                typeof(AttributeMetadata).GetProperty(nameof(AttributeMetadata.IsPrimaryId)).SetValue(attr, true);
                attr.RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.SystemRequired);
                context.SetEntityMetadata(entity);
            }
        }

        private void SetLookupTargets(XrmFakedContext context)
        {
            foreach (var entity in context.CreateMetadataQuery())
            {
                if (entity.LogicalName == "account")
                {
                    typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.ObjectTypeCode)).SetValue(entity, 1);
                    context.SetEntityMetadata(entity);
                }

                if (entity.LogicalName != "contact")
                    continue;

                typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.ObjectTypeCode)).SetValue(entity, 2);

                var attr = (LookupAttributeMetadata) entity.Attributes.Single(a => a.LogicalName == "parentcustomerid");
                attr.Targets = new[] { "account", "contact" };

                var nameAttr = new StringAttributeMetadata { LogicalName = attr.LogicalName + "name" };
                typeof(AttributeMetadata).GetProperty(nameof(AttributeMetadata.AttributeOf)).SetValue(nameAttr, attr.LogicalName);

                var typeAttr = new EntityNameAttributeMetadata { LogicalName = attr.LogicalName + "type" };
                typeof(AttributeMetadata).GetProperty(nameof(AttributeMetadata.AttributeOf)).SetValue(typeAttr, attr.LogicalName);

                var attributes = entity.Attributes.Concat(new AttributeMetadata[] { nameAttr, typeAttr }).ToArray();
                typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.Attributes)).SetValue(entity, attributes);
                context.SetEntityMetadata(entity);
            }
        }
    }
    
}
