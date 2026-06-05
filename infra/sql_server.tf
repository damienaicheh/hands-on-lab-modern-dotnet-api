resource "azurerm_mssql_server" "this" {
  name                         = format("sql-%s", local.resource_suffix_kebabcase)
  resource_group_name          = local.resource_group_name
  location                     = local.resource_group_location
  version                      = "12.0"
  administrator_login          = "azureuser"
  administrator_login_password = "P@ssw0rd1234!"
  minimum_tls_version          = "1.2"

  azuread_administrator {
    login_username = "AzureAD Admin"
    object_id      = data.azurerm_client_config.current.object_id
  }

  tags = local.tags
}