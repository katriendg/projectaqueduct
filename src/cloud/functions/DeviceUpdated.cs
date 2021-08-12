using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.DigitalTwins.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProjectAqueduct.Functions.Model;

namespace ProjectAqueduct.Functions
{
    public static class DeviceUpdated
    {
        [Function("DeviceUpdated")]
        public static async Task Run([EventHubTrigger("device-updates", Connection = "EventHubConnection")] string[] messages, FunctionContext context)
        {
            var logger = context.GetLogger("DeviceUpdated");
            var data = context.BindingContext.BindingData;
            var properties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(data["PropertiesArray"].ToString());
            var credentials = new DefaultAzureCredential();

            // Handle every EventHub message received
            for (int i = 0; i < messages.Length; i++)
            {
                // Create connection to the Azure Digital Twin instance
                string adtInstanceUrl = "https://" + properties[i]["cloudEvents:source"];
                var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), credentials);

                // Get and validate the twin from the message
                string twinId = properties[i]["cloudEvents:subject"];
                logger.LogInformation($"Patch twin '{twinId}' using: {messages[i]}");
                TwinMessage twinMessage = JsonSerializer.Deserialize<TwinMessage>(messages[i]);
                if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:device:"))
                {
                    // Get the updated value
                    foreach (var patch in twinMessage.patch)
                    {
                        if (patch.path == "/VolumeFlow")
                        {
                            await UpdateAttachedAsset(client, twinId, patch.value.GetDouble(), logger);
                            break;
                        }
                    }
                }
            }
        }

        private static async Task UpdateAttachedAsset(DigitalTwinsClient client, string twinId, double flowVolume, ILogger logger)
        {
            // Get attached asset
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(twinId, "isAttachedTo");
            await foreach (BasicRelationship relationship in relationships)
            {
                string attachedTwinId = relationship.TargetId;
                logger.LogInformation($"Twin '{twinId}' is attached to twin '{attachedTwinId}'");
                BasicDigitalTwin attachedTwin;
                Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(attachedTwinId);
                attachedTwin = twinResponse.Value;

                // Create patch document to update the asset twin
                var updateTwinData = new JsonPatchDocument();
                if (attachedTwin.Contents.TryGetValue("FlowVolume", out object flowVolumeValue))
                {
                    updateTwinData.AppendReplace("/FlowVolume", flowVolume);
                }
                else
                {
                    updateTwinData.AppendAdd("/FlowVolume", flowVolume);
                }

                // Update asset flow quality if capacity and margin are set
                if (attachedTwin.Contents.TryGetValue("FlowCapacity", out object flowCapacityValue) &&
                    attachedTwin.Contents.TryGetValue("FlowMargin", out object flowMarginValue))
                {
                    double flowCapacity = ((JsonElement)flowCapacityValue).GetDouble();
                    double flowMargin = ((JsonElement)flowMarginValue).GetDouble();
                    int quality = (Math.Abs(flowCapacity - flowVolume) <= flowMargin) ? 1 /* ok */ : 2 /* nok */;
                    if (attachedTwin.Contents.TryGetValue("FlowQuality", out object flowQualityValue))
                    {
                        updateTwinData.AppendReplace("/FlowQuality", quality);
                    }
                    else
                    {
                        updateTwinData.AppendAdd("/FlowQuality", quality);
                    }
                }

                // Update the asset twin
                await client.UpdateDigitalTwinAsync(attachedTwinId, updateTwinData);
                logger.LogInformation($"Patch twin '{attachedTwinId}' using {updateTwinData.ToString()}");
            }
        }
    }
}
