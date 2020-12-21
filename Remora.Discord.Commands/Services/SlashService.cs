//
//  SlashService.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Remora.Commands.Trees;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Abstractions.Results;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Results;
using Remora.Discord.Core;

namespace Remora.Discord.Commands.Services
{
    /// <summary>
    /// Handles updating and verifying of slash commands.
    /// </summary>
    public class SlashService
    {
        private readonly CommandTree _commandTree;
        private readonly IDiscordRestOAuth2API _oauth2API;
        private readonly IDiscordRestApplicationAPI _applicationAPI;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlashService"/> class.
        /// </summary>
        /// <param name="commandTree">The command tree.</param>
        /// <param name="oauth2API">The OAuth2 API.</param>
        /// <param name="applicationAPI">The application API.</param>
        public SlashService
        (
            CommandTree commandTree,
            IDiscordRestOAuth2API oauth2API,
            IDiscordRestApplicationAPI applicationAPI
        )
        {
            _commandTree = commandTree;
            _applicationAPI = applicationAPI;
            _oauth2API = oauth2API;
        }

        /// <summary>
        /// Determines whether the application's commands support being bound to Discord slash commands.
        /// </summary>
        /// <returns>true if slash commands are supported; otherwise, false.</returns>
        public bool SupportsSlashCommands()
        {
            // TODO: Improve
            // Yes, this is inefficient. Generally, this method is only expected to be called once on startup.
            return _commandTree.CreateApplicationCommands(out _).IsSuccess;
        }

        /// <summary>
        /// Updates the application's slash commands.
        /// </summary>
        /// <param name="guildID">The ID of the guild to update slash commands in, if any.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        public async Task<UpdateCommandsResult> UpdateSlashCommandsAsync
        (
            Snowflake? guildID = null,
            CancellationToken ct = default
        )
        {
            var getApplication = await _oauth2API.GetCurrentApplicationInformationAsync(ct);
            if (!getApplication.IsSuccess)
            {
                return UpdateCommandsResult.FromError(getApplication);
            }

            var application = getApplication.Entity;
            var createCommands = _commandTree.CreateApplicationCommands(out var commands);
            if (!createCommands.IsSuccess)
            {
                return UpdateCommandsResult.FromError(createCommands);
            }

            Func<IApplicationCommandOption, Task<ICreateRestEntityResult<IApplicationCommand>>> updateMethod;
            if (guildID is null)
            {
                updateMethod = c => _applicationAPI.CreateGlobalApplicationCommandAsync
                (
                    application.ID,
                    c.Name,
                    c.Description,
                    c.Options,
                    ct
                );
            }
            else
            {
                updateMethod = c => _applicationAPI.CreateGuildApplicationCommandAsync
                (
                    application.ID,
                    guildID.Value,
                    c.Name,
                    c.Description,
                    c.Options,
                    ct
                );
            }

            foreach (var command in commands!)
            {
                var updateResult = await updateMethod(command);
                if (!updateResult.IsSuccess)
                {
                    return UpdateCommandsResult.FromError(updateResult);
                }
            }

            return UpdateCommandsResult.FromSuccess();
        }
    }
}
