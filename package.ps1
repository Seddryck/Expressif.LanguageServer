$ErrorActionPreference = "Stop"

$publishPath = "./.publish"
if (Test-Path $publishPath) {
    Remove-Item -Recurse -Force $publishPath
}

$projects = @(
    "src/Expressif.LanguageServer.Core/Expressif.LanguageServer.Core.csproj",
    "src/Expressif.LanguageServer.Cli/Expressif.LanguageServer.Cli.csproj"
)
$frameworks = @("net8.0", "net9.0", "net10.0")
$runtimes = @("win-x64", "linux-x64")

foreach ($project in $projects) {
    foreach ($framework in $frameworks) {
        foreach ($runtime in $runtimes) {
            Write-Host "Building $project for $framework on $runtime ..."
            dotnet build $project -p:Version="$env:GitVersion_SemVer" -c Release -f $framework -r $runtime --nologo

            if ($LASTEXITCODE -ne 0) {
                throw "Build failed for $project ($framework, $runtime)."
            }

            if ($project -like "*Expressif.LanguageServer.Cli*") {
                $outputPath = "$publishPath/$framework/$runtime"
                Write-Host "Publishing $project for $framework on $runtime ..."
                dotnet publish $project -p:Version="$env:GitVersion_SemVer" -c Release -f $framework -r $runtime --no-self-contained -o $outputPath --no-build --nologo

                if ($LASTEXITCODE -ne 0) {
                    throw "Publish failed for $project ($framework, $runtime)."
                }

                $archivePath = "$publishPath/Expressif.LanguageServer-$env:GitVersion_SemVer-$framework-$runtime.zip"
                Compress-Archive -Path "$outputPath/*" -DestinationPath $archivePath -Force
            }
        }
    }

    Write-Host "Packaging NuGet project $project ..."
    dotnet pack $project -p:Version="$env:GitVersion_SemVer" -c Release --include-symbols --no-build --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Packaging failed for $project."
    }
}
