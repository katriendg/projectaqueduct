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
        public static async Task Run([EventHubTrigger("asset-updates", Connection = "EventHubConnection", ConsumerGroup = "function")] string[] messages, FunctionContext context)
        {
            var logger = context.GetLogger("AssetUpdated");
            var data = context.BindingContext.BindingData;
            var properties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(data["PropertiesArray"].ToString());
            var credentials = new DefaultAzureCredential();

            // Handle every EventHub message received
            for (int i = 0; i < messages.Length; i++)
            {
                // Create connection to the Azure Digital Twin instance
                string adtInstance = properties[i]["cloudEvents:source"];
                var client = new DigitalTwinsClient(new Uri("https://" + adtInstance), credentials);

                // Get and validate the asset twin from the message
                string assetTwinId = properties[i]["cloudEvents:subject"];
                logger.LogInformation($"Patch twin '{assetTwinId}' using: {messages[i]}");
                TwinMessage twinMessage = JsonSerializer.Deserialize<TwinMessage>(messages[i]);
                if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:"))
                {
                    // Get the updated value
                    foreach (var patch in twinMessage.patch)
                    {
                        if (patch.path == "/FlowCondition")
                        {
                            await UpdateAreaStatus(client, assetTwinId, patch.value.GetInt32(), logger);
                            break;
                        }
                    }
                }
            }
        }

        private static async Task UpdateAreaStatus(DigitalTwinsClient client, string assetTwinId, int flowCondition, ILogger logger)
        {
            var flowConditions = new List<int>();
            flowConditions.Add(flowCondition);

            // Get area of the asset
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(assetTwinId, "isLocatedIn");
            await foreach (BasicRelationship relationship in relationships)
            {
                string areaTwinId = relationship.TargetId;
                logger.LogInformation($"Twin '{assetTwinId}' is located in area '{areaTwinId}'");

                // Get all other assets of the same area with not normal condition
                string query = $"SELECT asset.$dtId, asset.FlowCondition FROM DIGITALTWINS asset JOIN area RELATED asset.isLocatedIn WHERE area.$dtId='{areaTwinId}' AND asset.$dtId!='{assetTwinId}' AND asset.FlowCondition!=4";
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
                Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(areaTwinId);
                BasicDigitalTwin areaTwin = twinResponse.Value;

                // Create patch document to update the area twin
                bool updateTwin = false;
                var updateTwinData = new JsonPatchDocument();
                if (areaTwin.Contents.TryGetValue("OperationalStatus", out object areaStatusValue))
                {
                    int areaStatus = ((JsonElement)areaStatusValue).GetInt32();
                    if (areaStatus != newAreaStatus)
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

                    await UpdateRegionStatus(client, areaTwinId, newAreaStatus, logger);
                }
            }
        }

        private static async Task UpdateRegionStatus(DigitalTwinsClient client, string areaTwinId, int operationalStatus, ILogger logger)
        {
            var operationalStatuses = new List<int>();
            operationalStatuses.Add(operationalStatus);

            // Get region of the area
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(areaTwinId, "isLocatedIn");
            await foreach (BasicRelationship relationship in relationships)
            {
                string regionTwinId = relationship.TargetId;
                logger.LogInformation($"Twin '{areaTwinId}' is located in region '{regionTwinId}'");

                // Get all other areas of the same region
                string query = $"SELECT area.$dtId, area.OperationalStatus FROM DIGITALTWINS area JOIN region RELATED area.isLocatedIn WHERE region.$dtId='{regionTwinId}' AND area.$dtId!='{areaTwinId}'";
                AsyncPageable<BasicDigitalTwin> areaTwins = client.QueryAsync<BasicDigitalTwin>(query);
                await foreach (BasicDigitalTwin areaTwin in areaTwins)
                {
                    if (areaTwin.Contents.TryGetValue("OperationalStatus", out object areaStatusValue))
                    {
                        int areaStatus = ((JsonElement)areaStatusValue).GetInt32();
                        logger.LogInformation($"Area '{areaTwin.Id}' in same region '{regionTwinId}' has operational status {areaStatus}");
                        operationalStatuses.Add(areaStatus);
                    }                
                }

                // Update the region status
                int newRegionStatus = GetRegionStatus(operationalStatuses);
                logger.LogInformation($"New status of region '{regionTwinId}' is {newRegionStatus}");

                // Get region twin
                Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(regionTwinId);
                BasicDigitalTwin regionTwin = twinResponse.Value;

                // Create patch document to update the region twin
                bool updateTwin = false;
                var updateTwinData = new JsonPatchDocument();
                if (regionTwin.Contents.TryGetValue("OperationalStatus", out object regionStatusValue))
                {
                    int regionStatus = ((JsonElement)regionStatusValue).GetInt32();
                    if (regionStatus != newRegionStatus)
                    {
                        updateTwin = true;
                        updateTwinData.AppendReplace("/OperationalStatus", newRegionStatus);
                    }
                }
                else
                {
                    updateTwin = true;
                    updateTwinData.AppendAdd("/OperationalStatus", newRegionStatus);
                }

                // Update the region twin
                if (updateTwin)
                {
                    await client.UpdateDigitalTwinAsync(regionTwinId, updateTwinData);
                    logger.LogInformation($"Patch twin '{regionTwinId}' using {updateTwinData.ToString()}");
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

        private static int GetRegionStatus(IList<int> statuses)
        {
            int newStatus = 0;
            foreach(int status in statuses)
            {
                if (status > newStatus)
                    newStatus = status;
            }
            return newStatus;
        }
    }
}
