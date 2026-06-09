---
published: true
type: workshop
title: Product Hands-on Lab - Modern .NET API
short_title: Modern .NET API
description: This workshop will teach you how to build a modern .NET API. You will implement a document management API that allows users to upload, search, and download documents while following best practices for API design, security, and observability.
level: beginner # Required. Can be 'beginner', 'intermediate' or 'advanced'
navigation_numbering: false
authors: # Required. You can add as many authors as needed
  - Damien Aicheh
contacts: # Required. Must match the number of authors
  - "@damienaicheh"
duration_minutes: 360
tags: asp net, minimal API, .NET 10, azure, storage account, application insights, SQL, devcontainer, csu
navigation_levels: 3
banner_url: assets/banner.jpg
audience: developers

---

# Product Hands-on Lab - Modern .NET API

Welcome to this hands-on lab! In this workshop, you will learn how to build a modern .NET API using ASP.NET Core Minimal APIs, Azure SQL Database, Azure Blob Storage, and Application Insights. You will implement a document management API that allows users to upload, search, and download documents while following best practices for API design, security, and observability.

---

## Prerequisites

Before starting this lab, be sure to set your Azure environment :

- An Azure Subscription with the **Contributor** role to create and manage the labs' resources and deploy the infrastructure as code
- Register the Azure providers on your Azure Subscription if not done yet: `Microsoft.Storage`, `Microsoft.Sql`, `Microsoft.Insights`, `Microsoft.OperationalInsights`.

To retrieve the lab content :

- A GitHub account (Free, Team or Enterprise)
- Create a [fork][repo-fork] of the repository from the **main** branch to help you keep track of your changes

2 development options are available:
  - 🥈 Local Devcontainer
  - 🥉 Local Dev Environment with all the prerequisites detailed below

### 🥈 : Using a local Devcontainer

This repo comes with a Devcontainer configuration that will let you open a fully configured dev environment from your local Visual Studio Code, while still being completely isolated from the rest of your local machine configuration : No more dependancy conflict.
Here are the required tools to do so :

- [Git client][git-client]
- [Docker Desktop][docker-desktop] running
- [Visual Studio Code][vs-code] installed on your machine

Start by cloning the repository you just forked on your local Machine and open the local folder in Visual Studio Code.
Once you have cloned the repository locally, make sure Docker Desktop is up and running and open the cloned repository in Visual Studio Code.  

You will be prompted to open the project in a Dev Container. Click on `Reopen in Container`.

If you are not prompted by Visual Studio Code, you can open the command palette (`Ctrl + Shift + P`) and search for `Reopen in Container` and select it:

![devcontainer-reopen](./assets/devcontainer-reopen.png)

Once you have reopened the project in the Dev Container, Visual Studio Code will start building the container image based on the `.devcontainer` folder. This process might take a few minutes, but it only happens the first time you open the project in a Dev Container.

### 🥉 : Using your own local environment

The following tools and access will be necessary to run the lab on a local environment:  

- [Git client][git-client]
- [Visual Studio][visual-studio] installed
- [Azure CLI][az-cli-install] installed on your machine
- [Terraform][download-terraform] installed on your machine (to deploy the infrastructure as code)
- [Docker Desktop][docker-desktop]

Once you have set up your local environment, you can clone the repository you just forked on your machine, and open the solution in Visual Studio.

### Sign in to Azure

> - Log into your Azure subscription in your environment using Azure CLI and on the [Azure Portal][az-portal] using your credentials.
> - Register the Azure providers on your Azure Subscription if not done yet: `Microsoft.Storage`, `Microsoft.Sql`, `Microsoft.Insights`, `Microsoft.OperationalInsights`.

```bash
# Login to Azure : 
# --tenant : Optional | In case your Azure account has access to multiple tenants

# Option 1 : Local Environment 
az login --tenant <yourtenantid or domain.com>
# Option 2 : Github Codespace : you might need to specify --use-device-code parameter to ease the az cli authentication process
az login --use-device-code --tenant <yourtenantid or domain.com>

# Display your account details
az account show
# Select your Azure subscription
az account set --subscription <subscription-id>

# Register the following Azure providers if they are not already
# Azure Storage
az provider register --namespace 'Microsoft.Storage'
# Azure SQL
az provider register --namespace 'Microsoft.Sql'
# Azure Monitor
az provider register --namespace 'Microsoft.Insights'
# Azure Log Analytics
az provider register --namespace 'Microsoft.OperationalInsights'
```

### Deploy the infrastructure

First, you need to initialize the terraform infrastructure by running the following command:

```bash
# Run the following line which will dynamically set the subscription ID as an environment variable:
# On Bash or Zsh:
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
# On Windows PowerShell:
$env:ARM_SUBSCRIPTION_ID = (az account show --query id -o tsv)

# Initialize terraform
cd infra && terraform init
```

Then run the following command to deploy the infrastructure:

```bash
# Apply the deployment directly
terraform apply -auto-approve
```

The deployment should take around 5 minutes to complete.

[az-cli-install]: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
[az-portal]: https://portal.azure.com
[visual-studio]: https://visualstudio.microsoft.com/
[docker-desktop]: https://www.docker.com/products/docker-desktop/
[repo-fork]: https://github.com/damienaicheh/hands-on-lab-agent-framework-on-azure/fork
[git-client]: https://git-scm.com/downloads
[github-account]: https://github.com/join
[download-terraform]: https://developer.hashicorp.com/terraform/install

---