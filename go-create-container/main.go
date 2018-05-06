package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"time"

	"github.com/Azure/azure-sdk-for-go/services/containerinstance/mgmt/2018-04-01/containerinstance"
	"github.com/Azure/azure-sdk-for-go/services/resources/mgmt/2016-06-01/subscriptions"
	"github.com/Azure/azure-sdk-for-go/services/resources/mgmt/2017-05-10/resources"
	"github.com/Azure/go-autorest/autorest/azure/auth"
	"github.com/Azure/go-autorest/autorest/to"
)

func prettyJSON(buffer []byte) string {
	var prettyJSON string
	if len(buffer) > 0 {
		var jsonBuffer bytes.Buffer
		error := json.Indent(&jsonBuffer, buffer, "", "  ")
		if error != nil {
			return string(buffer)
		}
		prettyJSON = jsonBuffer.String()
	} else {
		prettyJSON = ""
	}

	return prettyJSON
}

func main() {
	authorizer, err := auth.NewAuthorizerFromEnvironment()

	if err != nil {
		fmt.Println("Failed to find an authentication method.")
		fmt.Println("Define environment variable AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET.")
		return
	}

	subscriptionClient := subscriptions.NewClient()
	subscriptionClient.Authorizer = authorizer

	subs, err := subscriptionClient.ListComplete(context.Background())
	if err != nil {
		fmt.Println("Failed to list subscriptions: ", err)
		return
	}

	fmt.Printf("Found %d subscriptions. \n", len(*subs.Response().Value))
	fmt.Printf("Going to use subscription %s\n", *subs.Value().DisplayName)

	containerGroupsClient := containerinstance.NewContainerGroupsClient(*subs.Value().SubscriptionID)
	containerGroupsClient.Authorizer = authorizer
	containerGroupsClient.AddToUserAgent("github.com/yangl900/aci-samples")

	c := &containerinstance.ContainerGroup{
		Name:     to.StringPtr("ubuntu"),
		Location: to.StringPtr("eastus"),

		ContainerGroupProperties: &containerinstance.ContainerGroupProperties{
			OsType: containerinstance.Linux,
			Containers: &[]containerinstance.Container{
				containerinstance.Container{
					Name: to.StringPtr("ubuntu"),
					ContainerProperties: &containerinstance.ContainerProperties{
						Image: to.StringPtr("ubuntu"),
						Ports: &[]containerinstance.ContainerPort{
							{
								Port: to.Int32Ptr(80),
							},
						},
						Resources: &containerinstance.ResourceRequirements{
							Requests: &containerinstance.ResourceRequests{
								MemoryInGB: to.Float64Ptr(1),
								CPU:        to.Float64Ptr(1),
							},
						},
						Command: &[]string{
							"/bin/bash",
							"-c",
							"sleep 100000",
						},
					},
				},
			},
		},
	}

	buf, _ := c.MarshalJSON()
	fmt.Println("Going to create container with definition: ")
	fmt.Println(prettyJSON(buf))

	rgName := "demo"
	groupClient := resources.NewGroupsClient(*subs.Value().SubscriptionID)
	result, err := groupClient.CheckExistence(context.Background(), rgName)

	if result.StatusCode == 404 {
		groupClient.CreateOrUpdate(context.Background(), rgName, resources.Group{Location: to.StringPtr("EastUS")})
	}

	future, err := containerGroupsClient.CreateOrUpdate(context.Background(), rgName, *c.Name, *c)
	if err != nil {
		fmt.Println("Failed to create container group: ", err)
		return
	}

	fmt.Println("Creation request accepted. Status: ", future.Status())
	fmt.Println("Polling completion every second...")

	containerGroupsClient.PollingDelay = time.Second
	containerGroupsClient.PollingDuration = time.Minute
	err = future.WaitForCompletion(context.Background(), containerGroupsClient.Client)
	if err != nil {
		fmt.Println("Failed to wait for completion: ", err.Error())
	}

	cg, err := containerGroupsClient.Get(context.Background(), rgName, *c.Name)
	if err != nil {
		fmt.Println("Failed to get container: ", err.Error())
		return
	}

	respbuff, err := cg.MarshalJSON()

	fmt.Println("Created container group:")
	fmt.Println(prettyJSON(respbuff))

	fmt.Println("PrivisioningState: ", *cg.ProvisioningState)
	fmt.Println("Instance State: ", *cg.InstanceView.State)
	fmt.Printf("Group Events (%d):\n", len(*cg.InstanceView.Events))
	for _, evt := range *cg.InstanceView.Events {
		fmt.Printf("%s\t%s\t%s\n", evt.FirstTimestamp.String(), *evt.Name, *evt.Message)
	}

	for _, container := range *cg.Containers {
		fmt.Printf("Container '%s' Events (%d):\n", *container.Name, len(*container.InstanceView.Events))

		for _, evt := range *container.InstanceView.Events {
			fmt.Printf("%s\t%s\t%s\n", evt.FirstTimestamp.String(), *evt.Name, *evt.Message)
		}
	}
}
