using System;
using System.Linq;
using EZPlay.API.Exceptions;
using EZPlay.Utils;
using Newtonsoft.Json.Linq;

namespace EZPlay.API.Executors
{
    public static class PrintingPodExecutor
    {
        public static object HandlePrintingPodAction(int worldId, JObject payload)
        {
            var command = payload["command"]?.ToString();
            if (command == null)
            {
                throw new ApiException(400, "Missing 'command' in payload for PrintingPod action.");
            }

            var telepad = Components.Telepads.GetWorldItems(worldId).FirstOrDefault();
            if (telepad == null)
            {
                throw new ApiException(404, $"Printing pod not found in world {worldId}.");
            }

            switch (command)
            {
                case "get_printables":
                    return GetPrintables(telepad);
                case "select_printable":
                    var choice = payload["choice"]?.ToObject<int>();
                    if (choice == null)
                    {
                        throw new ApiException(400, "Missing 'choice' (integer) in payload for 'select_printable' command.");
                    }
                    return SelectPrintable(telepad, choice.Value);
                case "reject_all":
                    return RejectAll(telepad);
                default:
                    throw new ApiException(400, $"Unknown command '{command}' for PrintingPod action.");
            }
        }

        private static object GetPrintables(Telepad telepad)
        {
            if (telepad.GetComponent<Immigration>().ImmigrantsAvailable)
            {
                return new { printables = new object[0] };
            }

            var carePackagesField = typeof(Immigration).GetField("carePackages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var carePackages = (System.Collections.Generic.List<CarePackageInfo>)carePackagesField.GetValue(Immigration.Instance);
            var printables = carePackages.Select((info, index) => new
            {
                index = index,
                type = info.id,
                amount = info.quantity
            }).ToList();

            return new { printables = printables };
        }

        private static object SelectPrintable(Telepad telepad, int choice)
        {
            var immigration = telepad.GetComponent<Immigration>();
            if (!immigration.ImmigrantsAvailable)
            {
                throw new ApiException(409, "No printables available to select.");
            }

            var carePackagesField = typeof(Immigration).GetField("carePackages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var carePackages = (System.Collections.Generic.List<CarePackageInfo>)carePackagesField.GetValue(immigration);
            if (choice < 0 || choice >= carePackages.Count)
            {
                throw new ApiException(400, $"Invalid choice index {choice}. Must be between 0 and {carePackages.Count - 1}.");
            }

            telepad.OnAcceptDelivery(carePackages[choice]);
            return new { success = true, message = $"Selected printable at index {choice}." };
        }

        private static object RejectAll(Telepad telepad)
        {
            var immigration = telepad.GetComponent<Immigration>();
            if (!immigration.ImmigrantsAvailable)
            {
                throw new ApiException(409, "No printables available to reject.");
            }

            telepad.RejectAll();
            return new { success = true, message = "All printables rejected." };
        }
    }
}