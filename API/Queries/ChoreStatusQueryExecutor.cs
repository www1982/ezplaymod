using System;
using System.Linq;
using Newtonsoft.Json;

namespace EZPlay.API.Queries
{
    public static class ChoreStatusQueryExecutor
    {
        private class ChoreStatusRequest
        {
            public string DuplicantName { get; set; }
        }

        public static object Execute(int worldId, string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<ChoreStatusRequest>(jsonPayload);
            if (request == null || string.IsNullOrEmpty(request.DuplicantName))
            {
                throw new ArgumentException("Invalid payload for chore status query.");
            }

            var minion = Components.MinionIdentities.GetWorldItems(worldId).FirstOrDefault(m => m.name.Replace("(Clone)", "") == request.DuplicantName);
            if (minion == null)
            {
                return new { error = $"Duplicant '{request.DuplicantName}' not found in world {worldId}." };
            }

            var choreConsumer = minion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null || choreConsumer.choreDriver == null)
            {
                return new { error = "ChoreConsumer or ChoreDriver component not found." };
            }

            var currentChore = choreConsumer.choreDriver.GetCurrentChore();
            return new
            {
                DuplicantName = request.DuplicantName,
                CurrentChore = currentChore != null ? currentChore.GetType().Name : "None",
                IsIdle = currentChore == null
            };
        }
    }
}