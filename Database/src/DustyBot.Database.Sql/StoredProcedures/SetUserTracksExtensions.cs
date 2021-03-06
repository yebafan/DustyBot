﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Sql.StoredProcedures.Utility;
using DustyBot.Database.Sql.UserDefinedTypes;
using StoredProcedureEFCore;

namespace DustyBot.Database.Sql.StoredProcedures
{
    public static class SetUserTracksExtensions
    {
        public static Task SetUserTracksAsync(this DustyBotDbContext dbContext, IEnumerable<SetUserTracksTable> tracks, CancellationToken ct)
        {
            return dbContext.LoadStoredProc("[DustyBot].[SetUserTracks]")
                .AddParam("@tracks", tracks, SetUserTracksTable.TypeName)
                .ExecNonQueryAsync(ct);
        }
    }
}
