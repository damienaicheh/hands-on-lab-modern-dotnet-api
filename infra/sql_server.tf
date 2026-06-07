resource "azurerm_mssql_server" "this" {
  name                                     = format("sql-%s", local.resource_suffix_kebabcase)
  resource_group_name                      = local.resource_group_name
  location                                 = local.resource_group_location
  version                                  = "12.0"
  administrator_login                      = "azureuser"
  administrator_login_password             = "P@ssw0rd1234!"
  minimum_tls_version                      = "1.2"
  express_vulnerability_assessment_enabled = false

  azuread_administrator {
    login_username = "AzureAD Admin"
    object_id      = data.azurerm_client_config.current.object_id
  }

  tags = local.tags
}

resource "azurerm_mssql_database" "document_api" {
  name                                                       = "DocumentDb"
  server_id                                                  = azurerm_mssql_server.this.id
  collation                                                  = "SQL_Latin1_General_CP1_CI_AS"
  license_type                                               = "LicenseIncluded"
  maintenance_configuration_name                             = "SQL_Default"
  max_size_gb                                                = 2
  min_capacity                                               = 0
  read_scale                                                 = false
  sku_name                                                   = "GP_Gen5_2"
  storage_account_type                                       = "Geo"
  tags                                                       = local.tags
}
