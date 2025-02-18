﻿using Examine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Our.Umbraco.FullTextSearch.Interfaces;
using Our.Umbraco.FullTextSearch.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core;
using nots = Umbraco.Cms.Core.Notifications;
using umb = Umbraco;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;

namespace Our.Umbraco.FullTextSearch.NotificationHandlers
{
    public class AddFullTextItemsToIndex : INotificationHandler<UmbracoApplicationStartingNotification>
    {
        private readonly IExamineManager _examineManager;
        private readonly FullTextSearchOptions _options;
        private readonly ILogger<AddFullTextItemsToIndex> _logger;
        private readonly IProfilingLogger _profilingLogger;
        private readonly ICacheService _cacheService;

        public AddFullTextItemsToIndex(IExamineManager examineManager,
            IOptions<FullTextSearchOptions> options,
            ILogger<AddFullTextItemsToIndex> logger,
            IProfilingLogger profilingLogger,
            ICacheService cacheService)
        {
            _examineManager = examineManager;
            _options = options.Value;
            _logger = logger;
            _profilingLogger = profilingLogger;
            _cacheService = cacheService;
        }

        public void Handle(UmbracoApplicationStartingNotification notification)
        {

            if (!_examineManager.TryGetIndex(Constants.UmbracoIndexes.ExternalIndexName, out IIndex index))
            {
                _logger.LogError(new InvalidOperationException($"{Constants.UmbracoIndexes.ExternalIndexName} not found"), $"{Constants.UmbracoIndexes.ExternalIndexName} not found");
                return;
            }

            //we need to cast because BaseIndexProvider contains the TransformingIndexValues event
            if (index is not BaseIndexProvider indexProvider)
            {
                _logger.LogError(new InvalidOperationException($"Could not cast {Constants.UmbracoIndexes.ExternalIndexName} to BaseIndexProvider"), $"Could not cast {Constants.UmbracoIndexes.ExternalIndexName} to BaseIndexProvider");
                return;
            }
            indexProvider.TransformingIndexValues += IndexProviderTransformingIndexValues;
        }

        private void IndexProviderTransformingIndexValues(object sender, IndexingItemEventArgs e)
        {
            if (e.Index.Name != Constants.UmbracoIndexes.ExternalIndexName) return;
            if (e.ValueSet.Category != IndexTypes.Content) return;
            if (!_options.Enabled)
            {
                _logger.LogDebug("FullTextIndexing is not enabled");
                return;
            }

            // check if contentType is allowed
            if (_options.DisallowedContentTypeAliases.InvariantContains(e.ValueSet.ItemType))
            {
                _logger.LogDebug(
                    "{nodeTypeAlias} is disallowed by DisallowedContentTypeAliases - {disallowedContentTypeAliases}",
                    e.ValueSet.ItemType,
                    string.Join(",", _options.DisallowedContentTypeAliases)
                    );
                return;
            }

            if (_options.DisallowedPropertyAliases.Any())
            {
                foreach (var disallowedPropertyAlias in _options.DisallowedPropertyAliases)
                {
                    var value = e.ValueSet.GetValue(disallowedPropertyAlias);
                    if (value != null && value.ToString() == "1")
                    {
                        return;
                    }
                }
            }

            // check if there is a template
            var templateId = e.ValueSet.GetValue("templateID");
            if (templateId == null || templateId.ToString() == "0")
            {
                _logger.LogDebug("Template Id is 0 or null");
                return;
            }

            // todo path stuff from callum

            // set path value
            var currentPath = e.ValueSet.GetValue("path");
            if (currentPath != null)
            {
                var pathFieldName = _options.FullTextPathField;
                e.ValueSet.TryAdd(pathFieldName, currentPath.ToString().Replace(",", " "));
            }

            // convert id to int, so we can get it from the content cache
            if (!int.TryParse(e.ValueSet.Id, out int id)) return;

            using (_profilingLogger.DebugDuration<AddFullTextItemsToIndex>("Attempt to fulltext index for node " + id, "Completed fulltext index for node " + id))
            {
                var cacheItems = _cacheService.GetFromCache(id);
                if (cacheItems == null || !cacheItems.Any()) return;

                foreach (var item in cacheItems)
                {
                    var fieldName = _options.FullTextContentField;
                    if (item.Culture != "") fieldName += "_" + item.Culture;

                    e.ValueSet.TryAdd(fieldName, item.Text);
                }
            }
        }
    }
}
