using System;
using System.CommandLine;
using System.Linq;
using Wyam.Common.IO;
using Wyam.Common.Tracing;
using Wyam.Configuration.Preprocessing;

namespace Wyam.Commands
{
    internal class NewCommand : Command
    {
        private readonly ConfigOptions _configOptions = new ConfigOptions();

        private DirectoryPath _path = null;

        public override string Description => "Scaffolds the given recipe into a specified path.";

        public override string[] SupportedDirectives => new[]
        {
            "nuget",
            "nuget-source",
            "assembly",
            "recipe"
        };

        protected override void ParseOptions(ArgumentSyntax syntax)
        {
            syntax.DefineOption("u|update-packages", ref _configOptions.UpdatePackages, "Check the NuGet server for more recent versions of each package and update them if applicable.");
            syntax.DefineOption("use-local-packages", ref _configOptions.UseLocalPackages, "Toggles the use of a local NuGet packages folder.");
            syntax.DefineOption("use-global-sources", ref _configOptions.UseGlobalSources, "Toggles the use of the global NuGet sources (default is false).");
            syntax.DefineOption("packages-path", ref _configOptions.PackagesPath, DirectoryPath.FromString, "The packages path to use (only if use-local is true).");
        }

        protected override void ParseParameters(ArgumentSyntax syntax)
        {
            syntax.DefineParameter("path", ref _path, DirectoryPath.FromString, "The path to generate the scaffold in.");
        }

        protected override ExitCode RunCommand(Preprocessor preprocessor)
        {
            // Make sure we actually got a recipe value
            if (preprocessor.Values.All(x => x.Name != "recipe"))
            {
                Trace.Critical("A recipe must be specified");
                return ExitCode.CommandLineError;
            }

            _configOptions.RootPath = Environment.CurrentDirectory;
            _path = _path ?? "input";

            // Get the engine and configurator
            using (EngineManager engineManager = EngineManager.Get(preprocessor, _configOptions))
            {
                if (engineManager == null)
                {
                    return ExitCode.CommandLineError;
                }
                
                // Check to make sure the directory is empty (and provide option to clear it)
                IDirectory scaffoldDirectory = engineManager.Engine.FileSystem.GetRootDirectory(_path);
                if (scaffoldDirectory.Exists)
                {
                    Console.WriteLine($"Scaffold directory {scaffoldDirectory.Path.FullPath} exists, are you sure you want to clear it [y|N]?");
                    char inputChar = Console.ReadKey(true).KeyChar;
                    if (inputChar != 'y' && inputChar != 'Y')
                    {
                        Trace.Information($"Scaffold directory will not be cleared");
                        return ExitCode.Normal;
                    }
                    Trace.Information($"Scaffold directory will be cleared");
                }
                else
                {
                    Trace.Information($"Scaffold directory {scaffoldDirectory.Path.FullPath} does not exist and will be created");
                }
                if (scaffoldDirectory.Exists)
                {
                    scaffoldDirectory.Delete(true);
                }
                scaffoldDirectory.Create();

                // We can ignore theme packages since we don't care about the theme for scaffolding
                engineManager.Configurator.IgnoreKnownThemePackages = true;

                // Configure everything (primarily to get the recipe)
                try
                {
                    engineManager.Configurator.Configure(null);
                }
                catch (Exception ex)
                {
                    Trace.Critical("Error while configuring engine: {0}", ex.Message);
                    return ExitCode.ConfigurationError;
                }
                
                // Scaffold the recipe
                engineManager.Configurator.Recipe.Scaffold(scaffoldDirectory);
            }

            return ExitCode.Normal;
        }
    }
}