using System;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class GlobalActionExecutor
    {
        private class GlobalActionRequest
        {
            public string ActionName { get; set; }
        }

        public static object Execute(string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<GlobalActionRequest>(jsonPayload);
            if (request == null || string.IsNullOrEmpty(request.ActionName))
            {
                throw new ArgumentException("Invalid payload for global action.");
            }

            switch (request.ActionName.ToLower())
            {
                case "take_screenshot":
                    // Correct way to take a screenshot via the pause screen
                    string activeSaveFilePath = SaveLoader.GetActiveSaveFilePath();
                    string text = Path.Combine(Path.GetDirectoryName(activeSaveFilePath), "screenshot");
                    string fileName = Path.GetFileName(activeSaveFilePath);
                    Directory.CreateDirectory(text);
                    string text2 = string.Concat(new string[]
                    {
                        Path.GetFileNameWithoutExtension(fileName),
                        "_",
                        GameClock.Instance.GetCycle().ToString(),
                        "_",
                        System.DateTime.Now.ToString("yyyy-MM-dd_HH\\hmm\\mss\\s"),
                        ".png"
                    });
                    string filename = Path.Combine(text, text2);
                    ScreenCapture.CaptureScreenshot(filename);
                    return new { success = true, message = "Screenshot taken." };
                case "pause_game":
                    SpeedControlScreen.Instance.Pause();
                    return new { success = true, message = "Game paused." };
                case "unpause_game":
                    SpeedControlScreen.Instance.Unpause();
                    return new { success = true, message = "Game unpaused." };
                default:
                    throw new ArgumentException($"Unknown global action: {request.ActionName}");
            }
        }
    }
}