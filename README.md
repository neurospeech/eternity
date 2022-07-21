[![.NET](https://github.com/neurospeech/eternitiy/actions/workflows/dotnet.yml/badge.svg)](https://github.com/neurospeech/eternitiy/actions/workflows/dotnet.yml)

# Eternity Framework

Long running workflows with ability to suspend and replay the workflow in future.

## NuGet
| Name                                               | Package                                                                                                                                                        |
|----------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| NeuroSpeech.Eternity                               | [![NuGet](https://img.shields.io/nuget/v/NeuroSpeech.Eternity.svg?label=NuGet)](https://www.nuget.org/packages/NeuroSpeech.Eternity)                           |
| NeuroSpeech.Eternity.DependencyInjectionExtensions | [![NuGet](https://img.shields.io/nuget/v/NeuroSpeech.Eternity.DependencyInjectionExtensions.svg?label=NuGet)](https://www.nuget.org/packages/NeuroSpeech.Eternity.DependencyInjectionExtensions) |
| NeuroSpeech.Eternity.SqliteStorage                 | [![NuGet](https://img.shields.io/nuget/v/NeuroSpeech.Eternity.SqliteStorage.svg?label=NuGet)](https://www.nuget.org/packages/NeuroSpeech.Eternity.SqliteStorage) |
| NeuroSpeech.Eternity.SqlStorage                 | [![NuGet](https://img.shields.io/nuget/v/NeuroSpeech.Eternity.SqlStorage.svg?label=NuGet)](https://www.nuget.org/packages/NeuroSpeech.Eternity.SqlStorage) |
| NeuroSpeech.Eternity.Mocks                         | [![NuGet](https://img.shields.io/nuget/v/NeuroSpeech.Eternity.Mocks.svg?label=NuGet)](https://www.nuget.org/packages/NeuroSpeech.Eternity.Mocks)               |


## Features
1. Strongly typed API
2. Mobile Ready - Storage is abstract and does support running workflows in Mobile Devices.
3. Dependency Injection - easy integration with Microsoft Dependency Injection Extensions
4. Activities are simple public virtual C# methods
5. Activities can be scheduled to be called in future
6. Support for external events, workflow can wait for external events
7. Really very large workflow supports, duration of waiting can have timeout for days/months/weeks. This allows in creating workflow for monthly/yearly memberships.
8. Abstract Storage - you can create your own storage, in memory Mock storage, Sqlite Storage (for mobile) and Sql Server Storage is included.
9. Unit testable - Mocks package contains useful mocks to unit test your workflows.
10. Support for non deterministic workflows, activities are isolated by parameters and time of execution, (you can also make Activity unique) so same activity method with same parameter anywhere in the workflow will execute only once and will give same result.
11. Workflow can wait for multiple events, and when you raise an event, it will optionally throw an exception if workflow is not waiting.
12. You can wait for events for days/months. It does not occupy any resources, waiting occurs in queue, and workers do not stay busy while waiting.
13. .NET Standard 2.0 support, it means it can run anywhere without any native/local dependency.

## Why did we remove Azure Table Storage?

1. Table Storage is very expensive, instead, using Sql Azure is cheaper as it does not charge per transaction.
2. Table Storage Key has restrictions on ID, it needs URL escaping. Sql Azure has no such restriction.
3. You can easily query and view table, there is only single table named "EternityEntities".

# Documentation
1. [Home](https://github.com/neurospeech/eternity/wiki)
2. [Getting Started](https://github.com/neurospeech/eternity/wiki/Getting-Started)
3. [Samples](https://github.com/neurospeech/eternity/wiki/Samples)
4. [Child Workflow Sample](https://github.com/neurospeech/eternity/wiki/Sample-Child-Workflows)
5. [Unit Testing](https://github.com/neurospeech/eternity/wiki/Unit-Testing)

## Other Interesting Projects by NeuroSpeech
1. [YantraJS - JavaScript engine for .NET with latest features](https://github.com/yantrajs/yantra)
2. [Automatic Migrations for EF Core](https://github.com/neurospeech/ef-core-automatic-migration)
