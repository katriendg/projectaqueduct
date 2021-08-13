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
            var flowConditions = new List<int>();
            flowConditions.Add(flowCondition);

            // Get area of the asset
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(twinId, "isLocatedIn");
            await foreach (BasicRelationship relationship in relationships)
            {
                string areaTwinId = relationship.TargetId;
                logger.LogInformation($"Twin '{twinId}' is located in area '{areaTwinId}'");

                // Get all assets of the same area with not normal condition
                string query = $"SELECT asset.$dtId, asset.FlowCondition FROM DIGITALTWINS asset JOIN area RELATED asset.isLocatedIn WHERE area.$dtId='{areaTwinId}' AND asset.$dtId!='{twinId}' AND asset.FlowCondition!=4";
                AsyncPageable<BasicDigitalTwin> assetTwins = client.QueryAsync<BasicDigitalTwin>(query);
                await foreach (BasicDigitalTwin assetTwin in assetTwins)
                {
                    if (assetTwin.Contents.TryGetValue("FlowCondition", out object flowConditionValue))
                    {
                        int assetFlowCondition = ((JsonElement)flowConditionValue).GetInt32();
                        logger.LogInformation($"Asset '{assetTwin.Id}' in same area '{areaTwinId}' has condition {assetFlowCondition}");
                        flowConditions.Add(assetFlowCondition);
                    }                
                }

                // Update the area status
                int newAreaStatus = GetAreaStatus(flowConditions);
                logger.LogInformation($"New status of area '{areaTwinId}' is {newAreaStatus}");

                // Get area twin
                BasicDigitalTwin areaTwin;
                Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(areaTwinId);
                areaTwin = twinResponse.Value;

                // Create patch document to update the area twin
                bool updateTwin = false;
                var updateTwinData = new JsonPatchDocument();
                if (areaTwin.Contents.TryGetValue("OperationalStatus", out object operationalStatusValue))
                {
                    int operationalStatus = ((JsonElement)operationalStatusValue).GetInt32();
                    if (operationalStatus != newAreaStatus)
                    {
                        updateTwin = true;
                        updateTwinData.AppendReplace("/OperationalStatus", newAreaStatus);
                    }
                }
                else
                {
                    updateTwin = true;
                    updateTwinData.AppendAdd("/OperationalStatus", newAreaStatus);
                }

                // Update the area twin
                if (updateTwin)
                {
                    await client.UpdateDigitalTwinAsync(areaTwinId, updateTwinData);
                    logger.LogInformation($"Patch twin '{areaTwinId}' using {updateTwinData.ToString()}");
                }
            }
        }

        private static int GetAreaStatus(IList<int> conditions)
        {
            int newStatus = 0;
            foreach(int condition in conditions)
            {
                int conditionStatus = condition switch
                {
                    1 /* under */ => 3 /* alarm */,
                    2 /* no */ => 2 /* warning */,
                    3 /* low */ => 2 /* warning */,
                    4 /* normal */ => 1 /* ok */,
                    5 /* high */ => 2 /* warning */,
                    6 /* max */ => 2 /* warning */,
                    7 /* over */ => 3 /* alarm */,
                    _ => 0
                };
                if (conditionStatus > newStatus)
                    newStatus = conditionStatus;
            }
            return newStatus;
        }
    }
}
