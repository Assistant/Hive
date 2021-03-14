﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hive.Plugins.Loading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hive.Plugins.Tests
{
    public class TestPluginLoading
    {
        private static IHostBuilder BuildLoadingHost(string cfgKey, Dictionary<string, string> cfgValues, Action<PluginLoaderOptionsBuilder>? optBuilder = null)
        {
            optBuilder ??= _ => { };
            return new HostBuilder()
                .ConfigureAppConfiguration(cfg
                    => cfg.AddInMemoryCollection(cfgValues))
                .UseDefaultServiceProvider((ctx, spo) => { })
                .UseWebHostPlugins(builder
                    => optBuilder(builder
                        .WithConfigurationKey(cfgKey)
                        .WithApplicationConfigureRegistrar((services, target, method)
                            => services.AddSingleton(new PluginRegistration(target, method)))));
        }

        private record PluginRegistration(object Target, MethodInfo Method)
        {
            public void Invoke(IServiceProvider services)
                => services.InjectVoidMethod(Method, null)(Target);
        }

        private static void InitRegistrations(IServiceProvider services, IEnumerable<PluginRegistration> registrations)
        {
            // initialize the registrations
            foreach (var registr in registrations)
            {
                registr.Invoke(services);
            }
        }

        [Fact]
        public void TestImplicitLoadPlugin()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "true" }
            })
                .Build();

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // we only have one test plugin that we put in the dir
            Assert.Single(pluginInstances);
            // that test plugin has only one startup class
            Assert.Single(pluginRegistrations);

            var firstRegistration = pluginRegistrations.First();
            // the registration target is itself registered
            Assert.Same(firstRegistration, host.Services.GetRequiredService(firstRegistration.GetType()));
        }

        [Fact]
        public void TestImplicitDenyLoadPlugin()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "true" },
                { "Plugins:ExcludePlugins:0", "Hive.Plugins.Tests.TestPlugin" }
            })
                .Build();

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // we exclude the only plugin that is present
            Assert.Empty(pluginInstances);
            Assert.Empty(pluginRegistrations);
        }

        [Fact]
        public void TestExplicitLoadPlugin()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "false" },
                { "Plugins:LoadPlugins:0", "Hive.Plugins.Tests.TestPlugin" }
            })
                .Build();

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // we only have one test plugin that we are loading
            Assert.Single(pluginInstances);
            // that test plugin has only one startup class
            Assert.Single(pluginRegistrations);

            var firstRegistration = pluginRegistrations.First();
            // the registration target is itself registered
            Assert.Same(firstRegistration, host.Services.GetRequiredService(firstRegistration.GetType()));
        }

        [Fact]
        public void TestExplicitLoadExcludePlugin()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "false" },
                { "Plugins:LoadPlugins:0", "Hive.Plugins.Tests.TestPlugin" },
                { "Plugins:ExcludePlugins:0", "Hive.Plugins.Tests.TestPlugin" }
            })
                .Build();

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // the only plugin which is explicitly loaded is also excluded
            Assert.Empty(pluginInstances);
            Assert.Empty(pluginRegistrations);
        }

        [Fact]
        public void TestExplicitNoLoadPlugin()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "false" },
            })
                .Build();

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // no plugins were loaded
            Assert.Empty(pluginInstances);
            Assert.Empty(pluginRegistrations);
        }

        [Fact]
        public void TestPluginSharedConfigurations()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "false" },
                { "Plugins:LoadPlugins:0", "Hive.Plugins.Tests.TestPlugin" },
                { "Plugins:UsePluginSpecificConfig", "false" },
                // actual config values
                { "Plugins:PluginConfigurations:Hive.Plugins.Tests.TestPlugin:val1", "hello" },
                { "Plugins:PluginConfigurations:Hive.Plugins.Tests.TestPlugin:val2", "17" },
            })
                .ConfigureServices(sc => sc.AddSingleton<Action<IConfiguration>>(OnGetConfiguration))
                .Build();

            static void OnGetConfiguration(IConfiguration config)
            {
                Assert.Equal("hello", config.GetValue<string>("val1"));
                Assert.Equal(17, config.GetValue<int>("val2"));
            }

            // TODO: is this repetition bad enough to warrant helpers?

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // we only have one test plugin that we are loading
            Assert.Single(pluginInstances);
            // that test plugin has only one startup class
            Assert.Single(pluginRegistrations);

            var firstRegistration = pluginRegistrations.First();
            // the registration target is itself registered
            Assert.Same(firstRegistration, host.Services.GetRequiredService(firstRegistration.GetType()));
        }

        [Fact]
        public void TestPluginSpecificConfigurations()
        {
            var host = BuildLoadingHost("Plugins", new()
            {
                { "Plugins:PluginPath", "plugins" },
                { "Plugins:ImplicitlyLoadPlugins", "false" },
                { "Plugins:LoadPlugins:0", "Hive.Plugins.Tests.TestPlugin" },
                { "Plugins:UsePluginSpecificConfig", "true" },
            }, b => b.ConfigurePluginConfig((c, i)
                => c.AddInMemoryCollection(new Dictionary<string, string>()
                {
                    // actual config values
                    { "val1", "hello" },
                    { "val2", "17" },
                })))
                .ConfigureServices(sc => sc.AddSingleton<Action<IConfiguration>>(OnGetConfiguration))
                .Build();

            static void OnGetConfiguration(IConfiguration config)
            {
                Assert.Equal("hello", config.GetValue<string>("val1"));
                Assert.Equal(17, config.GetValue<int>("val2"));
            }

            // TODO: is this repetition bad enough to warrant helpers?

            var pluginInstances = host.Services.GetRequiredService<IEnumerable<PluginInstance>>();
            var pluginRegistrations = host.Services.GetRequiredService<IEnumerable<PluginRegistration>>();

            InitRegistrations(host.Services, pluginRegistrations);

            // we only have one test plugin that we are loading
            Assert.Single(pluginInstances);
            // that test plugin has only one startup class
            Assert.Single(pluginRegistrations);

            var firstRegistration = pluginRegistrations.First();
            // the registration target is itself registered
            Assert.Same(firstRegistration, host.Services.GetRequiredService(firstRegistration.GetType()));
        }

        // TODO: test that exceptions are propagated correctly
    }
}
