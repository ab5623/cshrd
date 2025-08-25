using System;
using System.Diagnostics;
using System.Windows;

namespace WpfApp1
{
    public partial class App : Application
    {
        private Process childProcess;
        private bool isShuttingDown = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            StartExe();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            isShuttingDown = true;
            KillChildProcess();

            base.OnExit(e);
        }

        private void StartExe()
        {
            string exePath = @"C:\Users\atesb\source\repos\UsingDLL\UsingDLL\bin\Debug\net8.0\UsingDLL.exe";

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            childProcess = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            childProcess.Exited += ChildProcess_Exited;
            childProcess.Start();
        }

        private void ChildProcess_Exited(object sender, EventArgs e)
        {
            if (!isShuttingDown)
            {
                StartExe();
            }
        }

        private void KillChildProcess()
        {
            try
            {
                if (childProcess != null && !childProcess.HasExited)
                {
                    childProcess.Kill();
                    childProcess.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Process kill error: {ex.Message}");
            }
            finally
            {
                childProcess?.Dispose();
                childProcess = null;
            }
        }
    }
}
