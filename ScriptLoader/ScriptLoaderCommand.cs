using System;
using System.Linq;
using CommandSystem;
using SixModLoader.Api.Extensions;

namespace ScriptLoader
{
    [AutoCommandHandler(typeof(GameConsoleCommandHandler))]
    [AutoCommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ScriptLoaderCommand : ParentCommand
    {
        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            return DefaultCommand.Execute(arguments, sender, out response);
        }

        public ICommand DefaultCommand;
        public override string Command => "scriptloader";
        public override string[] Aliases => new[] {"sl"};
        public override string Description => "ScriptLoader";

        public ScriptLoaderCommand()
        {
            LoadGeneratedCommands();
        }

        public class ListCommand : ICommand
        {
            public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                var scripts = ScriptLoader.Instance.AvailableScripts.Values;
                response = $"Scripts ({scripts.Count}): " + string.Join(", ", scripts.Select(script => script.Name));
                return true;
            }

            public string Command => "list";
            public string[] Aliases => new string[0];
            public string Description => "Lists scripts";
        }
        
        public override void LoadGeneratedCommands()
        {
            RegisterCommand(DefaultCommand = new ListCommand());
        }
    }
}