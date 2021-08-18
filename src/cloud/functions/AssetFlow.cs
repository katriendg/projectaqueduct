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
        public static async Task Run([EventHubTrigger("asset-flow", Connection = "EventHubConnection")] string[] messages, FunctionContext context)
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
                        if (patch.path == "/FlowVolume")
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
                                    logger.LogInformation($"Pipe '{assetTwinId}' found with diameter {diameter}cm and length {length}m");

                                    double flowArea = Math.PI * Math.Pow((diameter / 100 / 2), 2); // π * (diameter / 2)² in m²
                                    double assetVolume = 1000 * length * flowArea; // in liter
                                    double secondsToFlow = assetVolume / flowVolume; // seconds needed to flow through the pipe
                                    logger.LogInformation($"Water takes {secondsToFlow} seconds to flow through pipe '{assetTwinId}' at {flowVolume} liter/sec.");

                                    string flowingToTwinId = await GetFlowingToAsset(client, assetTwinId, logger);
                                    await UpdateTwinAfter(adtInstance, twinMessage.modelId, flowingToTwinId, "FlowVolume", (double)(flowVolume), secondsToFlow, logger);
                                }
                            }
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Reservoir;1"))
                            {
                                if (assetTwin.Contents.TryGetValue("Volume", out object volumeValue))
                                {
                                }
                            }
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Junction;1"))
                            {
                                var assetCapacity = await GetFlowingToAssetsCapacity(client, assetTwinId, logger);
                                double totalCapacity = 0.0;
                                foreach(var flowingTo in assetCapacity) totalCapacity += flowingTo.Value;
                                foreach(var flowingTo in assetCapacity)
                                {
                                    await UpdateTwin(client, flowingTo.Key, "FlowVolume", (double)(flowVolume * flowingTo.Value / totalCapacity), logger);
                                }
                            }
                            else if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:asset:Pump;1"))
                            {
                                string flowingToTwinId = await GetFlowingToAsset(client, assetTwinId, logger);
                                await UpdateTwin(client, flowingToTwinId, "FlowVolume", (double)(flowVolume), logger);
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

        private static async Task UpdateTwin(DigitalTwinsClient client, string twinId, string property, object value, ILogger logger)
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

        private static async Task UpdateTwinAfter(string adtInstance, string modelId, string twinId, string property, object value, double seconds, ILogger logger)
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
