using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MEC;
using SixModLoader;
using SixModLoader.Api;
using SixModLoader.Api.Events.Server;
using SixModLoader.Events;
using SixModLoader.Mods;

namespace ScriptLoader
{
    [Mod("SixModLoader.ScriptLoader")]
    public class ScriptLoader
    {
        public static ScriptLoader Instance { get; private set; }
        
        public string ScriptsPath { get; }
        public Dictionary<string, ScriptInfo> AvailableScripts { get; set; } = new Dictionary<string, ScriptInfo>();
        private FileSystemWatcher _fileSystemWatcher;
        private Assembly _lastCompilationAssembly;
        private string _lastCompilationHash;
        private LoggerTextWriter _loggerTextWriter;
        public bool ShouldRecompile { get; set; }

        [Inject]
        public SixModLoaderApi Api { get; set; }

        public ScriptLoader(ModContainer<ScriptLoader> modContainer)
        {
            ScriptsPath = Path.Combine(modContainer.Directory, "scripts");
            Directory.CreateDirectory(ScriptsPath);
            Utilities.KnownPaths["Scripts"] = ScriptsPath;
            Instance = this;
        }

        [EventHandler(typeof(ModEnableEvent))]
        public void OnEnable()
        {
            _loggerTextWriter = new LoggerTextWriter();

            _fileSystemWatcher = new FileSystemWatcher(ScriptsPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.cs"
            };

            _fileSystemWatcher.Changed += (sender, args) =>
            {
                Logger.Info($"File {Path.GetFileName(args.Name)} changed. Recompiling...");
                ShouldRecompile = true;
            };
            _fileSystemWatcher.Deleted += (sender, args) =>
            {
                Logger.Info($"File {Path.GetFileName(args.Name)} removed. Recompiling...");
                ShouldRecompile = true;
            };
            _fileSystemWatcher.Created += (sender, args) =>
            {
                Logger.Info($"File {Path.GetFileName(args.Name)} created. Recompiling...");
                ShouldRecompile = true;
            };
            _fileSystemWatcher.Renamed += (sender, args) =>
            {
                Logger.Info($"File {Path.GetFileName(args.Name)} renamed. Recompiling...");
                ShouldRecompile = true;
            };
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        [EventHandler(typeof(ServerConsoleReadyEvent))]
        public void OnServerConsoleReady()
        {
            CompileScripts();
            Timing.RunCoroutine(Update());
        }

        [EventHandler(typeof(ModDisableEvent))]
        public void OnDisable()
        {
            _fileSystemWatcher.EnableRaisingEvents = false;
            _fileSystemWatcher.Dispose();
        }

        private IEnumerator<float> Update()
        {
            while (true)
            {
                yield return Timing.WaitForOneFrame;
                if (ShouldRecompile)
                {
                    CompileScripts();
                    ShouldRecompile = false;
                }
            }
        }

        private void CompileScripts()
        {
            if (!Directory.Exists(ScriptsPath))
            {
                Directory.CreateDirectory(ScriptsPath);
                return;
            }

            try
            {
                var files = Directory.GetFiles(ScriptsPath, "*.cs");
                AvailableScripts = files.ToDictionary(f => f, ScriptInfo.FromTextFile);

                Logger.Info($"Found {files.Length} scripts to compile");

                var md5 = MD5.Create();
                var scriptDict = new Dictionary<string, byte[]>();
                foreach (var scriptFile in files)
                {
                    var data = File.ReadAllBytes(scriptFile);
                    md5.TransformBlock(data, 0, data.Length, null, 0);
                    scriptDict[scriptFile] = data;
                }

                md5.TransformFinalBlock(new byte[0], 0, 0);
                var hash = Convert.ToBase64String(md5.Hash);

                if (hash == _lastCompilationHash)
                {
                    Logger.Info("No changes detected! Skipping compilation!");
                    return;
                }

                foreach (var scriptFile in files)
                {
                    if (!AvailableScripts.TryGetValue(scriptFile, out var info)) continue;
                    foreach (var infoReference in info.References)
                        Assembly.LoadFile(infoReference);
                }

                var ass = MonoCompiler.Compile(scriptDict, _loggerTextWriter);

                if (ass == null)
                {
                    Logger.Error("Skipping loading scripts because of errors above.");
                    return;
                }

                if (_lastCompilationAssembly != null)
                    foreach (var type in _lastCompilationAssembly.GetTypes())
                    {
                        Api.CommandManager.UnregisterCommand(type);

                        var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                            .FirstOrDefault(m => m.Name == "Unload" && m.GetParameters().Length == 0);

                        if (method == null)
                            continue;

                        SixModLoader.SixModLoader.Instance.EventManager.UnregisterStatic(type);
                        Logger.Info($"Unloading {type.Name}");
                        method.Invoke(null, new object[0]);
                    }

                _lastCompilationAssembly = ass;
                _lastCompilationHash = hash;

                foreach (var type in ass.GetTypes())
                {
                    Api.CommandManager.RegisterCommand(type);

                    var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "Main" && m.GetParameters().Length == 0);

                    if (method == null)
                        continue;

                    SixModLoader.SixModLoader.Instance.EventManager.RegisterStatic(type);
                    Logger.Info($"Running {type.Name}");
                    method.Invoke(null, new object[0]);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Failed compiling scripts\n" + e);
            }
        }

        internal class LoggerTextWriter : TextWriter
        {
            private StringBuilder StringBuilder { get; } = new StringBuilder();

            public override Encoding Encoding { get; } = Encoding.UTF8;

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    Logger.Info(StringBuilder.ToString());
                    StringBuilder.Length = 0;
                    return;
                }

                StringBuilder.Append(value);
            }
        }
    }
}