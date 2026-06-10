## Closing the workshop

Once you're done with this lab you can delete the resource group you created at the beginning.

To do so, click on **Delete resource group** in the Azure Portal to delete all the resources at once. The following Az-Cli command can also be used to delete the resource group:

```bash
# Delete the resource group with all the resources
az group delete --name <resource-group>
```