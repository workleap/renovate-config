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
