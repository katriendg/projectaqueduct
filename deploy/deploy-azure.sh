#!/bin/bash

if [[ $# -eq 0 ]]
then
    echo "Create environment in Azure."
    echo "Use --help for usage and options."
fi

# Get input parameters
enableDebugging=false
reuseResourceGroup=false
while [[ $# -gt 0 ]]
do
    key="${1}"
    case ${key} in
    -p|--prefix)
        prefixName="${2}"
        shift # past argument
        shift # past value
        ;;
    -l|--location)
        locationName="${2}"
        shift # past argument
        shift # past value
        ;;
    -r|--reuse)
        reuseResourceGroup=true
        shift # past argument
        ;;
    -d|--enableDebugging)
        enableDebugging=true
        shift # past argument
        ;;
    -h|--help)
        echo "Create environment in Azure, containing EH, Functions, ADT."
        echo ""
        echo "Usage: create-solution [options]"
        echo "  -p, --prefix=PREFIX  prefix used for Azure resource names"
        echo "  -l, --location=LOCATION  location to create Azure resources, default westeurope"
        echo "  -r, --reuse  reuse the resource group if it already exists"
        echo "  -d, --enableDebugging  add access of local user to Azure resources for Azure Functions debugging"
        echo ""
        shift # past argument
        ;;
    *)    # unknown option
        shift # past argument
        ;;
    esac
done

# Log Start time
date

# Validate input paremeters
if [[ -z "$prefixName" ]]
then
    echo "Missing prefix. Use --prefix to set the prefix used for azure resources."
    exit 1
fi

# Ensure location name is set or use default
if [[ -z "$locationName" ]]
then
    locationName="westeurope"
fi

# Veryfying the Azure subscription
subscriptionName=$(az account show --query 'name' -o tsv)
echo "Executing script against Azure Subscription: ${subscriptionName}"
userName=$(az account show --query "user.name" -o tsv)

echo "Using prefix '$prefixName' and location $locationName for all Azure resources."

# Verify resource group
rgName="${prefixName}rg"
rgExists=$(az group exists --name $rgName -o tsv)
if $rgExists
then
    if $reuseResourceGroup
    then
        echo "Using existing resource group $rgName."
    else
        echo "Resource group $rgName does already exist. Please remove the existing resource group first or use another one."
        exit 1
    fi
else
    az group create --name $rgName --location $locationName -o none
    echo "Resource group $rgName created."
fi

# Create common log workspace
workspaceName="${prefixName}log"
if az monitor log-analytics workspace show -g $rgName --workspace-name $workspaceName -o none 2>/dev/null
then
    echo "Using existing Log Analytics workspace $workspaceName."
else
    az monitor log-analytics workspace create -g $rgName --workspace-name $workspaceName --location $locationName -o none
    echo "Log Analytics workspace $workspaceName created."
fi

# Create common storage account
storageName="${prefixName}storage"
az storage account create -g $rgName --name $storageName --sku Standard_LRS --location $locationName --allow-blob-public-access false -o none
storageId=$(az storage account show -g $rgName -n $storageName --query "id" -o tsv)
echo "Storage $storageName created."
az monitor diagnostic-settings create --resource $storageId --workspace $workspaceName -n "all" -o none \


# Create application insights connected to log workspace
aiName="${prefixName}ai"
az extension add --name application-insights
az monitor app-insights component create -g $rgName --app $aiName --location $locationName --workspace $workspaceName -o none
echo "Application Insights component $aiName created."

# Create ADX cluster
kustoName="${prefixName}adx"
az extension add --name kusto
az kusto cluster create --name $kustoName -g $rgName -l $locationName --sku name="Dev(No SLA)_Standard_E2a_v4" tier="Basic" capacity=1 --no-wait --enable-streaming-ingest --type SystemAssigned
echo "Azure Data Explorer (Kusto) resource $kustoName started creation, silently continuing."

