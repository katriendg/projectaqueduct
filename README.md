# Project Aqueduct

End-to-end sample application in process industry showcasing how to leverage Azure Digital Twins, a DTDL based ontology, and the right mix of Azure services to address key patterns.

## Deployment

The repo contains deployment scripts for the full solution (execute from the `./deploy` folder).

### Pre-requisites
- Azure account
- Azure CLI
- .NET Core 5
- Visual Studio Code
- Azure Functions Tools

### Deploy

1. Deploy Azure Resources

    `./deploy-azure.sh -p aqueduct`

2. Deploy Azure Function

    From Visual Studio Code: Azure Functions: Deploy to Function App (which has been created with the above script)

3. Deploy Azure Digital Twins models

    `/deploy-models.sh -n aqueductadt -c -f ../models`

4. Deploy twins

    `./deploy-twins.sh -n aqueductadt`


## Testing

The repo contains a simulator for testing the solution (see `src/SimulateData` project).
To run the console app locally, update the appsettings.json file with the URL of your Azure Digital Twins service created by the deployment scripts. Or create your local `appsettings.local.json` file with the following structure:

```
{
    "ADT":
    {
        "ADT_URI": "https://[YOUR_ADT_URL]"
    }
}
```

