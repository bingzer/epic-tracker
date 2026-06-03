$root = Split-Path $PSScriptRoot -Parent
Start-Process "dotnet" -ArgumentList "`"$root\publish\api\EpicTracker.Api.dll`"" -WorkingDirectory "$root\publish\api" -WindowStyle Hidden
