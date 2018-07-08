﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Modules
{
    public abstract class Module : Events.EventHandler, IModule
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool Hidden { get; private set; }

        public IEnumerable<CommandRegistration> HandledCommands { get; private set; }

        public Module()
        {
            var module = GetType().GetTypeInfo();

            //Module attributes
            var moduleAttr =  module.GetCustomAttribute<ModuleAttribute>();
            if (moduleAttr == null)
                throw new InvalidOperationException("A module derived from Module must use the Module attribute.");

            Name = moduleAttr.Name;
            Description = moduleAttr.Description;
            Hidden = module.GetCustomAttribute<HiddenAttribute>() != null;

            //Command attributes
            var handledCommandsList = new List<CommandRegistration>();
            foreach (var method in module.GetMethods())
            {
                var commandAttr = method.GetCustomAttribute<CommandAttribute>();
                if (commandAttr == null)
                    continue;

                //Required
                var command = new CommandRegistration
                {
                    InvokeString = commandAttr.InvokeString,
                    Verb = commandAttr.Verb,
                    Handler = (CommandRegistration.CommandHandler)method.CreateDelegate(typeof(CommandRegistration.CommandHandler), this),
                    Description = commandAttr.Description
                };

                //Optional
                var parameters = method.GetCustomAttribute<ParametersAttribute>();
                if (parameters != null)
                    command.RequiredParameters = new List<ParameterType>(parameters.RequiredParameters);

                var permissions = method.GetCustomAttribute<PermissionsAttribute>();
                if (permissions != null)
                    command.RequiredPermissions = new HashSet<Discord.GuildPermission>(permissions.RequiredPermissions);

                var botPermissions = method.GetCustomAttribute<BotPermissionsAttribute>();
                if (botPermissions != null)
                    command.BotPermissions = new HashSet<Discord.GuildPermission>(botPermissions.RequiredPermissions);

                var usage = method.GetCustomAttribute<UsageAttribute>();
                if (usage != null)
                    command.Usage = usage.Usage;
                
                command.RunAsync = method.GetCustomAttribute<RunAsyncAttribute>() != null;
                command.OwnerOnly = method.GetCustomAttribute<OwnerOnlyAttribute>() != null;
                command.Hidden = method.GetCustomAttribute<HiddenAttribute>() != null;
                command.DirectMessageOnly = method.GetCustomAttribute<DirectMessageOnlyAttribute>() != null;
                command.DirectMessageAllow = method.GetCustomAttribute<DirectMessageAllowAttribute>() != null;
                command.TypingIndicator = method.GetCustomAttribute<TypingIndicatorAttribute>() != null;

                handledCommandsList.Add(command);
            }

            HandledCommands = handledCommandsList;
        }
    }
}
