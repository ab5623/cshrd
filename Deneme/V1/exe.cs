// ConsoleApp/Program.cs
using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using DLLDotnet; // DLL reference

class Program
{
    // Paylaşılan isimler (sabit olmalı)
    const string MemoryName = "MySharedMemory";
    const string MutexName = "MySharedMutex";
    const string RequestEventName = "RequestReady";
    const string ResponseEventName = "ResponseReady";

    private const int FLAG_EMPTY = 0;
    private const int FLAG_REQUEST_READY = 1;
    private const int FLAG_RESPONSE_READY = 2;

    static void Main()
    {
        var math = new DLLDotnet.Class1();

        using var mmf = MemoryMappedFile.CreateOrOpen(MemoryName, 65536);
        using var mutex = new Mutex(false, MutexName);
        using var requestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RequestEventName);
        using var responseEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ResponseEventName);

        while (true)
        {
            requestEvent.WaitOne();

            string request;
            //mutex.WaitOne();

            using (var accessor = mmf.CreateViewAccessor())
            {
                while (accessor.ReadInt32(0) != FLAG_REQUEST_READY) Thread.Sleep(0);

                int length = accessor.ReadInt32(sizeof(int));
                byte[] buffer = new byte[length];
                accessor.ReadArray(2 * sizeof(int), buffer, 0, length);
                request = Encoding.UTF8.GetString(buffer);
            }
            //mutex.ReleaseMutex();

            string[] parts = request.Split(" ");
            string command = parts[0];
            int a = int.Parse(parts[1]);
            int b = int.Parse(parts[2]);

            int result = command switch
            {
                "ADD" => math.add(a, 2 * a),
                "Add2" => math.add(a * 2, b * 2),
                _ => -1
            };

            string response;
            if (result == -1)
            {
                response = "909309309430493094039403940934348398493849384938498394830493849384939093093094304930940394039409343483984938493849384983948304938493849390930930943049309403940394093434839849384938493849839483049384938493\n";
            }
            else
            {
                response = result.ToString();
            }

            //mutex.WaitOne();

            using (var accessor = mmf.CreateViewAccessor())
            {


                byte[] bytes = Encoding.UTF8.GetBytes(response);
                int length = bytes.Length;

                accessor.Write(sizeof(int), length);
                accessor.WriteArray(2 * sizeof(int), bytes, 0, bytes.Length);
                accessor.Write(0, FLAG_RESPONSE_READY);

            }
            //mutex.ReleaseMutex();

            // wpf ye haber verelim
            responseEvent.Set();

        }
    }
}
