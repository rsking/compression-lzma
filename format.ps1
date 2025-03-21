dotnet format whitespace $PSScriptRoot\src --report $PSScriptRoot\whitespace.report.json --verbosity diagnostic
dotnet format style $PSScriptRoot\src --report $PSScriptRoot\style.report.json --severity info --verbosity diagnostic
dotnet format analyzers $PSScriptRoot\src --report $PSScriptRoot\analyzers.report.json --severity info --verbosity diagnostic --exclude-diagnostics S1133