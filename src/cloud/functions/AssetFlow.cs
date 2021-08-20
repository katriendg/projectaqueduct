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
    public static class AssetFlow
    {
        [Function("AssetFlow")]
        public static async Task Run([EventHubTrigger("asset-flow", Connection = "EventHubConnection", ConsumerGroup = "function")] string[] messages, FunctionContext context)
        {
            var logger = context.GetLogger("AssetFlow");
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
                        // For actual value updates, also update the expected value
                        if (patch.path == "/FlowVolume")
                        {
                            await UpdateAssetTwin(client, assetTwinId, "ExpectedFlowVolume", patch.value, logger);
                        }
                        // For expected value updates, update the connected assets depending on the asset type
                        else if (patch.path == "/ExpectedFlowVolume")
                        {
                            double flowVolume = patch.value.GetDouble();
                            Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(assetTwinId);
                            BasicDigitalTwin assetTwin = twinResponse.Value;

                            if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Pipe;"))
                            {
                                if (assetTwin.Contents.TryGetValue("Diameter", out object diameterValue) &&
                                    assetTwin.Contents.TryGetValue("Length", out object lengthValue))
                                {
                                    double diameter = ((JsonElement)diameterValue).GetDouble(); // cm
                                    double length = ((JsonElement)lengthValue).GetDouble(); // m
                                    double flowArea = Math.PI * Math.Pow((diameter / 100 / 2), 2); // π * (diameter / 2)² in m²
                                    double assetVolume = 1000 * length * flowArea; // in liter
                                    double secondsToFlow = assetVolume / flowVolume; // seconds needed to flow through the pipe
                                    string flowingToTwinId = await GetFlowingToAsset(client, assetTwinId, logger);
                                    await UpdateAssetTwinAfter(adtInstance, twinMessage.modelId, flowingToTwinId, "ExpectedFlowVolume", (double)(flowVolume), secondsToFlow, logger);
                                    logger.LogInformation($"Expectation: Water takes {secondsToFlow} seconds to flow through pipe '{assetTwinId}' to '{flowingToTwinId}' at {flowVolume} liter/sec.");
                                }
                            }
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Junction;1"))
                            {
                                var assetCapacity = await GetFlowingToAssetsCapacity(client, assetTwinId, logger);
                                double totalCapacity = 0.0;
                                foreach(var flowingTo in assetCapacity) totalCapacity += flowingTo.Value;
                                foreach(var flowingTo in assetCapacity)
                                {
                                    double splitFlowVolume = flowVolume * flowingTo.Value / totalCapacity;
                                    await UpdateAssetTwin(client, flowingTo.Key, "FlowVolume", (double)(splitFlowVolume), logger);
                                    logger.LogInformation($"Expectation: Water flows from junction '{assetTwinId}' to '{flowingTo.Key}' at {splitFlowVolume} liter/sec.");
                                }
                            }
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Valve;1"))
                            {
                                int openStatus = 0;
                                if (assetTwin.Contents.TryGetValue("OpenStatus", out object openStatusValue))
                                {
                                    openStatus = ((JsonElement)openStatusValue).GetInt32();
                                }
                                if (openStatus == 1)
                                {
                                    string flowingToTwinId = await GetFlowingToAsset(client, assetTwinId, logger);
                                    await UpdateAssetTwin(client, flowingToTwinId, "ExpectedFlowVolume", (double)(flowVolume), logger);
                                    logger.LogInformation($"Expectation: Water flows from open valve '{assetTwinId}' to '{flowingToTwinId}' at {flowVolume} liter/sec.");
                                }
                                else {
                                    logger.LogInformation($"Expectation: Water stops flowing at closed valve '{assetTwinId}'.");
                                }
                            }
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Pump;1"))
                            {
                                string flowingToTwinId = await GetFlowingToAsset(client, assetTwinId, logger);
                                await UpdateAssetTwin(client, flowingToTwinId, "ExpectedFlowVolume", (double)(flowVolume), logger);
                                logger.LogInformation($"Expectation: Water flows from pump '{assetTwinId}' to '{flowingToTwinId}' at {flowVolume} liter/sec.");
                            }                            
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Reservoir;1"))
                            {
                            }                            
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Tap;1"))
                            {
                            }                            
                            break;
                        }
                    }
                }
            }
        }

        private static async Task<string> GetFlowingToAsset(DigitalTwinsClient client, string twinId, ILogger logger)
        {
            var assetCapacity = new Dictionary<string, double>();
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(twinId, "isFlowingTo");
            await foreach (BasicRelationship relationship in relationships)
            {
                return relationship.TargetId;
            }
            return null;
        }

        private static async Task<Dictionary<string, double>> GetFlowingToAssetsCapacity(DigitalTwinsClient client, string twinId, ILogger logger)
        {
            var assetCapacity = new Dictionary<string, double>();
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(twinId, "isFlowingTo");
            await foreach (BasicRelationship relationship in relationships)
            {
                string flowingToTwinId = relationship.TargetId;
                Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(flowingToTwinId);
                BasicDigitalTwin flowingToTwin = twinResponse.Value;
                if (flowingToTwin.Contents.TryGetValue("FlowCapacity", out object flowCapacityValue))
                {
                    double flowCapacity = ((JsonElement)flowCapacityValue).GetDouble();
                    assetCapacity.Add(flowingToTwinId, flowCapacity);
                }
            }
            return assetCapacity;
        }

        private static async Task UpdateAssetTwin(DigitalTwinsClient client, string twinId, string property, object value, ILogger logger)
        {
            Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(twinId);
            BasicDigitalTwin twin = twinResponse.Value;

            var updateTwinData = new JsonPatchDocument();
            if (twin.Contents.TryGetValue(property, out object propertyValue))
            {
                updateTwinData.AppendReplace("/" + property, value);
            }
            else
            {
                updateTwinData.AppendAdd("/" + property, value);
            }

            await client.UpdateDigitalTwinAsync(twinId, updateTwinData);
            logger.LogInformation($"Update {property} of asset '{twinId}' to {value}");
        }

        private static async Task UpdateAssetTwinAfter(string adtInstance, string modelId, string twinId, string property, object value, double seconds, ILogger logger)
        {
            var twinMessage = new TwinMessageExt();
            twinMessage.adtInstance = adtInstance;
            twinMessage.twinId = twinId;
            twinMessage.modelId = modelId;
            twinMessage.patch = new List<Patch>();
            twinMessage.patch.Add(new Patch() { value = value, path = "/" + property, op = "add"});

            // Since ServiceBusClient implements IAsyncDisposable we create it with "await using"
            string fullyQualifiedNamespace = Environment.GetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace");
            await using var sbClient = new ServiceBusClient(fullyQualifiedNamespace, new DefaultAzureCredential());

            // create the sender
            ServiceBusSender sender = sbClient.CreateSender("twin-updates");

            // create a message that we can send. UTF-8 encoding is used when providing a string.
            string messageText = JsonSerializer.Serialize(twinMessage);
            ServiceBusMessage message = new ServiceBusMessage(messageText);

            // Send the message scheduled to be delivered
            long seq = await sender.ScheduleMessageAsync(message, DateTimeOffset.Now.AddSeconds(seconds));
            logger.LogInformation($"Asset '{twinId}' property {property} will be updated to {value} after {seconds} seconds.");
        }
    }
}
