﻿using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Settings
{
    public class BotConfig : BaseGlobalSettings
    {
        public string CommandPrefix { get; set; }
        public string BotToken { get; set; }
        public List<ulong> OwnerIDs { get; set; } = new List<ulong>();
        public string YouTubeKey { get; set; }
        public GoogleAccountCredentials GCalendarSAC { get; set; }
        public string ShortenerKey { get; set; }
        public string LastFmKey { get; set; }
        public string SpotifyId { get; set; }
        public string SpotifyKey { get; set; }
        public string TableStorageConnectionString { get; set; }
        public string SqlDbConnectionString { get; set; }
        public string PapagoClientId { get; set; }
        public string PapagoClientSecret { get; set; }
    }
}