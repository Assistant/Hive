﻿using Hive.Models;
using Hive.Plugins;
using Hive.Controllers;
using Hive.Permissions;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace Hive.Services.Common
{
    /// <summary>
    /// Common functionality for game version related actions.
    /// </summary>
    public class GameVersionService
    {
        private readonly Serilog.ILogger log;
        private readonly HiveContext context;
        private readonly IAggregate<IGameVersionsPlugin> plugin;
        private readonly PermissionsManager<PermissionContext> permissions;
        [ThreadStatic] private static PermissionActionParseState versionsParseState;

        private const string ActionName = "hive.game.version";
        private static readonly HiveObjectQuery<IEnumerable<GameVersion>> forbiddenResponse = new(null, "Forbidden", StatusCodes.Status403Forbidden);

        /// <summary>
        /// Create a GameVersionService with DI.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="perms"></param>
        /// <param name="ctx"></param>
        /// <param name="plugin"></param>
        public GameVersionService([DisallowNull] Serilog.ILogger logger, PermissionsManager<PermissionContext> perms, HiveContext ctx, IAggregate<IGameVersionsPlugin> plugin)
        {
            if (logger is null) throw new ArgumentNullException(nameof(logger));
            log = logger.ForContext<GameVersionService>();
            context = ctx;
            permissions = perms;
            this.plugin = plugin;
        }

        /// <summary>
        /// Gets all available <see cref="GameVersion"/> objects.
        /// This performs a permission check at: <c>hive.game.version</c>.
        /// </summary>
        /// <param name="user">The user to associate with this request.</param>
        /// <returns>A wrapped enumerable of <see cref="GameVersion"/> objects, if successful.</returns>
        public HiveObjectQuery<IEnumerable<GameVersion>> RetrieveAllVersions(User? user)
        {
            if (!permissions.CanDo(ActionName, new PermissionContext { User = user }, ref versionsParseState))
                return forbiddenResponse;

            // Combine plugins
            log.Debug("Combining plugins...");
            var combined = plugin.Instance;
            log.Debug("Perform additional checks for GetGameVersions...");
            // If the plugins say the user cannot access the list of game versions, then we forbid.
            if (!combined.GetGameVersionsAdditionalChecks(user))
                return forbiddenResponse;

            // Grab our list of game versions
            var versions = context.GameVersions.ToList();
            log.Debug("Filtering versions from all {0} versions...", versions.Count);
            // First, we perform a permission check on each game version, in case we need to filter any specific ones
            // (Use additionalData to flag beta game versions, perhaps? Could be a plugin.)
            var filteredVersions = versions.Where(v => permissions.CanDo(ActionName, new PermissionContext { GameVersion = v, User = user }, ref versionsParseState));
            log.Debug("Remaining versions after permissions check: {0}", filteredVersions.Count());
            // Then we filter this even further by passing it through all of our Hive plugins.
            filteredVersions = combined.GetGameVersionsFilter(user, filteredVersions);
            // This final filtered list of versions is what we'll return back to the user.
            log.Debug("Remaining versions after plugin filters: {0}", filteredVersions.Count());

            return new HiveObjectQuery<IEnumerable<GameVersion>>(filteredVersions, null, StatusCodes.Status200OK);
        }
    }
}