# Extend Azure Function with ACI container

This sample demonstrates how you can extend Azure Function using an Azure Container Instance.

## The Scenario
Say we need a REST API to do a little bit image processing, like resize, crop etc. We want to leverage Azure function consumption plan to optimize the cost.

We could implement it ourselves using function supported language, C#, Java etc. But if someone have already done a good job, like [Imaginary](https://github.com/h2non/imaginary), we don't really want to reinvent the wheel.

Now the only problem is that imaginary is written in Go, how do we host that in Azure function?

## The Design
Here we will combine the power of Azure Function and Auzre Container Instance. Azure function will host the severless REST API, but we will let a container running in ACI do the actual image procesing.

We will implement 2 functions:

* Process function. This is a http trigger that exposes REST API. When the function receives a request, it checks whether the backend container is up and running. If not, it provisions the container, otherwise it simply calls the container endpoint for the actual processing.

* Cleanup function. This is a timer trigger. It runs every 5 mins and checks if the container is idle in the past 5mins, if it is, it deletes the container.

```
                 +----------+
httpTrigger -->  | Process  |  -- (create) ->  Management API
                 |          |  -- (proxy)  ->  Imaginary Container
                 +----------+
timerTrigger --> | Cleanup  |  -- (delete) ->  Management API
                 |          |
                 +----------+
```

## The Implementation
The sample is implemented in .Net Core on a Ubuntu machine, it should work on Windows as well.

### Run it locally

Set environment variable RESOURCE_GROUP and SUBSCRIPTION_ID for where you want the container to run.
```
az login
dotnet build -o bin
func host start
```

### Run it on Azure

1. Create a new Azure function.
2. In deployment options, configure as "local git"
3. git clone this repo
4. Add your function git url as a remote
5. git push to your function repo. this step will build the .net code remotly on azure function and deploy.
6. Set environment variable RESOURCE_GROUP and SUBSCRIPTION_ID for where you want the container to run.
7. Enable "Managed Service Identity" for your function. This will be the identity to manage the containers.
8. In the resource group, grant the Azure function Contributor permission (IAM tab).

## The Note
* In this sample the container endpoint is simply http, but you can extend to https fairly easy. Like this [caddy server](https://github.com/yangl900/azure-aci-samples/tree/master/caddyserver-autossl) sample.

* The container lifetime is managed by an in-memory state for simplicity. A proper way is probably persist it in a local file or somewhere remotely.
