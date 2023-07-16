using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Scripter
{
    public partial class ScriptMetadata : ObservableObject
    {
        #region Imports

        private ObservableCollection<Import> _Imports = new();
        /// <summary>
        /// List of filelocations for imports
        /// </summary>
        public ObservableCollection<Import> Imports
        {
            get => _Imports;
            set => SetProperty(ref _Imports, value);
        }

        #endregion

        #region Options

        private ScripterOutputKind _OutputKind = ScripterOutputKind.Plugin;
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public ScripterOutputKind OutputKind
        {
            get => _OutputKind;
            set => SetProperty(ref _OutputKind, value);
        }

        private bool _ShouldLoadAutomatically = true;
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public bool ShouldLoadAutomatically
        {
            get => _ShouldLoadAutomatically;
            set => SetProperty(ref _ShouldLoadAutomatically, value);
        }

        private string _Argument = string.Empty;
        /// <summary>
        /// Gets or sets 
        /// </summary>
        public string Argument
        {
            get => _Argument;
            set => SetProperty(ref _Argument, value);
        }


        #endregion

        #region Commands

        [RelayCommand]
        public void Fix(Import? import)
        {
            if (import != null)
            {
                OpenFileDialog openFileDialog = new()
                {
                    Title = $"Fix {import.Location}",
                    Filter = "Dynamic Link Library (*.dll)|*.dll|All files|*.*",
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    import.Location = openFileDialog.FileName;
                }
            }
        }

        #endregion
    }

    public partial class Import : ObservableObject
    {
        private string _Location = string.Empty;

        /// <summary>
        /// Gets or sets the location of the import library
        /// </summary>
        [XmlText]
        public string Location
        {
            get => _Location;
            set
            {
                if (!File.Exists(value))
                {
                    Match m = LibRef().Match(value);
                    if (m.Success)
                    {
                        DirectoryInfo directoryInfo = new(m.Groups[1].Value);
                        if (directoryInfo.Exists)
                        {
                            DirectoryInfo? sub = directoryInfo.GetDirectories().OrderByDescending(d => d.Name).FirstOrDefault();
                            if (sub != null)
                            {
                                value = sub.FullName + m.Groups[2].Value;
                            }
                        }
                    }
                }

                if (SetProperty(ref _Location, value))
                {
                    if (File.Exists(value))
                    {
                        Image = MetadataReference.CreateFromFile(value);
                    };
                }
            }
        }

        private MetadataReference? _Image;
        /// <summary>
        /// The <see cref="MetadataReference"/> associated with this import
        /// </summary>
        [XmlIgnore]
        public MetadataReference? Image { get => _Image; private set => SetProperty(ref _Image, value); }

        public Import()
        {

        }

        /// <summary>
        /// Imports the library of the provided <paramref name="type"/>
        /// </summary>
        /// <param name="type">Any type from the desired assembly</param>
        public Import(System.Type type)
        {
            _Location = type.GetTypeInfo().Assembly.Location;
            Image = MetadataReference.CreateFromFile(_Location);
        }

        /// <summary>
        /// Imports the library at the provided file <paramref name="location"/>
        /// </summary>
        /// <param name="location">File location of dll</param>
        /// <remarks>
        /// Also loads the corresponding <see cref="MetadataReference"/>.
        /// Enclose this method in a try-catch loop incase errors.
        /// </remarks>
        public Import(string location)
        {
            _Location = location;
            if (File.Exists(location))
            {
                Image = MetadataReference.CreateFromFile(_Location);
            }
        }

        public override string ToString()
        {
            return Location;
        }

        [GeneratedRegex(@"^(.*)\\.*(\\.*)")]
        private static partial Regex LibRef();
    }

    public enum ScripterOutputKind
    {
        ConsoleApplication = 0,
        Plugin = 6,
    }

    public static class ScripterOutputKindExtensions
    {
        /// <summary>
        /// Gets the equilavent <see cref="OutputKind"/> for this <see cref="ScripterOutputKind"/>
        /// </summary>
        public static OutputKind ToCodeAnalysisOutputKind(this ScripterOutputKind scripterOutputKind)
        {
            if (scripterOutputKind == ScripterOutputKind.Plugin) return Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary;
            else return (OutputKind)scripterOutputKind;
        }

        private static readonly string[] _Extensions = { ".exe", ".exe", ".dll", ".netmodule", ".winmdobj", ".exe", ".dll" };

        /// <summary>
        /// Gets the extension for this type of <see cref="ScripterOutputKind"/>
        /// </summary>
        /// <param name="scripterOutputKind"></param>
        /// <returns>A <see cref="string"/> containing the extension, including the '<c>.</c>'</returns>
        public static string GetExtension(this ScripterOutputKind scripterOutputKind)
        {
            return _Extensions[(int)scripterOutputKind];
        }
    }
}
