using EZPlay.Core.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace EZPlay.Core
{
    public class SecurityWhitelist : ISecurityWhitelist, IDisposable
    {
        private Dictionary<string, HashSet<string>> _allowedMembers = new Dictionary<string, HashSet<string>>();
        private HashSet<IPAddress> _allowedIPs = new HashSet<IPAddress>();
        private readonly object _lock = new object();
        private readonly ILogger _logger;
        private readonly string _configPath;
        private FileSystemWatcher _watcher;

        public SecurityWhitelist(ILogger logger, string configPath)
        {
            _logger = logger;
            _configPath = configPath;

            // Initialize with default loopback addresses
            _allowedIPs.Add(IPAddress.Loopback);
            _allowedIPs.Add(IPAddress.IPv6Loopback);

            LoadWhitelist();
            SetupWatcher();
        }

        private void SetupWatcher()
        {
            if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath)) return;

            var directory = Path.GetDirectoryName(_configPath);
            var fileName = Path.GetFileName(_configPath);

            _watcher = new FileSystemWatcher(directory, fileName);
            _watcher.Changed += OnWhitelistFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnWhitelistFileChanged(object sender, FileSystemEventArgs e)
        {
            _logger.Info("Whitelist file changed. Reloading...");
            ReloadWhitelist();
        }

        private void ReloadWhitelist()
        {
            // To prevent multiple rapid reloads, we can add a small delay or use a debounce mechanism.
            // For simplicity, we'll just reload directly here.
            Thread.Sleep(100); // Simple debounce
            LoadWhitelist();
        }

        private void LoadWhitelist()
        {
            if (!File.Exists(_configPath))
            {
                _logger.Warning($"Whitelist file not found at: {_configPath}. Reflection API will be restricted.");
                return;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                var rules = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

                var newAllowedMembers = new Dictionary<string, HashSet<string>>();
                var newAllowedIPs = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };

                foreach (var rule in rules)
                {
                    if (string.IsNullOrEmpty(rule.Key) || rule.Value == null) continue;

                    if (rule.Key == "*")
                    {
                        _logger.Error("Wildcard '*' as a type key is forbidden. This rule will be ignored.");
                        continue;
                    }

                    if (rule.Key == "ips")
                    {
                        foreach (var ipStr in rule.Value)
                        {
                            if (string.IsNullOrEmpty(ipStr)) continue;
                            if (IPAddress.TryParse(ipStr, out var ip))
                            {
                                newAllowedIPs.Add(ip);
                            }
                        }
                    }
                    else
                    {
                        var validMembers = new HashSet<string>();
                        foreach (var member in rule.Value)
                        {
                            if (!string.IsNullOrEmpty(member))
                            {
                                validMembers.Add(member);
                            }
                        }
                        if (validMembers.Count > 0)
                        {
                            newAllowedMembers[rule.Key] = validMembers;
                        }
                    }
                }

                lock (_lock)
                {
                    _allowedMembers = newAllowedMembers;
                    _allowedIPs = newAllowedIPs;
                }

                _logger.Info($"Whitelist loaded successfully from: {_configPath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load or process whitelist file: {ex.Message}");
            }
        }

        public bool IsAllowed(string typeName, string memberName)
        {
            lock (_lock)
            {
                if (_allowedMembers.TryGetValue(typeName, out var memberRules))
                {
                    return memberRules.Contains(memberName) || memberRules.Contains("*");
                }
            }
            return false;
        }

        public bool IsIPAllowed(IPAddress ipAddress)
        {
            lock (_lock)
            {
                return _allowedIPs.Contains(ipAddress);
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}