using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.DigitalTwins.Core;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProjectAqueduct.Functions.Model;

namespace ProjectAqueduct.Functions
{
    public static class DelayedAssetUpdated
    {
        [Function("DelayedAssetUpdated")]
        public static async Task Run([ServiceBusTrigger("twin-updates", Connection = "ServiceBusConnection")] string message, FunctionContext context)
        {
            var logger = context.GetLogger("DelayedAssetUpdated");

            var twinMessage = JsonSerializer.Deserialize<TwinMessageExt>(message);

            // Create connection to the Azure Digital Twin instance
            string adtInstance = twinMessage.adtInstance;
            var credentials = new DefaultAzureCredential();
            var client = new DigitalTwinsClient(new Uri("https://" + adtInstance), credentials);

            // Create patch document to update the twin
            var updateTwinData = new JsonPatchDocument();
            foreach(var patch in twinMessage.patch)
            {
                if (patch.op == "add")
                    updateTwinData.AppendAdd(patch.path, patch.value);
                else if (patch.op == "replace")
                    updateTwinData.AppendReplace(patch.path, patch.value);
            }

            // Update the twin
            await client.UpdateDigitalTwinAsync(attachedTwinId, updateTwinData);
            logger.LogInformation($"Delayed update asset '{twinMessage.twinId}' with {twinMessage.ToString()}.");
        }
    }
}
