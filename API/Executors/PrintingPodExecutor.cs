using System;
using System.Linq;
using EZPlay.API.Exceptions;
using EZPlay.Utils;
using Newtonsoft.Json.Linq;

namespace EZPlay.API.Executors
{
    public static class PrintingPodExecutor
    {
        public static object HandlePrintingPodAction(JObject payload)
        {
            var command = payload["command"]?.ToString();
            if (command == null)
            {
                throw new ApiException(400, "Missing 'command' in payload for PrintingPod action.");
            }

            switch (command)
            {
                case "get_printables":
                    return GetPrintables();
                case "select_printable":
                    var choice = payload["choice"]?.ToObject<int>();
                    if (choice == null)
                    {
                        throw new ApiException(400, "Missing 'choice' (integer) in payload for 'select_printable' command.");
                    }
                    return SelectPrintable(choice.Value);
                case "reject_all":
                    return RejectAll();
                default:
                    throw new ApiException(400, $"Unknown command '{command}' for PrintingPod action.");
            }
        }

        private static object GetPrintables()
        {
            if (!Immigration.Instance.ImmigrantsAvailable)
            {
                return new { printables = new object[0] };
            }

            var carePackages = ImmigrationHelper.GetCarePackages();
            var printables = carePackages.Select((info, index) => new
            {
                index = index,
                type = info.id,
                amount = info.quantity
            }).ToList();

            return new { printables = printables };
        }

        private static object SelectPrintable(int choice)
        {
            if (!Immigration.Instance.ImmigrantsAvailable)
            {
                throw new ApiException(409, "No printables available to select.");
            }

            var carePackages = ImmigrationHelper.GetCarePackages();
            if (choice < 0 || choice >= carePackages.Count)
            {
                throw new ApiException(400, $"Invalid choice index {choice}. Must be between 0 and {carePackages.Count - 1}.");
            }

            ImmigrationHelper.SelectCarePackage(carePackages[choice]);
            return new { success = true, message = $"Selected printable at index {choice}." };
        }

        private static object RejectAll()
        {
            if (!Immigration.Instance.ImmigrantsAvailable)
            {
                throw new ApiException(409, "No printables available to reject.");
            }

            ImmigrationHelper.RejectAllCarePackages();
            return new { success = true, message = "All printables rejected." };
        }
    }
}