using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EZPlay.Logistics
{
    public enum PolicyType
    {
        CONSOLIDATE,
        DISTRIBUTE
    }

    public abstract class LogisticsPolicy : ILogisticsPolicy
    {
        public string policy_id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public PolicyType policy_type { get; set; }

        public Dictionary<string, object> parameters { get; set; }
        public abstract void Execute();
    }
}