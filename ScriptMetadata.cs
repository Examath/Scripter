using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Examath.Core.Environment;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
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

        #endregion
    }

    public class Import : ObservableObject
    {
        private string _Location = string.Empty;
        /// <summary>
        /// Gets or sets 
        /// </summary>
        [XmlText]
        public string Location
        {
            get => _Location;
            set
            {
                if (SetProperty(ref _Location, value))
                {
                    Image = MetadataReference.CreateFromFile(value);
                };
            }
        }


        /// <summary>
        /// The <see cref="MetadataReference"/> associated with this import
        /// </summary>
        [XmlIgnore]
        public MetadataReference Image { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Import()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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
            Image = MetadataReference.CreateFromFile(_Location);
        }

        public override string ToString()
        {
            return Location;
        }
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
