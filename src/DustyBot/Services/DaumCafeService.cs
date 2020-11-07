﻿using Discord.WebSocket;
using DustyBot.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Helpers;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Discord;
using DustyBot.Framework.Services;
using System.Diagnostics;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using DustyBot.Core.Formatting;

namespace DustyBot.Services
{
    class DaumCafeService : IDisposable, IService
    {
        public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);

        private System.Threading.Timer _timer;

        public ISettingsService Settings { get; }
        public BaseSocketClient Client { get; }
        public ILogger Logger { get; }

        public static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(15);
        private bool Updating { get; set; }
        private object UpdatingLock = new object();

        Dictionary<Guid, Tuple<DateTime, DaumCafeSession>> _sessionCache = new Dictionary<Guid, Tuple<DateTime, DaumCafeSession>>();

        public DaumCafeService(BaseSocketClient client, ISettingsService settings, ILogger logger)
        {
            Settings = settings;
            Client = client;
            Logger = logger;
        }

        public Task StartAsync()
        {
            _timer = new System.Threading.Timer(OnUpdate, null, (int)UpdateFrequency.TotalMilliseconds, (int)UpdateFrequency.TotalMilliseconds);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _timer?.Dispose();
            _timer = null;
            return Task.CompletedTask;
        }

        void OnUpdate(object state)
        {
            TaskHelper.FireForget(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                bool skip = false;
                lock (UpdatingLock)
                {
                    if (Updating)
                        skip = true; // Skip if the previous update is still running
                    else
                        Updating = true;
                }

                if (skip)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Debug, "Service", "Skipping Daum Cafe feeds update (already running)..."));
                    return;
                }

                try
                {
                    await Logger.Log(new LogMessage(LogSeverity.Debug, "Service", "Updating Daum Cafe feeds."));

                    foreach (var settings in await Settings.Read<MediaSettings>())
                    {
                        if (settings.DaumCafeFeeds == null || settings.DaumCafeFeeds.Count <= 0)
                            continue;

                        foreach (var feed in settings.DaumCafeFeeds)
                        {
                            try
                            {
                                await UpdateFeed(feed, settings.ServerId);
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to update Daum Cafe feed {feed.Id} ({feed.CafeId}/{feed.BoardId}) on server {settings.ServerId}.", ex));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Service", "Failed to update Daum Cafe feeds.", ex));
                }
                finally
                {
                    Updating = false;
                }

                await Logger.Log(new LogMessage(LogSeverity.Debug, "Service", $"Finished updating Daum Cafe feeds in {stopwatch.Elapsed}."));
            });            
        }

        async Task UpdateFeed(DaumCafeFeed feed, ulong serverId)
        {
            var guild = Client.GetGuild(serverId);
            if (guild == null)
                return; //TODO: zombie settings should be cleared

            var channel = guild.GetTextChannel(feed.TargetChannel);
            if (channel == null)
                return; //TODO: zombie settings should be cleared

            //Choose a session
            DaumCafeSession session;
            if (feed.CredentialId != Guid.Empty)
            {
                if (!_sessionCache.TryGetValue(feed.CredentialId, out var dateSession) || DateTime.Now - dateSession.Item1 > SessionLifetime)
                {
                    var credential = await Modules.CafeModule.GetCredential(Settings, feed.CredentialUser, feed.CredentialId);
                    try
                    {
                        session = await DaumCafeSession.Create(credential.Login, credential.Password);
                        _sessionCache[feed.CredentialId] = Tuple.Create(DateTime.Now, session);
                    }
                    catch (Exception ex) when (ex is CountryBlockException || ex is LoginFailedException)
                    {
                        session = DaumCafeSession.Anonymous;
                        _sessionCache[feed.CredentialId] = Tuple.Create(DateTime.Now, session);
                    }
                }
                else
                    session = dateSession.Item2;
            }
            else
                session = DaumCafeSession.Anonymous;

            //Get last post ID
            var lastPostId = await session.GetLastPostId(feed.CafeId, feed.BoardId);

            //If new feed -> just store the last post ID and return
            if (feed.LastPostId < 0)
            {
                await Settings.Modify<MediaSettings>(serverId, s =>
                {
                    var current = s.DaumCafeFeeds.FirstOrDefault(x => x.Id == feed.Id);
                    if (current != null && current.LastPostId < 0)
                        current.LastPostId = lastPostId;
                });

                return;
            }
            
            var currentPostId = feed.LastPostId;
            if (lastPostId <= feed.LastPostId)
                return;

            await Logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Updating feed {feed.CafeId}/{feed.BoardId}" + (lastPostId - currentPostId > 1 ? $", found {lastPostId - currentPostId} new posts ({currentPostId + 1} to {lastPostId})" : $" (post {lastPostId})") + $" on {guild.Name}"));

            while (lastPostId > currentPostId)
            {
                var preview = await CreatePreview(session, feed.CafeId, feed.BoardId, currentPostId + 1);

                if (!guild.CurrentUser.GetPermissions(channel).SendMessages)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Can't update Cafe feed because of permissions in #{channel.Name} ({channel.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                    currentPostId = lastPostId;
                    break;
                }

                await channel.SendMessageAsync(preview.Item1.Sanitise(), false, preview.Item2);
                currentPostId++;
            }

            await Settings.Modify<MediaSettings>(serverId, settings =>
            {
                var current = settings.DaumCafeFeeds.FirstOrDefault(x => x.Id == feed.Id);
                if (current != null && current.LastPostId < currentPostId)
                    current.LastPostId = currentPostId;
            });
        }

        private Embed BuildPreview(string title, string url, string description, string imageUrl, string cafeName)
        {
            var embedBuilder = new EmbedBuilder()
                        .WithTitle(title)
                        .WithUrl(url)
                        .WithFooter("Daum Cafe • " + cafeName);

            if (!string.IsNullOrWhiteSpace(description))
                embedBuilder.Description = description.JoinWhiteLines(2).TruncateLines(13, trim: true).Truncate(350);

            if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.Contains("cafe_meta_image.png"))
                embedBuilder.ImageUrl = imageUrl;

            return embedBuilder.Build();
        }

        public async Task<Tuple<string, Embed>> CreatePreview(DaumCafeSession session, string cafeId, string boardId, int postId)
        {
            var mobileUrl = $"http://m.cafe.daum.net/{cafeId}/{boardId}/{postId}";
            var desktopUrl = $"http://cafe.daum.net/{cafeId}/{boardId}/{postId}";

            var text = $"<{desktopUrl}>";
            Embed embed = null;
            try
            {
                var metadata = await session.GetPageMetadata(new Uri(mobileUrl));
                if (metadata.Type == "comment" && (!string.IsNullOrWhiteSpace(metadata.Body.Text) || !string.IsNullOrWhiteSpace(metadata.ImageUrl)))
                {
                    embed = BuildPreview("New memo", mobileUrl, metadata.Body.Text, metadata.Body.ImageUrl, cafeId);
                }
                else if (!string.IsNullOrEmpty(metadata.Body.Subject) && (!string.IsNullOrWhiteSpace(metadata.Body.Text) || !string.IsNullOrWhiteSpace(metadata.ImageUrl)))
                {
                    embed = BuildPreview(metadata.Body.Subject, mobileUrl, metadata.Body.Text, metadata.Body.ImageUrl, cafeId);
                }
                else if (metadata.Type == "article" && !string.IsNullOrWhiteSpace(metadata.Title) && (!string.IsNullOrWhiteSpace(metadata.Description) || !string.IsNullOrWhiteSpace(metadata.ImageUrl)))
                {
                    embed = BuildPreview(metadata.Title, mobileUrl, metadata.Description, metadata.ImageUrl, cafeId);
                }
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Warning, "Service", $"Failed to create Daum Cafe post preview for {mobileUrl}.", ex));
            }

            return Tuple.Create(text, embed);
        }
        
        #region IDisposable 

        private bool _disposed = false;
                
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
                
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                    _timer = null;
                }
                
                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }

}
