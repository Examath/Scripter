using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Scripter.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Examath.Core.Model;
using ICSharpCode.AvalonEdit.Document;
using System.Diagnostics;
using System.Xml.Serialization;
using Examath.Core.Plugin;
using Examath.Core.Environment;
using Microsoft.Win32;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel;

namespace Scripter
{
    public partial class Executor : FileManipulationObject
    {
        #region Init

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Executor() : base(new("Adapted C# file", "*.acs"), new("C# file", "*.cs"))
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            AddCodeEventHandlers();
            _ModifyTimer = new(_ => AutoParseAsync());
            Env.Default = Env;
            Env.Model = _SModel;
        }

        #endregion

        #region Options

        private bool _AutoParse = true;
        /// <summary>
        /// Gets or sets whether code is automatically parsed when code is changed
        /// </summary>
        public bool AutoParse
        {
            get => _AutoParse;
            set => SetProperty(ref _AutoParse, value);
        }

        #endregion

        #region File Implementation

        private const string METADATA_START_SYMBOL = "\n/*<ScriptMetadata/>\n";
        private const string METADATA_END_SYMBOL = "\n*/";

        private readonly XmlSerializer _MetadataSerializer = new(typeof(ScriptMetadata));

        public override string FileLocation
        {
            get => Settings.Default.FileName;
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
            set
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
            {
                Settings.Default.FileName = value;
                Settings.Default.Save();
                OnPropertyChanged(nameof(FileLocation));
                SaveCommand.NotifyCanExecuteChanged();
            }
        }

        public override void CreateFile()
        {
            RemoveCodeEventHandlers();
            Code = new();
            AddCodeEventHandlers();
            ResetMetadata();
        }

        public override async Task LoadFileAsync()
        {
            if (FileLocation != null)
            {
                Log log = _Env.StartLog();
                log.StartTiming($"Opening {Path.GetFileName(FileLocation)}");

                string[] parts = (await File.ReadAllTextAsync(FileLocation)).Split(METADATA_START_SYMBOL);
                Code.Text = parts[0];
                IsModified = false;
                await Task.Run(() => SyntaxTree = CSharpSyntaxTree.ParseText(parts[0]));

                try
                {
                    if (parts.Length >= 2)
                    {
                        // parts[1] contains metadata, inside /*...*/ comment.
                        // Hence the end symbol are removed
                        using TextReader reader = new StringReader(parts[1][..^METADATA_END_SYMBOL.Length]);
                        object? result = _MetadataSerializer.Deserialize(reader);
                        if (result is ScriptMetadata metadata)
                        {
                            Metadata = metadata;
                        }
                        else
                        {
                            ResetMetadata();
                        }
                    }
                    else
                    {
                        ResetMetadata();
                    }
                }
                catch (Exception e)
                {
                    log.OutException(e, "Loading Metadata");
                    ResetMetadata();
                }

                log.EndTiming($"{parts[0].Length / 1024:0.0} kB");
            };
        }

        public override async Task SaveFileAsync()
        {
            if (Path.GetExtension(FileLocation) == ".cs")
            {
                await File.WriteAllTextAsync(FileLocation, Code.Text);
            }
            else
            {
                StringWriter stringWriter = new();
                _MetadataSerializer.Serialize(stringWriter, Metadata);
                await File.WriteAllTextAsync(FileLocation, Code.Text + METADATA_START_SYMBOL + stringWriter.ToString() + METADATA_END_SYMBOL);
            }
        }
        #endregion

        #region Code

        private TextDocument _Code = new();
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public TextDocument Code
        {
            get => _Code;
            set { if (SetProperty(ref _Code, value)) IsModified = true; }
        }

        /// <summary>
        /// Adds any needed event handlers to the <see cref="TextDocument"/>.
        /// Should be called after any initialisation of Code.
        /// </summary>
        private void AddCodeEventHandlers()
        {
            Code.Changed += CodeModified;
        }

        /// <summary>
        /// Removes any needed event handlers from the <see cref="TextDocument"/>
        /// Should be called before Code is rebuilt.
        /// </summary>
        private void RemoveCodeEventHandlers()
        {
            Code.Changed -= CodeModified;
        }

        private readonly TimeSpan ModifyInterval = new(0, 0, 2);

        private readonly System.Threading.Timer _ModifyTimer;

