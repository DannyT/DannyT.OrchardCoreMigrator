# OrchardCore Migrator
Tools to migrate CMS websites to Orchard Core

So far, this contains a command line tool that will take a WordPress export and create an Orchard Core recipe (including downloading images). The hope is to add support for other CMS platforms with help from the community.

## Requirements
You will need [.Net Core 3.0 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.0). Recommend using Visual Studio 2019 ([Community edition is free](https://visualstudio.microsoft.com/vs/))

## Getting started
Clone this repo, open `DannyT.OrchardCoreMigrator.sln` and run to see an example WordPress export (from https://wptoorchardcoretest.wordpress.com) processed into a recipe. Create a new Orchard Core site or tenant (suggest using TheBlog theme) and import the recipe.

## Custom import
From the WordPress admin, select Tools > Export and choose `All Content`. You can pass the path to this file as an argument to the command line application (or add it to the project properties in Visual Studio under `Properties > Debug > Application arguments`.
