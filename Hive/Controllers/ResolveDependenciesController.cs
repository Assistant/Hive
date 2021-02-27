﻿using Hive.Models;
using Hive.Services.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Hive.Controllers
{
    /// <summary>
    /// A REST controller for resolving dependencies.
    /// </summary>
    [Route("api/resolve_dependencies")]
    public class ResolveDependenciesController : ControllerBase
    {
        private readonly Serilog.ILogger log;
        private readonly DependencyResolverService dependencyResolverService;

        /// <summary>
        /// Create a ResolveDependenciesController with DI.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="dependencyResolverService"></param>
        public ResolveDependenciesController([DisallowNull] Serilog.ILogger logger, DependencyResolverService dependencyResolverService)
        {
            if (logger is null) throw new ArgumentNullException(nameof(logger));
            log = logger.ForContext<ResolveDependenciesController>();
            this.dependencyResolverService = dependencyResolverService;
        }

        /// <summary>
        /// Resolves the dependencies for a list of <see cref="ModIdentifier"/> objects.
        /// This performs a permission check at: <c>hive.resolve_dependencies</c>.
        /// </summary>
        /// <param name="identifiers">The identifiers to resolve dependencies for.</param>
        /// <returns>A wrapped <see cref="DependencyResolutionResult"/>, if successful.</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status424FailedDependency)]
        public async Task<ActionResult<DependencyResolutionResult>> ResolveDependencies([FromBody] ModIdentifier[] identifiers)
        {
            log.Debug("Performing dependency resolution...");

            var queryResult = await dependencyResolverService.ResolveAsync(User.Identity as User, identifiers).ConfigureAwait(false);

            return queryResult.Convert();
        }
    }
}
