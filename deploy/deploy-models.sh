if [[ $# -eq 0 ]]
then
    echo "Deploy ADT models."
    echo "Use --help for usage and options."
    exit 1
fi

# Get input parameters
clearModels=false
while [[ $# -gt 0 ]]
do
    key="${1}"
    case ${key} in
    -n|--dt-name)
        adtName="${2}"
        shift # past argument
        shift # past value
        ;;
    -f|--folder)
        modelsFolder="${2}"
        shift # past argument
        shift # past value
        ;;
    -c|--clear)
        clearModels=true
        shift # past argument
        ;;
    -h|--help)
        echo "Deploy ADT models from DTDL files."
        echo ""
        echo "Usage: deploy-models [options]"
        echo "  -f, --folder=FOLDER  folder containing the models to import"
        echo "  -c, --clear  clear the Azure Digital Twins instance models and twins first"
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

# Delete twins and models
if [[ $clearModels ]]
then
    echo "Delete all twins from $adtName."
    az dt twin delete-all --yes --dt-name $adtName -o none
    echo "Delete all models from $adtName."
    az dt model delete-all --yes --dt-name $adtName -o none
fi

# Deploy models from files
if [[ ! -z $modelsFolder ]]
then
    echo "Deploy DTDL models to Azure Digital Twins instance '$adtName' from folder '$modelsFolder'."
    az dt model create --dt-name $adtName --from-directory $modelsFolder -o none

    echo "Models available after deployment in Azure Digital Twins instance '$adtName':"
    az dt model list --dt-name $adtName --query '[].id'    
fi

# Log end time
date
