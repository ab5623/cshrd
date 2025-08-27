using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using DLLDotnet;
using UsingDLL;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using UsingDLL;

class Program
{
    static void Main()
    {
        Console.WriteLine("Enhanced Shared Memory Server Started...");

        var sharedMemory = EnhancedSharedMemorySingleton.Instance;
        var notificationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "GlobalNotificationEvent");

        while (true)
        {
            try
            {
                // Wait for request notification
                notificationEvent.WaitOne();

                // Process request
                ProcessRequest(sharedMemory, notificationEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
            }
        }
    }

    private static void ProcessRequest(EnhancedSharedMemorySingleton sharedMemory, EventWaitHandle notificationEvent)
    {
        try
        {
            // Read request (this should be done through proper interface, simplified here)
            var mutex = new Mutex(false, "MySharedMutex");
            var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting("MySharedMemory");

            Request request;
            using (var accessor = mmf.CreateViewAccessor())
            {
                mutex.WaitOne();
                try
                {
                    var length = accessor.ReadInt32(0);
                    var requestBytes = new byte[length];
                    accessor.ReadArray(sizeof(int), requestBytes, 0, length);
                    var requestJson = Encoding.UTF8.GetString(requestBytes);
                    request = JsonSerializer.Deserialize<Request>(requestJson);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            // Process command
            var response = ProcessCommand(request);

            // Write response
            var responseJson = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);

            using (var accessor = mmf.CreateViewAccessor())
            {
                mutex.WaitOne();
                try
                {
                    accessor.Write(0, responseBytes.Length);
                    accessor.WriteArray(sizeof(int), responseBytes, 0, responseBytes.Length);
                    notificationEvent.Set(); // Notify response is ready
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ProcessRequest: {ex.Message}");
        }
    }

    private static Response ProcessCommand(Request request)
    {
        try
        {
            var parts = request.Command.Split(' ');
            if (parts.Length >= 3 && parts[0] == "ADD")
            {
                if (int.TryParse(parts[1], out int a) && int.TryParse(parts[2], out int b))
                {
                    var result = a + b;
                    return new Response
                    {
                        RequestId = request.Id,
                        Result = result.ToString(),
                        IsSuccess = true
                    };
                }
            }

            return new Response
            {
                RequestId = request.Id,
                Result = "Invalid command format",
                IsSuccess = false
            };
        }
        catch (Exception ex)
        {
            return new Response
            {
                RequestId = request.Id,
                ErrorMessage = ex.Message,
                IsSuccess = false
            };
        }
    }
}