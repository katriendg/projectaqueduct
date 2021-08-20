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
        echo "Deploy ADT twins forming a Water Distribution System."
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
echo $twinReservoir
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Reservoir;1" --twin-id "$twinReservoir" -o none
az dt twin update --dt-name $adtName --twin-id $twinReservoir --json-patch '[ {"op":"add", "path":"/Capacity", "value": 5000}, {"op":"add", "path":"/FlowCapacity", "value": 100} ]' -o none
# Valve
twinValve1="Valve1"
echo $twinValve1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Valve;1" --twin-id "$twinValve1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinReservoir" --target "$twinValve1" --relationship-id "$twinReservoir-to-$twinValve1" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinValve1 --json-patch '[ {"op":"add", "path":"/OpenStatus", "value": 0}, {"op":"add", "path":"/FlowCapacity", "value": 100} ]' -o none
# Pipe
twinPipe1="Pipe1"
echo $twinPipe1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Pipe;1" --twin-id "$twinPipe1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinValve1" --target "$twinPipe1" --relationship-id "$twinValve1-to-$twinPipe1" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinPipe1 --json-patch '[ {"op":"add", "path":"/Diameter", "value": 20}, {"op":"add", "path":"/Length", "value": 2}, {"op":"add", "path":"/FlowCapacity", "value": 100} ]' -o none
# Pump
twinPump1="Pump1"
echo $twinPump1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Pump;1" --twin-id "$twinPump1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe1" --target "$twinPump1" --relationship-id "$twinPipe1-to-$twinPump1" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinPump1 --json-patch '[ {"op":"add", "path":"/FlowCapacity", "value": 100}, {"op":"add", "path":"/FlowMargin", "value": 20} ]' -o none
twinPump1Device="Pump1Device"
echo $twinPump1Device
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:device:Pump;1" --twin-id "$twinPump1Device" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1Device" --target "$twinPump1" --relationship-id "$twinPump1Device-at-$twinPump1" --kind "isAttachedTo" -o none
# Pipe
twinPipe2="Pipe2"
echo $twinPipe2
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Pipe;1" --twin-id "$twinPipe2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1" --target "$twinPipe2" --relationship-id "$twinPump1-to-$twinPipe2" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinPipe2 --json-patch '[ {"op":"add", "path":"/Diameter", "value": 20}, {"op":"add", "path":"/Length", "value": 50}, {"op":"add", "path":"/FlowCapacity", "value": 100}, {"op":"add", "path":"/FlowMargin", "value": 20} ]' -o none
# Junction
twinSplit1="Split1"
echo $twinSplit1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Junction;1" --twin-id "$twinSplit1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe2" --target "$twinSplit1" --relationship-id "$twinPipe2-to-$twinSplit1" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinSplit1 --json-patch '[ {"op":"add", "path":"/Diameter", "value": 20}, {"op":"add", "path":"/FlowCapacity", "value": 100} ]' -o none
# Pipe
twinPipe3a="Pipe3a"
echo $twinPipe3a
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Pipe;1" --twin-id "$twinPipe3a" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinPipe3a" --relationship-id "$twinSplit1-to-$twinPipe3a" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinPipe3a --json-patch '[ {"op":"add", "path":"/Diameter", "value": 15}, {"op":"add", "path":"/Length", "value": 30}, {"op":"add", "path":"/FlowCapacity", "value": 80}, {"op":"add", "path":"/FlowMargin", "value": 20} ]' -o none
# Tap
twinEnd1="End1"
echo $twinEnd1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Tap;1" --twin-id "$twinEnd1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3a" --target "$twinEnd1" --relationship-id "$twinPipe3a-to-$twinEnd1" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinEnd1 --json-patch '[ {"op":"add", "path":"/FlowCapacity", "value": 80} ]' -o none
# Pipe
twinPipe3b="Pipe3b"
echo $twinPipe3b
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Pipe;1" --twin-id "$twinPipe3b" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinPipe3b" --relationship-id "$twinSplit1-to-$twinPipe3b" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinPipe3b --json-patch '[ {"op":"add", "path":"/Diameter", "value": 15}, {"op":"add", "path":"/Length", "value": 80}, {"op":"add", "path":"/FlowCapacity", "value": 80}, {"op":"add", "path":"/FlowMargin", "value": 20} ]' -o none
twinPipe3bDevice="Pipe3bDevice"
echo $twinPipe3bDevice
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:device:FlowSensor;1" --twin-id "$twinPipe3bDevice" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3bDevice" --target "$twinPipe3b" --relationship-id "$twinPipe3bDevice-at-$twinPipe3b" --kind "isAttachedTo" -o none
# Valve
twinValve2="Valve2"
echo $twinValve2
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Valve;1" --twin-id "$twinValve2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3b" --target "$twinValve2" --relationship-id "$twinPipe3b-to-$twinValve2" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinValve2 --json-patch '[ {"op":"add", "path":"/OpenStatus", "value": 1}, {"op":"add", "path":"/FlowCapacity", "value": 80}, {"op":"add", "path":"/FlowMargin", "value": 20} ]' -o none
twinVirtualDevice1="ValveDevice1"
echo $twinVirtualDevice1
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:device:Valve;1" --twin-id "$twinVirtualDevice1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinVirtualDevice1" --target "$twinValve2" --relationship-id "$twinVirtualDevice1-at-$twinValve2" --kind "isAttachedTo" -o none
# Pipe
twinPipe3c="Pipe3c"
echo $twinPipe3c
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Pipe;1" --twin-id "$twinPipe3c" -o none
az dt twin relationship create --dt-name $adtName --source "$twinValve2" --target "$twinPipe3c" --relationship-id "$twinValve2-to-$twinPipe3c" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinPipe3c --json-patch '[ {"op":"add", "path":"/Diameter", "value": 15}, {"op":"add", "path":"/Length", "value": 30}, {"op":"add", "path":"/FlowCapacity", "value": 80}, {"op":"add", "path":"/FlowMargin", "value": 20} ]' -o none
# Tap2
twinEnd2="End2"
echo $twinEnd2
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:asset:Tap;1" --twin-id "$twinEnd2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3c" --target "$twinEnd2" --relationship-id "$twinPipe3c-to-$twinEnd2" --kind "isFlowingTo" -o none
az dt twin update --dt-name $adtName --twin-id $twinEnd2 --json-patch '[ {"op":"add", "path":"/FlowCapacity", "value": 80} ]' -o none

