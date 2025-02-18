﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace Our.Umbraco.FullTextSearch.Migrations
{
    public class ExecuteMigrations : INotificationHandler<UmbracoApplicationStartingNotification>
    {
        private readonly IScopeProvider scopeProvider;
        private readonly IMigrationPlanExecutor migrationPlanExecutor;
        private readonly IKeyValueService keyValueService;

        public ExecuteMigrations(
            IScopeProvider scopeProvider,
            IMigrationPlanExecutor migrationPlanExecutor,
            IKeyValueService keyValueService)
        {
            this.scopeProvider = scopeProvider;
            this.migrationPlanExecutor = migrationPlanExecutor;
            this.keyValueService = keyValueService;
        }

        public void Handle(UmbracoApplicationStartingNotification notification)
        {
            if (notification.RuntimeLevel >= RuntimeLevel.Run)
            {
                // register and run our migration plan
                var upgrader = new Upgrader(new FullTextSearchMigrationPlan());
                upgrader.Execute(migrationPlanExecutor, scopeProvider, keyValueService);
            }
        }
    }
}
