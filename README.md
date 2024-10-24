# renovate-config

This repository contains the shared renovate configurations.

The shared configurations are baselines. Each project is free to set their own rules on top of this configuration.

## Usage

### Extending the base configuration:
#### GitHub projects


```json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "extends": [
    "github>gsoft-inc/renovate-config"
  ]
}
```
#### Azure DevOps

```json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "platform": "azure",
  "endpoint": "https://dev.azure.com/YOUR_ORGANIZATION",
  "repositories": [
    "YOUR_REPO"
  ],
  "extends": [
      "github>gsoft-inc/renovate-config"
  ]
}
```

You will also need to provide a GitHub token in the CI:

```yaml
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
    ref: refs/tags/1.2.3

pool: 'Azure Pipelines'

variables:
- group: renovate

steps:
- template: steps/renovate/renovate-template.yml@templates
  parameters:
    githubToken: $(GITHUB_COM_TOKEN)
```

### Enabling Auto-Merge Functionality

There are multiple configurations you can extend to enable auto-merge on different packages. Here's a fully working example of all of them together:

```json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "extends": [
    "github>gsoft-inc/renovate-config"
    "github>gsoft-inc/renovate-config//microsoft-automerge.json",
    "github>gsoft-inc/renovate-config//workleap-automerge.json",
    "github>gsoft-inc/renovate-config//dotnet-trusted-thirdparty-dependencies-automerge.json",
    "github>gsoft-inc/renovate-config//all-automerge.json"
  ]
}

```

Auto-merge has been set up using the [branch approach](https://docs.renovatebot.com/key-concepts/automerge/#branch-vs-pr-automerging) to minimize noise and bypass PR review requirements. If your main branch has branch protection rules, you may need to allow your build agent's service account to bypass pull request creation.

With the branch approach, at least one status check must run on every Renovate branch when it is created, usually through your CI pipeline. If no status check is registered, Renovate will create the update branches but won't attempt to merge them into the main branch, as it will detect the absence of completed status checks.

To ensure one or more pipelines execute when a Renovate branch is created, add the Renovate branches as triggers. Since the branch names follow a standard pattern, they would look something like this:

```yaml
# Pipeline trigger in Azure DevOps
trigger:
  branches:
    include:
    - renovate/*


# Pipeline trigger in Github
on:
  push:
    branches:
      - 'renovate/*'
```


## System tests
In order to run the system tests you will need the `workflow` scope on this repository.

To request this scope you can run the following command in your CLI
```
gh auth login --scopes workflow
```

If you do not have this scope, you will run into the following error
```
! [remote rejected] main -> main (refusing to allow an OAuth App to create or update workflow `.github/workflows/ci.yml` without `workflow` scope)
```