# Caddy Server with auto ssl

This template demonstrates how you can run [Caddy Server](https://caddyserver.com/) in [Azure Container Instances](https://docs.microsoft.com/en-us/azure/container-instances/), with a DNS name and automatic TLS.

By deploying this template you will get a https web server on a `.eastus.azurecontainer.io` domain. A free SSL cert will be issued by Let's Encrypt and get renewed automatically.

## Implementation

* A secret volume is mounted to /tmp for Caddyfile. Doesn't really need secret, this is just a convinient way to pass value from ARM template into a file. The Caddyfile content is only 2 lines: domain name and your email.

* An azure file volume is mounted to /root/.caddy. this is to persist the certificates requested from let's encrypt. You can request new cert every time, but likely will hit let's encrypt request limit very soon. The volume expect the storage account already have a share named **`certs`**

## Using this template

1. Create a storage account or pick an existing storage account to persist certificates.
2. Create a file share named `certs` in the storage account.
3. Click this button to deploy.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fyangl900%2Fazure-aci-samples%2Fmaster%2Fcaddyserver-autossl%2Fdeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