# Area
twinArea1="Area1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Area;1" --twin-id "$twinArea1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinReservoir" --target "$twinArea1" --relationship-id "$twinReservoir-in-$twinArea1" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinValve1" --target "$twinArea1" --relationship-id "$twinValve1-in-$twinArea1" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe1" --target "$twinArea1" --relationship-id "$twinPipe1-in-$twinArea1" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1" --target "$twinArea1" --relationship-id "$twinPump1-in-$twinArea1" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe2" --target "$twinArea1" --relationship-id "$twinPipe2-in-$twinArea1" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinArea1" --relationship-id "$twinSplit1-in-$twinArea1" --kind "isLocatedIn" -o none
# Area
twinArea2="Area2"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Area;1" --twin-id "$twinArea2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinArea2" --relationship-id "$twinSplit1-in-$twinArea2" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3a" --target "$twinArea2" --relationship-id "$twinPipe3a-in-$twinArea2" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinEnd1" --target "$twinArea2" --relationship-id "$twinEnd1-in-$twinArea2" --kind "isLocatedIn" -o none
# Area
twinArea3="Area3"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Area;1" --twin-id "$twinArea3" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinArea3" --relationship-id "$twinSplit1-in-$twinArea3" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3b" --target "$twinArea3" --relationship-id "$twinPipe3b-in-$twinArea3" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinValve2" --target "$twinArea3" --relationship-id "$twinValve2-in-$twinArea3" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3c" --target "$twinArea3" --relationship-id "$twinPipe3c-in-$twinArea3" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinEnd2" --target "$twinArea3" --relationship-id "$twinEnd2-in-$twinArea3" --kind "isLocatedIn" -o none
# Region
twinRegion="Region"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Region;1" --twin-id "$twinRegion" -o none
az dt twin relationship create --dt-name $adtName --source "$twinArea1" --target "$twinRegion" --relationship-id "$twinArea1-in-$twinRegion" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinArea2" --target "$twinRegion" --relationship-id "$twinArea2-in-$twinRegion" --kind "isLocatedIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinArea3" --target "$twinRegion" --relationship-id "$twinArea3-in-$twinRegion" --kind "isLocatedIn" -o none

# Operator
twinOperator1="Operator1"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Operator;1" --twin-id "$twinOperator1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinOperator1" --target "$twinArea1" --relationship-id "$twinOperator1-for-$twinArea1" --kind "isServing" -o none
az dt twin relationship create --dt-name $adtName --source "$twinOperator1" --target "$twinArea2" --relationship-id "$twinOperator1-for-$twinArea2" --kind "isServing" -o none
twinOperator2="Operator2"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Operator;1" --twin-id "$twinOperator2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinOperator2" --target "$twinArea3" --relationship-id "$twinOperator2-for-$twinArea3" --kind "isServing" -o none

# ADD HERE

# Log end time
date
