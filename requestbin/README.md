# RequestBin

This template demonstrates how you can run [RequestBin](https://requestb.in/) in [Azure Container Instances](https://docs.microsoft.com/en-us/azure/container-instances/).

By deploying this template you will get a RequestBin service:
> RequestBin gives you a URL that will collect requests made to it and let you inspect them in a human-friendly way.
> Use RequestBin to see what your HTTP client is sending or to inspect and debug webhook requests.

## Using this template
### Azure CLI:
````
az login

az group create --name requestbin --location westus

az group deployment create \
    --name requestbin \
    --resource-group requestbin \
    --template-uri "https://raw.githubusercontent.com/wenwu449/azure-aci-samples/master/requestbin/azuredeploy.json"
    --template-parameter location=westus

az group deployment show \
    --name requestbin \
    --resource-group requestbin \
    --query properties.outputs.requestbin_url.value
````


### Azure Portal
Click this button to deploy.
<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fwenwu449%2Fazure-aci-samples%2Fmaster%2Frequestbin%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
