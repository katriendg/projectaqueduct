if [[ $# -eq 0 ]]
then
    echo "Deploy ADT twins."
    echo "Use --help for usage and options."
    exit 1
fi

# Get input parameters
clearTwins=false
while [[ $# -gt 0 ]]
do
    key="${1}"
    case ${key} in
    -n|--dt-name)
        adtName="${2}"
        shift # past argument
        shift # past value
        ;;
    -c|--clear)
        clearTwins=true
        shift # past argument
        ;;
    -h|--help)
        echo "Deploy ADT models from DTDL files."
        echo ""
        echo "Usage: deploy-models [options]"
        echo "  -c, --clear  clear the Azure Digital Twins instance twins"
        echo "  -n, --dt-name=DTNAME  name of the Azure digital twin instance"
        echo ""
        shift # past argument
        ;;
    *)    # unknown option
        shift # past argument
        ;;
    esac
done

# Validate input paremeters
if [[ -z $adtName ]]
then
    echo "Missing Azure Digital Twins name. Use --dt-name to set the name of the Azure Digital Twins instance to use."
    exit 1
fi

# Log start time
date

# Veryfying the Azure subscription
subscriptionName=$(az account show --query 'name' -o tsv)
echo "Executing script against Azure Subscription: ${subscriptionName}"

echo "Using Azure Digital Twins instance '$adtName' for the deployment."

# Delete twins
if [[ $clearTwins ]]
then
    echo "Delete all twins from $adtName."
    az dt twin delete-all --yes --dt-name $adtName -o none
fi

# Deploy the twins
echo "Deploy twins to Azure Digital Twins instance '$adtName'."
# Reservoir
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Reservoir;1" --twin-id "Start" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "Start-Out" -o none
az dt twin relationship create --dt-name $adtName --source "Start" --target "Start-Out" --relationship-id "Start-to-Start-Out" --kind "isFlowingTo" -o none
# Pipe1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "Pipe1-In" -o none
az dt twin relationship create --dt-name $adtName --source "Start-Out" --target "Pipe1-In" --relationship-id "Start-to-Pipe1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "Pipe1" -o none
az dt twin relationship create --dt-name $adtName --source "Pipe1-In" --target "Pipe1" --relationship-id "Pipe1-In-to-Pipe1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "Pipe1-Out" -o none
az dt twin relationship create --dt-name $adtName --source "Pipe1" --target "Pipe1-Out" --relationship-id "Pipe1-to-Pipe1-Out" --kind "isFlowingTo" -o none
# Tap
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "End-In" -o none
az dt twin relationship create --dt-name $adtName --source "Pipe1-Out" --target "End-In" --relationship-id "Pipe1-to-End" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Tap;1" --twin-id "End" -o none
az dt twin relationship create --dt-name $adtName --source "End-In" --target "End" --relationship-id "End-In-to-End" --kind "isFlowingTo" -o none
# ADD HERE

# Log end time
date
