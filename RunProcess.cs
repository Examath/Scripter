using Examath.Core.Environment;
using Examath.Core.Plugin;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Scripter
{
    public class RunProcess : Plugin, IExecuteAsync
    {
        public override Color Colour => Color.FromRgb(0, 255, 255);

        public async Task Execute(Env e)
        {
            if (e.Model is SModel sModel)
            {
                Log log = e.StartLog();
                log.StartTiming($"Running {Path.GetFileName(sModel.CompLocation)}");

                Process process = new();
                if (string.IsNullOrWhiteSpace(sModel.Argument))
                {
                    process.StartInfo = new ProcessStartInfo(sModel.CompLocation);
                }
                else
                {
                    process.StartInfo = new ProcessStartInfo(sModel.CompLocation, sModel.Argument);
                }
                process.Start();
                await process.WaitForExitAsync();

                log.EndTiming();
                log.Out($"Exited with code {process.ExitCode}");
            }
        }
    }
}
