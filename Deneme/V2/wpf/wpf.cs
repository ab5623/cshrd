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

        const int SLOT_COUNT = 64;      // ring buffer slot sayısı
        const int SLOT_SIZE = 1024;     // her slot max byte
        const int FLAG_EMPTY = 0;
        const int FLAG_FULL = 1;

        const int HEAD_OFFSET = 0;
        const int TAIL_OFFSET = 4;
        const int SLOT_BASE_OFFSET = 8;

        private MemoryMappedFile mmf;
        private Mutex mutex;
        private EventWaitHandle requestEvent;
        private EventWaitHandle responseEvent;

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
                string request = $"ADD {a} {b}";

                mutex.WaitOne();
                using (var accessor = mmf.CreateViewAccessor())
                {
                    int tail = accessor.ReadInt32(TAIL_OFFSET);
                    long slotOffset = SLOT_BASE_OFFSET + (tail % SLOT_COUNT) * SLOT_SIZE;

                    // Wait until slot boşalır
                    while (accessor.ReadInt32(slotOffset) == FLAG_FULL)
                    {
                        mutex.ReleaseMutex();
                        await Task.Delay(1); // UI thread’i bloklamamak için
                        mutex.WaitOne();
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(request);
                    accessor.Write(slotOffset, FLAG_FULL);
                    accessor.Write(slotOffset + 4, bytes.Length);
                    accessor.WriteArray(slotOffset + 8, bytes, 0, bytes.Length);

                    tail = (tail + 1) % SLOT_COUNT;
                    accessor.Write(TAIL_OFFSET, tail);

                }
                mutex.ReleaseMutex();

                requestEvent.Set(); // hemen çağır
                string response = null;

                await Task.Run(() =>
                {
                    bool gotResponse = false;

                    while (!gotResponse)
                    {
                        responseEvent.WaitOne();
                        mutex.WaitOne();

                        using (var accessor = mmf.CreateViewAccessor())
                        {
                            int head = accessor.ReadInt32(HEAD_OFFSET);
                            long slotOffset = SLOT_BASE_OFFSET + (head % SLOT_COUNT) * SLOT_SIZE;

                            if (accessor.ReadInt32(slotOffset) == FLAG_FULL)
                            {
                                int length = accessor.ReadInt32(slotOffset + 4);
                                byte[] buffer = new byte[length];
                                accessor.ReadArray(slotOffset + 8, buffer, 0, length);
                                response = Encoding.UTF8.GetString(buffer);

                                accessor.Write(slotOffset, FLAG_EMPTY);
                                head = (head + 1) % SLOT_COUNT;
                                accessor.Write(HEAD_OFFSET, head);

                                gotResponse = true;
                            }
                        }
                        mutex.ReleaseMutex();

                    }

                });


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
