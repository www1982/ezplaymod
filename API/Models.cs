using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EZPlay.API.Models
{
    public class ApiRequest
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("payload")]
        public JObject Payload { get; set; }

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
    }
    public class ExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}