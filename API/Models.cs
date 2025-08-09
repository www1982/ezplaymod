using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EZPlay.API.Models
{
    public class ApiRequest
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }
    }

    public class ApiResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("payload")]
        public object Payload { get; set; }

        [JsonProperty("requestId", NullValueHandling = NullValueHandling.Ignore)]
        public string RequestId { get; set; }

        public static ApiResponse Error(string action, string message, string stackTrace, string requestId)
        {
            return new ApiResponse
            {
                Type = action + ".Error",
                Status = "error",
                Payload = new { message, stackTrace },
                RequestId = requestId
            };
        }

        public static ApiResponse ParseError(string message, string requestId)
        {
            return new ApiResponse
            {
                Type = "Request.ParseError",
                Status = "error",
                Payload = new { message = "Failed to parse incoming request.", error = message },
                RequestId = requestId
            };
        }
    }
    public class ExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}