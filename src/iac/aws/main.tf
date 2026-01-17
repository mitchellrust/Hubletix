#
# allirustvb.com Module, for all things allirustvb.com.
#

terraform {
  # Reusable modules, like app modules, should contrain only the min allowed version.
  required_version = ">= 1.1.0"

  required_providers {
    # NOTE: Changing these values means you need to change values in test-main.tf as well.
    azurerm = {
      source  = "hashicorp/azurerm"
      # 4.29 - Latest at time of creation
      version = ">= 4.29"
    }

    azuread = {
      source  = "hashicorp/azuread"
      # 3.4 - Latest at time of creation
      version = ">= 3.4"
    }
  }
}

locals { }

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/data-sources/client_config
data "azuread_client_config" "current" {}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/group
resource "azuread_group" "admins" {
  display_name     = "App allirustvb.com Admins"
  owners           = [data.azuread_client_config.current.object_id]
  security_enabled = true
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/group_member
resource "azuread_group_member" "admin" {
  for_each = toset(var.admin_object_ids)

  group_object_id  = azuread_group.admins.object_id
  member_object_id = each.value
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/group
resource "azuread_group" "users" {
  display_name     = "App allirustvb.com Users"
  owners           = [data.azuread_client_config.current.object_id]
  security_enabled = true
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/group_member
resource "azuread_group_member" "user" {
  for_each = toset(var.user_object_ids)

  group_object_id  = azuread_group.users.object_id
  member_object_id = each.value
}

# https://registry.terraform.io/providers/hashicorp/random/latest/docs/resources/uuid
resource "random_uuid" "admin_role_id" {}

# https://registry.terraform.io/providers/hashicorp/random/latest/docs/resources/uuid
resource "random_uuid" "user_role_id" {}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/application
resource "azuread_application" "test" {
  display_name = "${var.resource_name_prefix}reg-allirustvbcom-test"

  owners = distinct(
    concat(
      [
        var.pipeline_service_principal_object_id
      ],
      var.additional_app_owner_ids
    )
  )

  sign_in_audience = "AzureADMyOrg"
  # Using roles, not groups
  group_membership_claims = [ "None" ]

  app_role {
    allowed_member_types = [ "User" ]
    description          = "Admin role for the app"
    display_name         = "Admin"
    id                   = random_uuid.admin_role_id.result
    enabled              = true
    value                = "admin"
  }

  app_role {
    allowed_member_types = [ "User" ]
    description          = "User role for the app"
    display_name         = "User"
    id                   = random_uuid.user_role_id.result
    enabled              = true
    value                = "user"
  }

  web {
    homepage_url  = "https://coach-website-git-preview-mitchell-rusts-projects.vercel.app/admin"
    redirect_uris = [
      "http://localhost:3000/api/auth/callback/azure-ad", # for local dev
      "https://coach-website-git-preview-mitchell-rusts-projects.vercel.app/api/auth/callback/azure-ad",  # for test
      "https://coach-website-git-private-lessons-mvp-mitchell-rusts-projects.vercel.app/api/auth/callback/azure-ad", # for one-off testing of private lessons
    ]
  }
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/service_principal
resource "azuread_service_principal" "test" {
  client_id                    = azuread_application.test.client_id
  app_role_assignment_required = true
  
  owners = distinct(
    concat(
      [
        var.pipeline_service_principal_object_id
      ],
      var.additional_app_owner_ids
    )
  )
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment
resource "azuread_app_role_assignment" "admins_test" {
  app_role_id         = random_uuid.admin_role_id.result
  principal_object_id = azuread_group.admins.object_id
  resource_object_id  = azuread_service_principal.test.object_id
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment
resource "azuread_app_role_assignment" "users_test" {
  app_role_id         = random_uuid.user_role_id.result
  principal_object_id = azuread_group.users.object_id
  resource_object_id  = azuread_service_principal.test.object_id
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/application
resource "azuread_application" "prod" {
  display_name = "${var.resource_name_prefix}reg-allirustvbcom-prod"

  owners = distinct(
    concat(
      [
        var.pipeline_service_principal_object_id
      ],
      var.additional_app_owner_ids
    )
  )

  sign_in_audience = "AzureADMyOrg"
  # Using roles, not groups
  group_membership_claims = [ "None" ]

  app_role {
    allowed_member_types = [ "User" ]
    description          = "Admin role for the app"
    display_name         = "Admin"
    id                   = random_uuid.admin_role_id.result
    enabled              = true
    value                = "admin"
  }

  app_role {
    allowed_member_types = [ "User" ]
    description          = "User role for the app"
    display_name         = "User"
    id                   = random_uuid.user_role_id.result
    enabled              = true
    value                = "user"
  }

  web {
    homepage_url  = "https://allirustvb.com/admin"
    redirect_uris = [
      "https://allirustvb.com/api/auth/callback/azure-ad",
      "https://coach-website-git-main-mitchell-rusts-projects.vercel.app/api/auth/callback/azure-ad"
    ]
  }
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/service_principal
resource "azuread_service_principal" "prod" {
  client_id                    = azuread_application.prod.client_id
  app_role_assignment_required = true
  
  owners = distinct(
    concat(
      [
        var.pipeline_service_principal_object_id
      ],
      var.additional_app_owner_ids
    )
  )
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment
resource "azuread_app_role_assignment" "admins_prod" {
  app_role_id         = random_uuid.admin_role_id.result
  principal_object_id = azuread_group.admins.object_id
  resource_object_id  = azuread_service_principal.prod.object_id
}

# https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment
resource "azuread_app_role_assignment" "users_prod" {
  app_role_id         = random_uuid.user_role_id.result
  principal_object_id = azuread_group.users.object_id
  resource_object_id  = azuread_service_principal.prod.object_id
}

 # https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/resource_group
resource "azurerm_resource_group" "main" {
  name     = "${var.resource_name_prefix}rgp-allirustvbcom-${var.environment}"
  location = var.default_location
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_account
# Note that while using this cosmosdb account for test and prod, we'll split out databases as below.
resource "azurerm_cosmosdb_account" "main" {
  name                = "${var.resource_name_prefix}cos-allirustvbcom-${var.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  offer_type          = "Standard"
  # Free tier baby!!!
  free_tier_enabled   = true

  # Default to regular old NoSQL to take advantage of the free tier
  kind = "GlobalDocumentDB"

  # Min TLS 1.2
  minimal_tls_version = "Tls12"

  # This is included in free tier, pretty slick.
  automatic_failover_enabled = true

  # TODO: Figure out network security / IP whitelisting if possible
  # ip_range_filter = [ ]

  # TODO: Figure out better network security.
  # For now, just open up to public internet.
  public_network_access_enabled = true

  # Default when creating Cosmos DB
  consistency_policy {
    consistency_level = "Session"
  }

  # Not using geo-replication, but need to specify primary location.
  geo_location {
    location          = azurerm_resource_group.main.location
    failover_priority = 0
  }

  # prevent the possibility of accidental data loss
  lifecycle {
    prevent_destroy = true
  }
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_database
resource "azurerm_cosmosdb_sql_database" "test" {
  name                = "test"
  resource_group_name = azurerm_cosmosdb_account.main.resource_group_name
  account_name        = azurerm_cosmosdb_account.main.name
  throughput          = 400
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container
resource "azurerm_cosmosdb_sql_container" "group_lesson_test" {
  name                  = "GroupLesson"
  resource_group_name   = azurerm_cosmosdb_account.main.resource_group_name
  account_name          = azurerm_cosmosdb_account.main.name
  database_name         = azurerm_cosmosdb_sql_database.test.name
  partition_key_paths   = [
    "/id"
  ]
  partition_key_version = 1
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container
resource "azurerm_cosmosdb_sql_container" "private_lesson_test" {
  name                  = "PrivateLesson"
  resource_group_name   = azurerm_cosmosdb_account.main.resource_group_name
  account_name          = azurerm_cosmosdb_account.main.name
  database_name         = azurerm_cosmosdb_sql_database.test.name
  partition_key_paths   = [
    "/id"
  ]
  partition_key_version = 1
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_database
resource "azurerm_cosmosdb_sql_database" "prod" {
  name                = "prod"
  resource_group_name = azurerm_cosmosdb_account.main.resource_group_name
  account_name        = azurerm_cosmosdb_account.main.name
  throughput          = 400
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container
resource "azurerm_cosmosdb_sql_container" "group_lesson_prod" {
  name                  = "GroupLesson"
  resource_group_name   = azurerm_cosmosdb_account.main.resource_group_name
  account_name          = azurerm_cosmosdb_account.main.name
  database_name         = azurerm_cosmosdb_sql_database.prod.name
  partition_key_paths   = [
    "/id"
  ]
  partition_key_version = 1
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container
resource "azurerm_cosmosdb_sql_container" "private_lesson_prod" {
  name                  = "PrivateLesson"
  resource_group_name   = azurerm_cosmosdb_account.main.resource_group_name
  account_name          = azurerm_cosmosdb_account.main.name
  database_name         = azurerm_cosmosdb_sql_database.prod.name
  partition_key_paths   = [
    "/id"
  ]
  partition_key_version = 1
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/storage_account
resource "azurerm_storage_account" "test" {
  name                           = "${var.resource_name_prefix}strarvbcomtest"
  resource_group_name            = azurerm_resource_group.main.name
  location                       = azurerm_resource_group.main.location
  account_tier                   = "Standard"
  account_replication_type       = "LRS"
  account_kind                   = "StorageV2"

  # Consumption defaults
  allow_nested_items_to_be_public = false
  default_to_oauth_authentication = true
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/service_plan
resource "azurerm_service_plan" "test" {
  name                = "${var.resource_name_prefix}asp-allirustvbcom-test"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Windows"
  # Consumption tier
  sku_name            = "Y1"

  # Consumption defaults
  zone_balancing_enabled = false
}

# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/windows_function_app
resource "azurerm_windows_function_app" "test" {
  name                = "${var.resource_name_prefix}fap-allirustvbcom-test"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  storage_account_name       = azurerm_storage_account.test.name
  storage_account_access_key = azurerm_storage_account.test.primary_access_key
  service_plan_id            = azurerm_service_plan.test.id

  # Consumption defaults
  builtin_logging_enabled = true
  client_certificate_mode = "Optional"
  ftp_publish_basic_authentication_enabled = false

  site_config {
    ftps_state = "Disabled"
    ip_restriction_default_action = "Allow"
    scm_ip_restriction_default_action = "Allow"

    # We're gonna have pretty low memory, so keeping this set true.
    use_32_bit_worker = true
  }
}