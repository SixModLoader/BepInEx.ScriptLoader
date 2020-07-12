# ScriptLoader for SixModLoader

This is a SixModLoader fork of BepInEx plugin that allows you to run C# script files without compiling them to a DLL.

This plugin uses a modified version of the Mono Compiler Service (mcs) that allows to use most of C# 7 features.  
The compiler relies on `System.Reflection.Emit` being present! As such, Unity games using .NET Standard API (i.e. has `netstandard.dll` in the `Managed` folder) 
will not be able to run this plugin!

**Now scripts ignore visibility checks!** Thanks to [custom MCS](https://github.com/denikson/mcs-unity), you can now access private members (methods, fields) via scripts!

## Writing and installing scripts

**To install scripts**, place raw `.cs` files (C# source code) into `SixModLoader/Mods/ScriptLoader/scripts`.  
**To remove scripts**, remove them from the `scripts` folder (or change file extension to `.cs.off`).

ScriptLoader will automatically load and compile all C# source code files it finds in the folder.  
ScriptLoader will also automatically run any `static void Main()` methods it finds.

Example script:

```csharp
// #name Example script
// #author js6pak
// #desc Descrption of script

using SixModLoader;
using UnityEngine;
using System;
using CommandSystem;
using SixModLoader.Api.Configuration;
using SixModLoader.Api.Extensions;
using SixModLoader.Events;
using SixModLoader.Mods;
using HarmonyLib;

[AutoCommandHandler(typeof(GameConsoleCommandHandler))]
[AutoCommandHandler(typeof(ClientCommandHandler))]
[AutoCommandHandler(typeof(RemoteAdminCommandHandler))]
public class ExampleCommand : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = $"Hello {(sender is CommandSender commandSender ? commandSender.Nickname : "someone")}!";

        return true;
    }

    public string Command => "example-script";
    public string[] Aliases => new[] { "exs" };
    public string Description => "Example script command!";
}

public static class ExampleScript {
    public static void Main() {
        Logger.Info("Hello, world!");
    }

    public static void Unload() {
        Logger.Info("Goodbye, world!");
    }
}
```

### Reloading scripts

ScriptLoader automatically detects changes in the scripts and reloads them.  

In order to make your script reloadable, **you must implement `static void Unload()`** method that cleans up any used resources.  
This is done because of Mono's limitation: you cannot actually unload any already loaded assemblies in Mono. Because of that, you should 
clean up your script so that the newly compiled script will not interfere with your game!

### Specifying metadata

You can specify metadata *at the very start of your script* by using the following format:

```csharp
// #name Short name of the script
// #author ghorsington
// #desc A longer description of the script. This still should be a one-liner.
// #ref ${Managed}/UnityEngine.UI.dll
// #ref ${SixModLoaderRoot}/core/MyDependency.dll

using UnityEngine;
...
```

The `ref` tag is special: ScriptLoader will automatically load any assemblies specified with the tag.  
The path is relative to the `scripts` folder, but you can use `${Folder}` to reference some special folders.

Currently the following special folders are available:

* `Managed` -- path to the game's Managed folder with all the main DLLs
* `Scripts` -- path to the `scripts` folder
* `SixModLoaderRoot` -- path to the `SixModLoaderRoot` folder

### Compilation errors

At this moment the compilation errors are simply written to the console.