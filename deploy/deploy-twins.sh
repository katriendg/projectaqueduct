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
twinReservoir="Start"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Reservoir;1" --twin-id "$twinReservoir" -o none
# Pipe
twinPipe1="Pipe1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinReservoir" --target "$twinPipe1" --relationship-id "$twinPipe1-to-$twinPipe1" --kind "isFlowingTo" -o none
# Pump
twinPump1="Pump1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pump;1" --twin-id "$twinPump1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe1" --target "$twinPump1" --relationship-id "$twinPump1-to-$twinPump1" --kind "isFlowingTo" -o none
twinPump1Device="Pump1Device"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:PumpDevice;1" --twin-id "$twinPump1Device" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1Device" --target "$twinPump1" --relationship-id "$twinPump1Device-at-$twinPump1" --kind "isAttachedTo" -o none
# Pipe
twinPipe2="Pipe2"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1" --target "$twinPipe2" --relationship-id "$twinPipe2-to-$twinPipe2" --kind "isFlowingTo" -o none
# Junction
twinSplit1="Split1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Junction;1" --twin-id "$twinSplit1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe2" --target "$twinSplit1" --relationship-id "$twinSplit1-to-$twinSplit1" --kind "isFlowingTo" -o none
# Pipe
twinPipe3a="Pipe3a"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe3a" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinPipe3a" --relationship-id "$twinPipe3a-to-$twinPipe3a" --kind "isFlowingTo" -o none
# Tap
twinEnd1="End1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Tap;1" --twin-id "$twinEnd1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3a" --target "$twinEnd1" --relationship-id "$twinEnd1-to-$twinEnd1" --kind "isFlowingTo" -o none
# Pipe
twinPipe3b="Pipe3b"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe3b" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinPipe3b" --relationship-id "$twinPipe3b-to-$twinPipe3b" --kind "isFlowingTo" -o none
twinPipe3bDevice="Pipe3bDevice"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:FlowSensor;1" --twin-id "$twinPipe3bDevice" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3bDevice" --target "$twinPipe3b" --relationship-id "$twinPipe3bDevice-at-$twinPipe3b" --kind "isAttachedTo" -o none
# Valve
twinValve1="Valve1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Valve;1" --twin-id "$twinValve1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3b" --target "$twinValve1" --relationship-id "$twinPipe3b-to-$twinValve1" --kind "isFlowingTo" -o none
# Valve
twinVirtualDevice1="ValveDevice1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:VirtualDeviceValve;1" --twin-id "$twinVirtualDevice1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinVirtualDevice1" --target "$twinValve1" --relationship-id "$twinVirtualDevice1-at-$twinValve1" --kind "isAttachedTo" -o none
# Pipe
twinPipe3c="Pipe3c"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe3c" -o none
az dt twin relationship create --dt-name $adtName --source "$twinValve1" --target "$twinPipe3c" --relationship-id "$twinValve1-to-$twinPipe3c" --kind "isFlowingTo" -o none
# Tap2
twinEnd2="End2"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Tap;1" --twin-id "$twinEnd2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3c" --target "$twinEnd2" --relationship-id "$twinPipe3c-to-$twinEnd2" --kind "isFlowingTo" -o none

# ADD HERE

# Log end time
date
