using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp
{
    public partial class MainWindow : Window
    {
        
        EnhancedSharedMemorySingleton sharedMemory;

        public MainWindow()
        {
            InitializeComponent();

           sharedMemory = EnhancedSharedMemorySingleton.Instance; 
        }

        private async void OnCalculateClick(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Disable button to prevent multiple clicks
                ((Button)sender).IsEnabled = false;

                if (int.TryParse(InputA.Text, out int a) && int.TryParse(InputB.Text, out int b))
                {
                    string command = $"ADD {a} {b}";

                    // Send async request with timeout
                    var response = await EnhancedSharedMemorySingleton.Instance
                        .SendRequestAsync(command, TimeSpan.FromSeconds(10));

                    sw.Stop();

                    if (response.IsSuccess)
                    {
                        ResultText.Text = $"Result = {response.Result} (Elapsed: {sw.ElapsedMilliseconds} ms)";
                    }
                    else
                    {
                        ResultText.Text = $"Error: {response.ErrorMessage}";
                    }
                }
                else
                {
                    ResultText.Text = "Please enter valid integers!";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                // Re-enable button
                ((Button)sender).IsEnabled = true;
            }
        }
    }
}
