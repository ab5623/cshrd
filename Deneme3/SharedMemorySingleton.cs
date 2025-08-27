
//using System;
//using System.Collections.Concurrent;
//using System.IO.MemoryMappedFiles;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using UsingDLL.Models;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace UsingDLL
{
    internal sealed class EnhancedSharedMemorySingleton : IDisposable
    {
        private const string MemoryName = "MySharedMemory";
        private const string MutexName = "MySharedMutex";
        private const string NotificationEventName = "GlobalNotificationEvent";

        private readonly Mutex mutex;
        private readonly EventWaitHandle notificationEvent;
        private readonly ISharedMemory sharedMemory;

        // Pending requests - thread safe dictionary
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Response>> pendingRequests;

        // Cancellation for cleanup
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly Task responseListenerTask;

        private static readonly Lazy<EnhancedSharedMemorySingleton> _instance =
            new Lazy<EnhancedSharedMemorySingleton>(() => new EnhancedSharedMemorySingleton());

        public static EnhancedSharedMemorySingleton Instance => _instance.Value;

        private EnhancedSharedMemorySingleton()
        {
            this.sharedMemory = new SharedMemoryImplementation(1024 * 1024, MemoryName); // 1MB
            this.mutex = new Mutex(false, MutexName);
            this.notificationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, NotificationEventName);
            this.pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<Response>>();
            this.cancellationTokenSource = new CancellationTokenSource();

            // Background task to listen for responses
            this.responseListenerTask = Task.Run(ListenForResponses, cancellationTokenSource.Token);
        }

        public async Task<Response> SendRequestAsync(string command, TimeSpan? timeout = null)
        {
            var request = new Request { Command = command };
            var tcs = new TaskCompletionSource<Response>();
            var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);

            try
            {
                // Register pending request
                pendingRequests[request.Id] = tcs;

                // Serialize and write request
                var requestJson = JsonSerializer.Serialize(request);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);

                await Task.Run(() =>
                {
                    mutex.WaitOne();
                    try
                    {
                        // Write request length and data
                        sharedMemory.WriteInt(0, requestBytes.Length);
                        sharedMemory.Write(sizeof(int), requestBytes, 0, requestBytes.Length);

                        // Signal that request is ready
                        notificationEvent.Set();
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                });

                // Wait for response with timeout
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token))
                {
                    cts.CancelAfter(actualTimeout);

                    try
                    {
                        return await tcs.Task.WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return new Response
                        {
                            RequestId = request.Id,
                            IsSuccess = false,
                            ErrorMessage = "Request timeout"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.Id,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                // Clean up pending request
                pendingRequests.TryRemove(request.Id, out _);
            }
        }

        private async Task ListenForResponses()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for notification that response is ready
                    await Task.Run(() => notificationEvent.WaitOne(1000), cancellationTokenSource.Token);

                    if (cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    // Read response
                    var response = await Task.Run(() =>
                    {
                        mutex.WaitOne();
                        try
                        {
                            // Read response length
                            var length = sharedMemory.ReadInt(0);
                            if (length <= 0 || length > 1024 * 1024) // Sanity check
                                return null;

                            // Read response data
                            var responseBytes = new byte[length];
                            sharedMemory.Read(sizeof(int), responseBytes, 0, length);

                            var responseJson = Encoding.UTF8.GetString(responseBytes);
                            return JsonSerializer.Deserialize<Response>(responseJson);
                        }
                        catch
                        {
                            return null;
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
                    }, cancellationTokenSource.Token);

                    // Match response to pending request
                    if (response != null && pendingRequests.TryGetValue(response.RequestId, out var tcs))
                    {
                        tcs.SetResult(response);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log error (in real app, use proper logging)
                    Console.WriteLine($"Error in response listener: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            responseListenerTask?.Wait(TimeSpan.FromSeconds(5));

            cancellationTokenSource?.Dispose();
            mutex?.Dispose();
            notificationEvent?.Dispose();
            (sharedMemory as IDisposable)?.Dispose();
        }
    }
}