# Create function app on consumption plan
# ToDo: Create consumption plan first, not possible from Azure CLI at the moment
planName="${prefixName}plan"
#az functionapp plan create -g $rgName --name $planName --location $locationName --sku Y1 -o none
#echo "App Service plan $planName created."
fnaName="${prefixName}fn"
az functionapp create -g $rgName --name $fnaName --consumption-plan-location $locationName --storage-account $storageName --functions-version 3 --os-type Windows --app-insights $aiName -o none
echo "Function App $fnaName created."
az functionapp identity assign -g $rgName --name $fnaName -o none
fnaPrincipalId=$(az functionapp identity show -g $rgName --name $fnaName --query "principalId" -o tsv)
echo "  Function App $fnaName is using managed service identity ($fnaPrincipalId)."

# Create event hubs namespace
ehnName="${prefixName}eh"
az eventhubs namespace create -g $rgName --name $ehnName --sku Standard --location $locationName -o none
ehnId=$(az eventhubs namespace show -g $rgName --name $ehnName --query "id" -o tsv)
echo "Event Hubs namespace $ehnName created."
ehDeviceUpdatesName="device-updates"
az eventhubs eventhub create -g $rgName --namespace-name $ehnName --name $ehDeviceUpdatesName --partition-count 1 -o none
az eventhubs eventhub consumer-group create -g $rgName --namespace-name $ehnName --eventhub-name $ehDeviceUpdatesName --name $fnaName -o none
ehDeviceUpdatesId=$(az eventhubs eventhub show -g $rgName --namespace-name $ehnName -n $ehDeviceUpdatesName --query "id" -o tsv)
echo "  Event Hub $ehDeviceUpdatesName in Event Hubs namespace $ehnName created."
ehAssetUpdatesName="asset-updates"
az eventhubs eventhub create -g $rgName --namespace-name $ehnName --name $ehAssetUpdatesName --partition-count 1 -o none
az eventhubs eventhub consumer-group create -g $rgName --namespace-name $ehnName --eventhub-name $ehAssetUpdatesName --name $fnaName -o none
ehAssetUpdatesId=$(az eventhubs eventhub show -g $rgName --namespace-name $ehnName -n $ehAssetUpdatesName --query "id" -o tsv)
echo "  Event Hub $ehAssetUpdatesName in Event Hubs namespace $ehnName created."
ehTwinHistRawName="twin-history"
az eventhubs eventhub create -g $rgName --namespace-name $ehnName --name $ehTwinHistRawName --partition-count 1 -o none
az eventhubs eventhub consumer-group create -g $rgName --namespace-name $ehnName --eventhub-name $ehTwinHistRawName --name $kustoName -o none
ehTwinHistRawId=$(az eventhubs eventhub show -g $rgName --namespace-name $ehnName -n $ehTwinHistRawName --query "id" -o tsv)
echo "  Event Hub $ehTwinHistRawName in Event Hubs namespace $ehnName created."


# Create digital twin
adtName="${prefixName}adt"
az dt create -g $rgName -n $adtName --assign-identity --location $locationName -o none
adtId=$(az dt show -g $rgName -n $adtName --query "id" -o tsv)
echo "Azure Digital Twin instance $adtName created."
adtPrincipalId=$(az dt show -g $rgName -n $adtName --query "identity.principalId" -o tsv)
echo "  Azure Digital Twin $adtName is using managed service identity ($adtPrincipalId)."
az dt wait -g $rgName -n $adtName --custom "provisioningState=='Succeeded'"
az dt role-assignment create -n $adtName --assignee $userName --role "Azure Digital Twins Data Owner" -o none ## needed to set the adt routing rules
# Check for role assignment
while (true)
do
    assignedUserNames=$(az dt role-assignment list -g $rgName -n $adtName --role "Azure Digital Twins Data Owner" --query "[].principalName" -o tsv)
    if [[ $assignedUserNames == *$userName* ]]
    then
        break
    fi
    sleep 1
done
echo "  User $userName added as Data Owner to Azure Digital Twin $adtName."

# Configure Access Control
az role assignment create --assignee $fnaPrincipalId --role "Azure Event Hubs Data Receiver" --scope $ehDeviceUpdatesId -o none
az role assignment create --assignee $fnaPrincipalId --role "Azure Event Hubs Data Receiver" --scope $ehAssetUpdatesId -o none

