# Project Aqueduct

End-to-end sample application in process industry showcasing how to leverage Azure Digital Twins, a DTDL based ontology, and the right mix of Azure services to address key patterns.

## Deployment

The repo contains deployment scripts for the full solution (execute from the `./deploy` folder).

1. Deploy Azure Resources

    `./deploy-azure.sh -p aqueduct`

2. Deploy Azure Function

    From Visual Studio Code: Azure Functions: Deploy to Function App

3. Deploy Azure Digital Twins models

    `/deploy-models.sh -n aqueductadt -c -f ../models`

4. Deploy twins

    `./deploy-twins.sh -n aqueductadt`

## Testing

The repo contains a simulator for testing the solution (see `SimulateData` project).
