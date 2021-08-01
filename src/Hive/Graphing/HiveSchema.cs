﻿using System;
using GraphQL.Types;
using GraphQL.Utilities;
using Hive.Graphing.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Hive.Graphing
{
    /// <summary>
    ///  The <see cref="Schema"/> used for Hive.
    /// </summary>
    public class HiveSchema : Schema
    {
        /// <summary>
        /// Construct a HiveSchema and obtain the <see cref="HiveQuery"/> from the provided <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="provider"></param>
        public HiveSchema(IServiceProvider provider) : base(provider) => Query = provider.GetRequiredService<HiveQuery>();
    }
}