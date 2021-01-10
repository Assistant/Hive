﻿using Hive.Utilities;
using NodaTime;
using Serilog;
using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Hive.Permissions
{
    /// <summary>
    /// The default rule provider for Hive, which utilizes the file system.
    /// </summary>
    public class ConfigRuleProvider : IRuleProvider
    {
        private const string RuleExtension = ".rule";

        private readonly string ruleDirectory;
        private readonly StringView splitToken;
        private readonly ILogger logger;

        // Rule name to information corresponding to the particular rule.
        private readonly ConcurrentDictionary<string, Rule<FileInfo>> cachedFileInfos = new();

        /// <summary>
        /// Construct a rule provider via DI.
        /// </summary>
        /// <param name="logger">Logger provided with DI.</param>
        /// <param name="ruleSubfolder">If not empty, the rule provider will treat this as the root rule directory.</param>
        /// <param name="splitToken">The token used to split rules.</param>
        /// <remarks>
        /// The <paramref name="splitToken"/> parameter, and the one given to <see cref="PermissionsManager{TContext}"/>, should always be the same.
        /// </remarks>
        public ConfigRuleProvider(ILogger logger, StringView splitToken, string ruleSubfolder = "Rules")
        {
            this.logger = logger;
            this.splitToken = splitToken;
            ruleDirectory = Path.Combine(Assembly.GetExecutingAssembly().Location, ruleSubfolder);

            _ = Directory.CreateDirectory(ruleSubfolder);
        }

        /// <inheritdoc/>
        public bool HasRuleChangedSince(StringView name, Instant time)
        {
            var location = GetRuleLocation(name);
            var fileInfo = new FileInfo(location);

            return IsRuleUpdatedOnFileSystem(name.ToString(), fileInfo, time);
        }

        /// <inheritdoc/>
        public bool HasRuleChangedSince(Rule rule, Instant time)
        {
            if (rule is Rule<FileInfo> fileInfoRule)
            {
                var fileInfo = fileInfoRule.Data;

                return IsRuleUpdatedOnFileSystem(rule.Name, fileInfo, time);
            }

            return false;
        }

        private bool IsRuleUpdatedOnFileSystem(string ruleName, FileInfo fileInfo, Instant time)
        {
            // Refresh access time fields for the file and its parent directory
            fileInfo.Refresh();
            fileInfo.Directory?.Refresh();

            var lastWriteTimeUtc = fileInfo.Exists
                ? fileInfo.LastWriteTimeUtc
                : fileInfo.Directory?.LastWriteTimeUtc;

            // If our rule was updated in some way, grab and cache the new data
            if (lastWriteTimeUtc is not null && Instant.FromDateTimeUtc(lastWriteTimeUtc.Value) > time)
            {
                var newRule = GetFromFileSystem(ruleName, fileInfo.FullName);

                if (newRule is not null)
                {
                    if (!cachedFileInfos.TryAdd(ruleName, newRule))
                    {
                        cachedFileInfos[ruleName] = newRule;
                    }
                }

                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool TryGetRule(StringView name, [MaybeNullWhen(false)] out Rule gotten)
        {
            var stringName = name.ToString();

            // The Permission System is asking for a new rule to cache, so we will always read from the file system.
            var rule = GetFromFileSystem(stringName, GetRuleLocation(name));

            // If we got a rule back from the method, the file exists.
            if (rule is not null)
            {
                // Add/replace cached rule
                if (!cachedFileInfos.TryAdd(stringName, rule))
                {
                    cachedFileInfos[stringName] = rule;
                }
            }

            return (gotten = rule) is not null;
        }

        // Helper function that reads information about a rule from the file system.
        private Rule<FileInfo>? GetFromFileSystem(string ruleName, string filePath)
        {
            // No default rule should exist, just return null.
            if (!File.Exists(filePath))
            {
                logger.Debug("No rule definition for \"{RuleName}\" exists on disk. You can define the rule by creating \"{RulePath}\".",
                    ruleName, filePath);
                return null;
            }

            // This cannot be made async without breaking changes to the permission manager and rule provider interface
            var ruleDefinition = File.ReadAllText(filePath);

            var fileInfo = new FileInfo(filePath);
            var rule = new Rule<FileInfo>(ruleName, ruleDefinition, fileInfo);

            return rule;
        }

        // Helper function that returns the file system location for a rule
        private string GetRuleLocation(StringView ruleName)
        {
            var parts = ruleName.Split(splitToken, ignoreEmpty: false);
            var localRuleDirectory = string.Join(@"\", parts);

            return Path.Combine(ruleDirectory, localRuleDirectory, RuleExtension);
        }
    }
}