if $enableDebugging
then
    az role assignment create --assignee $userName --role "Azure Event Hubs Data Receiver" --scope $ehDeviceUpdatesId -o none
    az role assignment create --assignee $userName --role "Azure Event Hubs Data Receiver" --scope $ehAssetUpdatesId -o none
    az role assignment create --assignee $userName --role "Azure Event Hubs Data Receiver" --scope $ehTwinHistRawId -o none
fi
az dt role-assignment create -n $adtName --assignee $fnaPrincipalId --role "Azure Digital Twins Data Owner" -o none
az role assignment create --assignee $adtPrincipalId --role "Azure Event Hubs Data Sender" --scope $ehDeviceUpdatesId -o none
az role assignment create --assignee $adtPrincipalId --role "Azure Event Hubs Data Sender" --scope $ehAssetUpdatesId -o none
az role assignment create --assignee $adtPrincipalId --role "Azure Event Hubs Data Sender" --scope $ehTwinHistRawId -o none
echo "Access Control configured."

# Create digital twins routing rules
az dt endpoint create eventhub -g $rgName -n $adtName --endpoint-name $ehDeviceUpdatesName --ehg $rgName --ehn $ehnName --eh $ehDeviceUpdatesName --auth-type IdentityBased -o none
az dt route create -g $rgName -n $adtName --route-name $ehDeviceUpdatesName --endpoint-name $ehDeviceUpdatesName --filter "type='Microsoft.DigitalTwins.Twin.Update' AND STARTS_WITH(\$body.modelId, 'dtmi:sample:aqueduct:device:')" -o none
az dt endpoint create eventhub -g $rgName -n $adtName --endpoint-name $ehAssetUpdatesName --ehg $rgName --ehn $ehnName --eh $ehAssetUpdatesName --auth-type IdentityBased -o none
az dt route create -g $rgName -n $adtName --route-name $ehAssetUpdatesName --endpoint-name $ehAssetUpdatesName --filter "type='Microsoft.DigitalTwins.Twin.Update' AND STARTS_WITH(\$body.modelId, 'dtmi:sample:aqueduct:asset:')" -o none
az dt endpoint create eventhub -g $rgName -n $adtName --endpoint-name $ehTwinHistRawName --ehg $rgName --ehn $ehnName --eh $ehTwinHistRawName --auth-type IdentityBased -o none
az dt route create -g $rgName -n $adtName --route-name $ehTwinHistRawName --endpoint-name $ehTwinHistRawName --filter "type = 'Microsoft.DigitalTwins.Twin.Update' OR type = 'Microsoft.DigitalTwins.Relationship.Update'" -o none
echo "Azure Digital Twin routing rules created."

# Configure Function App
az functionapp config appsettings set -g $rgName -n $fnaName --settings "EventHubConnection__fullyQualifiedNamespace=$ehnName.servicebus.windows.net" -o none
az functionapp config appsettings set -g $rgName -n $fnaName --settings "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" -o none
echo "Function app configured."

#validate ADX is created successfully, wait to finish until it is
echo "Now waiting for ADX Kusto cluster to be created"
az kusto cluster wait --cluster-name $kustoName --resource-group $rgName --created
echo "created"
kustoPrincipalId=$(az kusto cluster show -g $rgName -n $kustoName --query "identity.principalId" -o tsv)
echo "  ADX (Kusto) $kustoName is using managed service identity ($kustoPrincipalId)."
#Assign current User Id
az kusto cluster-principal-assignment create --cluster-name $kustoName --resource-group $rgName --principal-id $userName --principal-type "User" --role "AllDatabasesAdmin"  --principal-assignment-name "creatorPrincipalAssign1" -o none
echo "Kusto cluster principal assignment done for User $userName"

#create database
kustoDbName="adtHistoryDb"
az kusto database create --cluster-name $kustoName --resource-group $rgName --database-name $kustoDbName --read-write-database soft-delete-period=P365D hot-cache-period=P31D location=westeurope  -o none
echo "Kusto database $kustoDbName created"
#role assigment for EH
az role assignment create --assignee $kustoPrincipalId --role "Azure Event Hubs Data Receiver" --scope $ehTwinHistRawId -o none
echo "Kusto MSI ID added as Event Hubs Data Receiver"

# TODO download kusto.CLI, extract and run
# wget 

# Log end time
date