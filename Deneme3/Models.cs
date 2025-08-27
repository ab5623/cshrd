using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UsingDLL
{
    public class Request
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Command { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class Response
    {
        public string RequestId { get; set; }
        public string Result { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

}