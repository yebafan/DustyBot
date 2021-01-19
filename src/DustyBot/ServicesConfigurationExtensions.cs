﻿using Discord;
using Discord.WebSocket;
using DustyBot.Configuration;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Database.Services.Configuration;
using DustyBot.Database.Sql;
using DustyBot.Definitions;
using DustyBot.Framework;
using DustyBot.Framework.Configuration;
using DustyBot.Helpers;
using DustyBot.Modules;
using DustyBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;

namespace DustyBot
{
    internal static class ServicesConfigurationExtensions
    {
        public static void AddBotServices(this IServiceCollection services, IConfiguration config)
        {
            // Configuration
            services.Configure<BotOptions>(config);
            services.Configure<DatabaseOptions>(config);
            services.Configure<DiscordOptions>(config);
            services.Configure<IntegrationOptions>(config);
            services.Configure<LoggingOptions>(config);
            services.AddScoped<IFrameworkGuildConfigProvider, FrameworkGuildConfigProvider>();

            // Discord
            services.AddDiscordClient();
            services.AddTransient<DiscordClientLauncher>();

            // Database
            services.AddSingleton<ISettingsService, MongoSettingsService>();
            services.AddScoped<IProxyListService, ProxyListService>();
            services.AddScoped<ISpotifyAccountsService, SpotifyAccountsService>();
            services.AddScoped(x => DustyBotDbContext.Create(x.GetRequiredService<IOptions<DatabaseOptions>>().Value.SqlDbConnectionString));
            services.AddScoped<Func<ILastFmStatsService>>(x => () => ActivatorUtilities.CreateInstance<LastFmStatsService>(x));

            // Services
            services.AddHostedService<StatusService>();
            services.AddHostedService<DaumCafeService>();
            services.AddHostedApiService<IScheduleService, ScheduleService>();
            services.AddHostedApiService<IProxyService, RotatingProxyService>();

            // Modules
            services.AddScoped<AdministrationModule>();
            services.AddSingleton<AutorolesModule>();
            services.AddSingleton<BotModule>();
            services.AddScoped<CafeModule>();
            services.AddSingleton<EventsModule>();
            services.AddScoped<InfoModule>();
            services.AddSingleton<InstagramModule>();
            services.AddScoped<LastFmModule>();
            services.AddSingleton<LogModule>();
            services.AddSingleton<NotificationsModule>();
            services.AddScoped<PollModule>();
            services.AddSingleton<RaidProtectionModule>();
            services.AddSingleton<ReactionsModule>();
            services.AddSingleton<RolesModule>();
            services.AddScoped<ScheduleModule>();
            services.AddScoped<SpotifyModule>();
            services.AddSingleton<StarboardModule>();
            services.AddScoped<TranslatorModule>();
            services.AddScoped<ViewsModule>();

            // Framework
            services.AddFrameworkServices((provider, builder) => 
            {
                var options = provider.GetRequiredService<IOptions<BotOptions>>();
                builder.WithDiscordClient(provider.GetRequiredService<BaseSocketClient>())
                    .WithDefaultPrefix(options.Value.DefaultCommandPrefix)
                    .ConfigureLogging(x => x.AddSerilog(provider.GetRequiredService<ILogger>()))
                    .WithGuildConfigProvider(provider.GetRequiredService<IFrameworkGuildConfigProvider>())
                    .AddOwner(options.Value.OwnerID)
                    .AddModulesFromServices(services);
            });

            // Miscellaneous
            services.AddScoped<IUrlShortener, PolrUrlShortener>();
            services.AddScoped<HelpBuilder>();
            services.AddScoped<WebsiteWalker>();
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
        }

        public static void ConfigureBotLogging(this LoggerConfiguration configuration, IOptions<LoggingOptions> options)
        {
            configuration.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(options.Value.ElasticsearchNodeUri))
                {
                    IndexFormat = "dustybot-{0:yyyy-MM-dd}",
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    DetectElasticsearchVersion = true,
                    RegisterTemplateFailure = RegisterTemplateRecovery.FailSink,
                    EmitEventFailure = EmitEventFailureHandling.ThrowException
                })
                .Enrich.FromLogContext()
                .MinimumLevel.Information();
        }

        private static IServiceCollection AddDiscordClient(this IServiceCollection services)
        {
            services.AddSingleton(x =>
            {
                var intents = GatewayIntents.DirectMessageReactions |
                    GatewayIntents.DirectMessages |
                    GatewayIntents.GuildEmojis |
                    GatewayIntents.GuildMembers |
                    GatewayIntents.GuildMessageReactions |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.GuildMessageTyping |
                    GatewayIntents.Guilds;

                var config = new DiscordSocketConfig
                {
                    MessageCacheSize = 200,
                    ConnectionTimeout = int.MaxValue,
                    ExclusiveBulkDelete = true,
                    GatewayIntents = intents
                };

                return new DiscordShardedClient(config)
                    .UseSerilog(x.GetRequiredService<ILogger>());
            });

            services.AddSingleton<BaseSocketClient>(x => x.GetRequiredService<DiscordShardedClient>());
            return services;
        }
    }
}
