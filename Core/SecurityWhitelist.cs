using EZPlay.Core.Interfaces;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace EZPlay.Core
{
    public class SecurityWhitelist : ISecurityWhitelist
    {
        private readonly Dictionary<string, HashSet<string>> _allowedMembers = new Dictionary<string, HashSet<string>>();
        private readonly List<IPAddress> _allowedIPs = new List<IPAddress>();
        private readonly ILogger _logger;

        public SecurityWhitelist(ILogger logger, string configPath)
        {
            _logger = logger;
            LoadWhitelist(configPath);
        }

        private void LoadWhitelist(string configPath)
        {
            if (!File.Exists(configPath))
            {
                _logger.Warning($"Whitelist file not found at: {configPath}. Reflection API will be restricted.");
                return;
            }

            var json = File.ReadAllText(configPath);
            var rules = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

            foreach (var rule in rules)
            {
                if (rule.Key == "ips")
                {
                    foreach (var ipStr in rule.Value)
                    {
                        if (IPAddress.TryParse(ipStr, out var ip))
                        {
                            _allowedIPs.Add(ip);
                        }
                    }
                }
                else
                {
                    _allowedMembers[rule.Key] = new HashSet<string>(rule.Value);
                }
            }
            _logger.Info($"Whitelist loaded successfully from: {configPath}");
        }

        public bool IsAllowed(string typeName, string memberName)
        {
            if (_allowedMembers.TryGetValue("*", out var globalRules))
            {
                if (globalRules.Contains("*")) return true;
            }

            if (_allowedMembers.TryGetValue(typeName, out var memberRules))
            {
                return memberRules.Contains(memberName) || memberRules.Contains("*");
            }

            return false;
        }

        public bool IsIPAllowed(IPAddress ipAddress)
        {
            return _allowedIPs.Contains(ipAddress);
        }
    }
}