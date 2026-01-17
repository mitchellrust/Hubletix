variable "environment" {
  description = "environment for the instance of the module"
  type        = string
}

variable "resource_name_prefix" {
  description = "prefix value for all resources defined in this module, to match the subscription"
  type        = string
}

variable "default_location" {
  description = "default location for resources"
  type        = string
}

variable "pipeline_service_principal_object_id" {
  description = "object id of the service principal used to deploy this app"
  type        = string
}

variable "additional_app_owner_ids" {
  description = "list of additional owners for this application"
  type        = list(string)
  default     = []
}

variable "admin_object_ids" {
  description = "list of object ids for admins who can access the application"
  type        = list(string)
  default     = []
}

variable "user_object_ids" {
  description = "list of object ids for users who can access the application"
  type        = list(string)
  default     = []
}