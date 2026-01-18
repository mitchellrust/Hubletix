variable "environment" {
  description = "environment for the instance of the module"
  type        = string
}

variable "region_override" {
  description = "Region override if different than that of the provider"
  type        = string
  default     = ""
}
