using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ProjectAqueduct.Functions
{
    public static class TwinUpdatedEgress
    {
        [Function("TwinUpdatedEgress")]
        public static async Task Run([EventHubTrigger("twin-history-raw", Connection = "EventHubConnection")] string[] messages, FunctionContext context)
        {
            var logger = context.GetLogger("TwinUpdatedEgress");
            logger.LogInformation($"First Event Hubs triggered message: {input[0]}");
        }
    }
}
