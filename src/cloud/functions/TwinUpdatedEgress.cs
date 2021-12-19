using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure;
using ProjectAqueduct.Functions.Model;
using Azure.Messaging.EventHubs;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ProjectAqueduct.Functions
{
    public static class TwinUpdatedEgress
    {
        [Function("TwinUpdatedEgress")]
        public static MyEventHubOutput Run(
            [EventHubTrigger("twin-history", Connection = "EventHubConnection", ConsumerGroup = "function")] string[] messages, 
            FunctionContext context)
        {
            var logger = context.GetLogger("TwinUpdatedEgress");
            var data = context.BindingContext.BindingData;
            var properties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(data["PropertiesArray"].ToString());
            List<TwinHistory> outputTwins = new List<TwinHistory>();
            TwinHistory history = null;

            logger.LogInformation($"TwinUpdatedEgress - started trigger");

            // Handle every EventHub message received
            for (int i = 0; i < messages.Length; i++)
            {
                string twinId = properties[i]["cloudEvents:subject"];
                DateTime twinTime = DateTime.Parse(properties[i]["cloudEvents:time"]);
                logger.LogInformation($"Egress twin '{twinId}' using: {messages[i]}");
                TwinMessage twinMessage = JsonSerializer.Deserialize<TwinMessage>(messages[i]);

                logger.LogInformation($"TwinUpdatedEgress - full message {messages[i]}");

                //if this twin is based on device model - use the SensorTimestamp if available
                if (twinMessage.modelId.StartsWith("dtmi:sample:aqueduct:device:"))
                {
                    try{
                        var tsValue = twinMessage.patch.FirstOrDefault(p => p.path == "/SensorTimestamp");
                        if(tsValue!=null)
                            twinTime = ((JsonElement)tsValue.value).GetDateTime();
                    }
                    catch(System.Exception e){
                        Console.WriteLine($"Expected exception: {e.Message}");
                    }
                }
                
                history = new TwinHistory{
                    modelId = twinMessage.modelId,
                    twinId = twinId,
                    twinTime = twinTime,
                    patch = new List<Patch>()
                };
                foreach (var patch in twinMessage.patch)
                {
                    //replace first "/" in path
                    patch.path = patch.path.Substring(1);
                    history.patch.Add(patch);
                } 
                outputTwins.Add(history);
                logger.LogInformation($"TwinUpdatedEgress - Added {history.twinId} item to collection of EH output");

            }
            
            return new MyEventHubOutput()
            {
                outputEvents = outputTwins.ToArray<TwinHistory>()
            };          
            
        }

        public class MyEventHubOutput
        {
            [EventHubOutput("twin-history-adx", Connection = "EventHubConnection")]
            public TwinHistory[] outputEvents {get;set;}

        }
    }
}