        private string _CodeText = String.Empty;

        private void CodeModified(object? sender, DocumentChangeEventArgs e)
        {
            IsModified = true;
            IsCodeParsed = false;
            _CodeText = Code.Text;
            _ModifyTimer.Change(ModifyInterval, Timeout.InfiniteTimeSpan);
        }

        [RelayCommand]
        private async Task FormatDocument()
        {
            if (!IsCodeParsed) await ParseAsync(_CodeText);
            _CodeText = SyntaxTree.GetRoot().NormalizeWhitespace().ToString();
            Code.BeginUpdate();
            Code.Text = _CodeText;
            Code.EndUpdate();
            IsModified = true;
        }

        #endregion Code

        #region Syntax

        private bool _IsCodeParsed = true;
        /// <summary>
        /// Gets or sets whether the code has been parsed
        /// </summary>
        public bool IsCodeParsed
        {
            get => _IsCodeParsed;
            set=> SetProperty(ref _IsCodeParsed, value);
        }

        private SyntaxTree SyntaxTree { get; set; }

        private IEnumerable<Diagnostic> _Diagnostics;
        /// <summary>
        /// Gets or sets a list of all syntax errors
        /// </summary>
        public IEnumerable<Diagnostic> Diagnostics
        {
            get => _Diagnostics;
            set => SetProperty(ref _Diagnostics, value);
        }

        /// <summary>
        /// Calls the private <see cref="ParseAsync"/> if conditions are met
        /// </summary>
        public async void AutoParseAsync()
        {
            if (AutoParse && !IsCodeParsed)
            {
                await ParseAsync(_CodeText);
            }
        }

        /// <summary>
        /// Parses the code to a <see cref="SyntaxTree"/>
        /// </summary>
        private async Task ParseAsync(string code)
        {
            //SyntaxTree = SyntaxTree.WithChangedText(new());
            IsCodeParsed = true;

            SyntaxTree = await Task.Run(() => { return CSharpSyntaxTree.ParseText(code); });
            Diagnostics = SyntaxTree.GetDiagnostics();
        }

        #endregion

        #region Metadata

        private ScriptMetadata _Metadata = new();
        /// <summary>
        /// Gets or sets the metadata for this script
        /// </summary>
        public ScriptMetadata Metadata
        {
            get => _Metadata;
            set => SetProperty(ref _Metadata, value);
        }

        //[RelayCommand]
        //public void SearchType


