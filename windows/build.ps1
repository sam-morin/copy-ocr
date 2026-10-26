# Builds a standalone CopyOCR.exe into windows\dist (needs the .NET 8 SDK: https://dot.net)
dotnet publish "$PSScriptRoot\CopyOCR.csproj" -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o "$PSScriptRoot\dist"
