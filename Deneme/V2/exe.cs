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

    const int SLOT_COUNT = 64;
    const int SLOT_SIZE = 1024;
    const int FLAG_EMPTY = 0;
    const int FLAG_FULL = 1;

    // Shared memory başında HEAD ve TAIL için offset
    const int HEAD_OFFSET = 0;
    const int TAIL_OFFSET = 4;
    const int SLOT_BASE_OFFSET = 8;

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
            int head = 0, tail = 0;

            string request = null;
            mutex.WaitOne();

            using (var accessor = mmf.CreateViewAccessor())
            {
                head = accessor.ReadInt32(HEAD_OFFSET);
                tail = accessor.ReadInt32(TAIL_OFFSET);

                long slotOffset = SLOT_BASE_OFFSET + (head % SLOT_COUNT) * SLOT_SIZE;

                if (accessor.ReadInt32(slotOffset) == FLAG_FULL)
                {
                    int len = accessor.ReadInt32(slotOffset + 4);
                    byte[] buffer = new byte[len];
                    accessor.ReadArray(slotOffset + 8, buffer, 0, len);
                    request = Encoding.UTF8.GetString(buffer);

                    accessor.Write(slotOffset, FLAG_EMPTY); // slot boşalt
                    head = (head + 1) % SLOT_COUNT;
                    accessor.Write(HEAD_OFFSET, head);      // shared HEAD güncelle
                }
            }
            mutex.ReleaseMutex();

            if (request == null) continue;

            string[] parts = request.Split(" ");
            string command = parts[0];
            int a = int.Parse(parts[1]);
            int b = int.Parse(parts[2]);

            int result = command switch
            {
                "ADD" => math.add(a, b),
                "Add2" => math.add(a * 2, b * 2),
                _ => -1
            };

            string response;
            if (result == -1)
            {
                response = "9093093094304930940394039409398993\n";
            }
            else
            {
                response = result.ToString();
            }

            mutex.WaitOne();

            using (var accessor = mmf.CreateViewAccessor())
            {
                int responseTail = accessor.ReadInt32(TAIL_OFFSET);
                long slotOffset = SLOT_BASE_OFFSET + (responseTail % SLOT_COUNT) * SLOT_SIZE;

                if (accessor.ReadInt32(slotOffset) == FLAG_EMPTY)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(response);
                    accessor.Write(slotOffset, FLAG_FULL);
                    accessor.Write(slotOffset + 4, bytes.Length);
                    accessor.WriteArray(slotOffset + 8, bytes, 0, bytes.Length);

                    responseTail = (responseTail + 1) % SLOT_COUNT;
                    accessor.Write(TAIL_OFFSET, responseTail); // shared TAIL güncelle
                }

            }
            mutex.ReleaseMutex();

            // wpf ye haber verelim
            responseEvent.Set();

        }
    }
}
