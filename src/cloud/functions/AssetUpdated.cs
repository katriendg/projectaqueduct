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
    public static class AssetUpdated
    {
        [Function("AssetUpdated")]
        public static async Task Run([EventHubTrigger("asset-updates", Connection = "EventHubConnection")] string[] messages, FunctionContext context)
        {
            var logger = context.GetLogger("AssetUpdated");
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
                if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:"))
                {
                    // Get the updated value
                    foreach (var patch in twinMessage.patch)
                    {
                        if (patch.path == "/FlowCondition")
                        {
                            await UpdateAreaStatus(client, twinId, patch.value.GetInt32(), logger);
                            break;
                        }
                    }
                }
            }
        }

        private static async Task UpdateAreaStatus(DigitalTwinsClient client, string twinId, int flowCondition, ILogger logger)
        {
            // Get area of the asset
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(twinId, "isLocatedIn");
            await foreach (BasicRelationship relationship in relationships)
            {
                string areaTwinId = relationship.TargetId;
                logger.LogInformation($"Twin '{twinId}' is located in area '{areaTwinId}'");
                BasicDigitalTwin areaTwin;
                Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(areaTwinId);
                areaTwin = twinResponse.Value;

            }
        }
    }
}
