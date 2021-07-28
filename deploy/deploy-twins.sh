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
twinReservoirOut="${twinReservoir}-Out"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Reservoir;1" --twin-id "$twinReservoir" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinReservoirOut" -o none
az dt twin relationship create --dt-name $adtName --source "$twinReservoir" --target "$twinReservoirOut" --relationship-id "$twinReservoir-to-$twinReservoirOut" --kind "isFlowingTo" -o none
# Pipe
twinPipe1="Pipe1"
twinPipe1In="${twinPipe1}-In"
twinPipe1Out="${twinPipe1}-Out"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinPipe1In" -o none
az dt twin relationship create --dt-name $adtName --source "$twinReservoirOut" --target "$twinPipe1In" --relationship-id "$twinReservoir-to-$twinPipe1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe1In" --target "$twinPipe1" --relationship-id "$twinPipe1In-to-$twinPipe1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinPipe1Out" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe1" --target "$twinPipe1Out" --relationship-id "$twinPipe1-to-$twinPipe1Out" --kind "isFlowingTo" -o none
# Pump
twinPump1="Pump1"
twinPump1In="${twinPump1}-In"
twinPump1Out="${twinPump1}-Out"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinPump1In" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe1Out" --target "$twinPump1In" --relationship-id "$twinPipe1-to-$twinPump1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPump1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1In" --target "$twinPump1" --relationship-id "$twinPump1In-to-$twinPump1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinPump1Out" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1" --target "$twinPump1Out" --relationship-id "$twinPump1-to-$twinPump1Out" --kind "isFlowingTo" -o none
# Pipe
twinPipe2="Pipe2"
twinPipe2In="${twinPipe2}-In"
twinPipe2Out="${twinPipe2}-Out"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinPipe2In" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPump1Out" --target "$twinPipe2In" --relationship-id "$twinPump1-to-$twinPipe2" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe2In" --target "$twinPipe2" --relationship-id "$twinPipe2In-to-$twinPipe2" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinPipe2Out" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe2" --target "$twinPipe2Out" --relationship-id "$twinPipe2-to-$twinPipe2Out" --kind "isFlowingTo" -o none
# Junction
twinSplit1="Split1"
twinSplit1In="${twinSplit1}-In"
twinSplit1Out1="${twinSplit1}-Out1"
twinSplit1Out2="${twinSplit1}-Out2"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinSplit1In" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe2Out" --target "$twinSplit1In" --relationship-id "$twinPipe2-to-$twinSplit1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinSplit1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1In" --target "$twinSplit1" --relationship-id "$twinSplit1In-to-$twinSplit1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinSplit1Out1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinSplit1Out1" --relationship-id "$twinSplit1-to-$twinSplit1Out1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinSplit1Out2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1" --target "$twinSplit1Out2" --relationship-id "$twinSplit1-to-$twinSplit1Out2" --kind "isFlowingTo" -o none
# Pipe
twinPipe3a="Pipe3a"
twinPipe3aIn="${twinPipe3a}-In"
twinPipe3aOut="${twinPipe3a}-Out"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinPipe3aIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1Out1" --target "$twinPipe3aIn" --relationship-id "$twinSplit1-to-$twinPipe3a" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe3a" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3aIn" --target "$twinPipe3a" --relationship-id "$twinPipe3aIn-to-$twinPipe3a" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinPipe3aOut" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3a" --target "$twinPipe3aOut" --relationship-id "$twinPipe3a-to-$twinPipe3aOut" --kind "isFlowingTo" -o none
# Tap
twinEnd1="End1"
twinEnd1In="${twinEnd1}-In"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinEnd1In" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3aOut" --target "$twinEnd1In" --relationship-id "$twinPipe3a-to-$twinEnd1" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinEnd1" -o none
az dt twin relationship create --dt-name $adtName --source "$twinEnd1In" --target "$twinEnd1" --relationship-id "$twinEnd1In-to-$twinEnd1" --kind "isFlowingTo" -o none
# Pipe
twinPipe3b="Pipe3b"
twinPipe3bIn="${twinPipe3b}-In"
twinPipe3bOut="${twinPipe3b}-Out"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinPipe3bIn" -o none
az dt twin relationship create --dt-name $adtName --source "$twinSplit1Out2" --target "$twinPipe3bIn" --relationship-id "$twinSplit1-to-$twinPipe3b" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinPipe3b" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3bIn" --target "$twinPipe3b" --relationship-id "$twinPipe3bIn-to-$twinPipe3b" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:OutFlow;1" --twin-id "$twinPipe3bOut" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3b" --target "$twinPipe3bOut" --relationship-id "$twinPipe3b-to-$twinPipe3bOut" --kind "isFlowingTo" -o none
# Valve
twinEnd2="End2"
twinEnd2In="${twinEnd2}-In"
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:InFlow;1" --twin-id "$twinEnd2In" -o none
az dt twin relationship create --dt-name $adtName --source "$twinPipe3bOut" --target "$twinEnd2In" --relationship-id "$twinPipe3b-to-$twinEnd2" --kind "isFlowingTo" -o none
az dt twin create --dt-name $adtName --dtmi "dtmi:sample:aqueduct:Pipe;1" --twin-id "$twinEnd2" -o none
az dt twin relationship create --dt-name $adtName --source "$twinEnd2In" --target "$twinEnd2" --relationship-id "$twinEnd2In-to-$twinEnd2" --kind "isFlowingTo" -o none
# ADD HERE

# Log end time
date