        [RelayCommand]
        public void ImportFromFile()
        {
            IsModified = true;
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Dynamic Link Library (.dll)|*.dll|All files|*.*",
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Metadata.Imports.Add(new(openFileDialog.FileName));
                }
                catch (Exception e)
                {
                    Env.OutException(e, "Importing from file");
                }

            }
        }

        [RelayCommand]
        public void RemoveImport(Import? import)
        {
            if (import != null && Metadata.Imports.Remove(import))
            {
                IsModified = true;
            }
        }

        [RelayCommand]
        private void ResetMetadata()
        {
            Metadata = new()
            {
                Imports = new()
                    {
                        new(typeof(object)),
                        new(typeof(Window)),
                        new(typeof(System.Windows.Input.Keyboard)),
                        new(typeof(System.Windows.Input.Key)),
                        new(typeof(TextAlignment)),
                        new(Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location) ?? "", "System.Runtime.dll")),
                        new(typeof(Env)),
                    },
            };
            IsModified = true;
        }

        #endregion

        #region Env

        private Env _Env = new();
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public Env Env
        {
            get => _Env;
            set => SetProperty(ref _Env, value);
        }

        private readonly SModel _SModel = new();

        #endregion

        #region Compile

        /// <summary>
        /// Compiles the code
        /// </summary>
        /// <returns></returns>
        //https://github.com/joelmartinez/dotnet-core-roslyn-sample/blob/master/Program.cs
        [RelayCommand]
        private async Task Compile()
        {
            // Preparation
            Log log = Env.StartLog($"Compiling {Path.GetFileName(FileLocation)}");
            log.Out($"{Metadata.OutputKind} with {Metadata.Imports.Count} imports");
            log.Output.Style = (Style)Env.Output.Resources["MetaSectionStyle"];

            // Saving
            if (CanSave())
            {
                log.StartTiming("Saving code");
                await Save();
            }

            //Unloading
            if (PluginHost is ExternalPluginHost externalPluginHost)
            {
                await externalPluginHost.UnloadAsync(log);
            }

            // Parsing
            if (!IsCodeParsed)
            {
                log.StartTiming("Parsing");
                string code = Code.Text;
                await ParseAsync(code);
            }

            // Compile
            log.StartTiming("Compiling");
            string compLocation = Path.GetDirectoryName(FileLocation) + "\\" + Path.GetFileNameWithoutExtension(FileLocation) + Metadata.OutputKind.GetExtension();
            string assemblyName = Path.GetFileNameWithoutExtension(FileLocation).Replace(" ", "");
            MetadataReference[] references = Metadata.Imports.Select(r => r.Image).ToArray();

            FileStream fileStream;
            try
            {
                fileStream = new FileStream(compLocation, FileMode.Create);
            }
            catch (System.IO.IOException)
            {
                int tempN = Random.Shared.Next();
                log.Out($"Compilation file could not be accessed, possibly because it is still being used. " +
                    $"Hence compilation is emitted to z{tempN}", ConsoleStyle.FormatBlockStyle);
                compLocation = Path.GetDirectoryName(FileLocation) + "\\z" + tempN + Metadata.OutputKind.GetExtension();
                assemblyName = "z" + tempN;
                fileStream = new FileStream(compLocation, FileMode.Create);
            }

            CSharpCompilation compilation = await Task.Run(() =>
                {
                    return CSharpCompilation.Create(
                        assemblyName,
                        syntaxTrees: new[] { SyntaxTree },
                        references: references,
                        options: new CSharpCompilationOptions(Metadata.OutputKind.ToCodeAnalysisOutputKind()));
                });

            EmitResult compResult;

            compResult = compilation.Emit(fileStream);

            fileStream.Dispose();

            Diagnostics = compResult.Diagnostics;

            log.EndTiming();

            // Postscript

            if (!compResult.Success)
            {
                Paragraph errorHeading = new(new Run("Compilation failed"))
                {
                    Style = (Style)Env.Output.Resources["FormatBlockStyle"],
                    FontWeight = FontWeights.Bold,
                };
                log.OutBlock(errorHeading);
                log.Out("See compiler output above.\nAnyways, you know you can just use Visual Studio.");
            }
            else
            {
                Paragraph successHeading = new(new Run("Compilation successful"))
                {
                    Style = (Style)Env.Output.Resources["NewBlockStyle"],
                    FontWeight = FontWeights.Bold,
                };
                log.OutBlock(successHeading);
                log.Out($"Compilation saved to {compLocation}");

                _SModel.CompLocation = compLocation;
                _SModel.Argument = Metadata.Argument;

                if (Metadata.ShouldLoadAutomatically) switch (Metadata.OutputKind)
                    {
                        case ScripterOutputKind.ConsoleApplication:
                            await LoadRunPluginAsync();
                            break;
                        case ScripterOutputKind.Plugin:
                            LoadPluginIntoScripter(log, compLocation);
                            break;
                    }
            }
        }

        private async Task LoadRunPluginAsync()
        {
            PluginHost = new InternalPluginHost(Env, typeof(RunProcess));
            PluginHost.Load();
            await PluginHost.ExecuteAsync();
        }

        private void LoadPluginIntoScripter(Log log, string compLocation)
        {
            log.StartTiming("Loading plugin");
            try
            {
                //bool hostResult = await Task.Run(PluginHost.Load);
                PluginHost = new ExternalPluginHost(Env, compLocation);
                PluginHost.Load();

                if (PluginHost != null && PluginHost.IsPluginLoaded)
                {
                    PluginHost.CallSetup();
                }
                else
                {
                    log.Out("Assembly loaded, but no plugin found or loaded", ConsoleStyle.FormatBlockStyle);
                }

                PluginHost?.ExecuteCommand.NotifyCanExecuteChanged();
            }
            catch (Exception e)
            {
                log.OutException(e, "Loading Plugin");
            }
            log.EndTiming();
        }

        #endregion

        #region Plugin Host

        private PluginHost? _PluginHost = null;
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public PluginHost? PluginHost
        {
            get => _PluginHost;
            set => SetProperty(ref _PluginHost, value);
        }

        #endregion
    }
}
