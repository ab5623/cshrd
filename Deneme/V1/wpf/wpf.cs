using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Windows;
//using MyLibrary; // DLL referansı

namespace WpfApp
{
    public partial class MainWindow : Window
    {
        // Paylaşılan isimler (sabit olmalı)
        const string MemoryName = "MySharedMemory";
        const string MutexName = "MySharedMutex";
        const string RequestEventName = "RequestReady";
        const string ResponseEventName = "ResponseReady";

        private MemoryMappedFile mmf;
        private Mutex mutex;
        private EventWaitHandle requestEvent;
        private EventWaitHandle responseEvent;

        private const int FLAG_EMPTY = 0;
        private const int FLAG_REQUEST_READY = 1;
        private const int FLAG_RESPONSE_READY = 2;

        public MainWindow()
        {
            InitializeComponent();

            mmf = MemoryMappedFile.CreateOrOpen(MemoryName, 65536);
            mutex = new Mutex(false, MutexName);
            requestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RequestEventName);
            responseEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ResponseEventName);

        }

        async private void OnCalculateClick(object sender, RoutedEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            await Task.Delay(10);

            if (int.TryParse(InputA.Text, out int a) && int.TryParse(InputB.Text, out int b))
            {
                string request = $"ADDddd {a} {b}";

                //mutex.WaitOne();
                using (var accessor = mmf.CreateViewAccessor())
                {
                    while (accessor.ReadInt32(0) != FLAG_EMPTY) Thread.Sleep(0);

                    byte[] bytes = Encoding.UTF8.GetBytes(request);

                    accessor.Write(sizeof(int), bytes.Length);
                    accessor.WriteArray(2 * sizeof(int), bytes, 0, bytes.Length);
                    accessor.Write(0, FLAG_REQUEST_READY);

                }
                //mutex.ReleaseMutex();

                requestEvent.Set(); // hemen çağır
                await Task.Run(() => responseEvent.WaitOne()); // UI thread’i bloklamamak için

                string response;
                //mutex.WaitOne();
                using (var accessor = mmf.CreateViewAccessor())
                {
                    while (accessor.ReadInt32(0) != FLAG_RESPONSE_READY) Thread.Sleep(0);

                    int length = accessor.ReadInt32(sizeof(int));

                    byte[] buffer = new byte[length];
                    accessor.ReadArray(2 * sizeof(int), buffer, 0, length);
                    response = Encoding.UTF8.GetString(buffer);
                    accessor.Write(0, FLAG_EMPTY);

                }

                //mutex.ReleaseMutex();
                ResultText.Text = $"Result = {response}";

                sw.Stop(); // ölçüm bitir

                ResultText.Text = $"Result = {response} (Elapsed: {sw.ElapsedMilliseconds} ms)";
            }
            else
            {
                ResultText.Text = "Please enter valid integers!";
            }
        }
    }
}
