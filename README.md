# renovate-config

This repository contains the shared renovate configurations.

The shared configurations are baselines. Each project is free to set their own rules on top of this configuration.

# Usage

## GitHub projects

````json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "extends": [
    "github>gsoft-inc/renovate-config"
  ]
}
````

# Azure DevOps

````json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "platform": "azure",
  "endpoint": "https://dev.azure.com/gsoft",
  "repositories": [
    "YOUR_REPO"
  ],
  "extends": [
      "github>gsoft-inc/renovate-config"
  ]
}
````

You also need to provide a GitHub token in the CI:

````yaml
trigger: none
schedules:
  - cron: "13 22 * * *"
    displayName: "Daily run"
    branches:
      include:
        - main
    always: true

resources:
  repositories:
  - repository: self
    type: git
  - repository: templates
    type: git
    name: Shared-Assets/Pipeline-Library
    ref: refs/tags/0.9.2

pool: 'Azure Pipelines'

variables:
- group: renovate

steps:
- template: steps/renovate/renovate-template.yml@templates
  parameters:
    githubToken: $(GITHUB_COM_TOKEN)
````
