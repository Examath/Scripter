using ICSharpCode.AvalonEdit.Document;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Scripter.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Scripter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Executor? Executor { get; set; }

        public MainWindow()
        {
            if (((App)Application.Current).Args?.Length >= 1)
            {
                Settings.Default.FileName = ((App)Application.Current).Args[0];
            }

            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CrashHandler);
            Executor = (Executor?)DataContext;
            Title += $" {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()[0..^2]}";

            CodeEditor.TextArea.SelectionChanged += TextArea_SelectionChanged;
        }

        #region Window

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Executor != null)
            {
                if (!(await Executor.TryLoad()))
                {
                    await Executor.CreateOrOpen();
                }

                Executor.Env.Out($"Press [Compile] (Alt+C) and then execute (green button or Alt+E)\nFor help using this application, um, er ... just use Microsoft Visual Studio");
            }

            IsEnabled = true;
        }

        private bool _IsReadyToClose = false;

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (_IsReadyToClose) return;

            base.OnClosing(e);
            if (Executor != null)
            {
                e.Cancel = true;
                if (await Executor.IsUserReadyToPartWithCurrentFile())
                {
                    _IsReadyToClose = true;
                    Application.Current.Shutdown();
                }
            }
        }

        private void CrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
#if DEBUG
            return;
#endif
#pragma warning disable CS0162 // Unreachable code detected when DEBUG config
            /*try
            {
                File.WriteAllText(Properties.Settings.Default.FileName, Code.Text);
#pragma warning restore CS4014
                Exception e = (Exception)args.ExceptionObject;
                MessageBox.Show($"{e.GetType().Name}: {e.Message}\nPlanfile was saved. See crash.txt in current directory fore more info.", " An Unhandled Exception Occured", MessageBoxButton.OK, MessageBoxImage.Error);
                File.WriteAllText(Core.GetFileName("\\crash.txt"),
                    $"An unhandled exception occured at {DateTime.Now:g}\n" +
                    $"A backup of PlanFile was saved at {Settings.Default.PlanFileLocation}.crashed\n" +
                    $"Error Message:\t{e.Message}\n" +
                    $"Stack Trace:\n{e.StackTrace}"
                    );
            }
            catch (Exception)
            {*/
            MessageBox.Show($"The code was not saved whist this app crashed. Oh well, you may as well use Visual Studio. Worth the lag, isn't it?", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
#pragma warning restore CS0162 // Unreachable code detected
        }

        #endregion

        private void CodeEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Executor != null) Executor.AutoParseAsync();
        }

        private void TextArea_SelectionChanged(object? sender, EventArgs e)
        {
            TextLocation selectionStartLocation = CodeEditor.Document.GetLocation(CodeEditor.SelectionStart);
            SelectionPositionLabel.Content = $"[{selectionStartLocation.Line}, {selectionStartLocation.Column}]";
        }

        private void DiagnosticsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedValue is Diagnostic diagnostic)
            {
                if (diagnostic.Location != Location.None)
                {
                    TextSpan textSpan = diagnostic.Location.SourceSpan;
                    CodeEditor.Select(textSpan.Start, (textSpan.IsEmpty) ? 1 : textSpan.Length);
                }
            }
        }
    }
}
