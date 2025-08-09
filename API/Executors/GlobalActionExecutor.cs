using System;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class GlobalActionExecutor
    {
        private static readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("GlobalActionExecutor");
        private class GlobalActionRequest
        {
            public string ActionName { get; set; }
        }

        public static ExecutionResult Execute(string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                throw new ApiException(400, "Payload cannot be null or empty.");
            }

            GlobalActionRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<GlobalActionRequest>(jsonPayload);
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }

            if (request == null || string.IsNullOrEmpty(request.ActionName))
            {
                throw new ApiException(400, "Invalid payload. It must contain an 'ActionName' field.");
            }

            switch (request.ActionName.ToLower())
            {
                case "take_screenshot":
                    string activeSaveFilePath = SaveLoader.GetActiveSaveFilePath();
                    if (string.IsNullOrEmpty(activeSaveFilePath))
                    {
                        throw new ApiException(500, "Cannot take screenshot: No active save file path found.");
                    }
                    string dir = Path.Combine(Path.GetDirectoryName(activeSaveFilePath), "screenshots");
                    Directory.CreateDirectory(dir);
                    string fileName = $"{Path.GetFileNameWithoutExtension(activeSaveFilePath)}_{GameClock.Instance.GetCycle()}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                    string filePath = Path.Combine(dir, fileName);
                    ScreenCapture.CaptureScreenshot(filePath);
                    return new ExecutionResult { Success = true, Message = $"Screenshot saved to {filePath}", Data = filePath };
                case "pause_game":
                    if (SpeedControlScreen.Instance != null)
                    {
                        SpeedControlScreen.Instance.Pause();
                        return new ExecutionResult { Success = true, Message = "Game paused." };
                    }
                    throw new ApiException(500, "SpeedControlScreen instance not found.");
                case "unpause_game":
                    if (SpeedControlScreen.Instance != null)
                    {
                        SpeedControlScreen.Instance.Unpause();
                        return new ExecutionResult { Success = true, Message = "Game unpaused." };
                    }
                    throw new ApiException(500, "SpeedControlScreen instance not found.");
                default:
                    throw new ApiException(400, $"Unknown global action: {request.ActionName}");
            }
        }
    }
}