$ErrorActionPreference = "Stop"

function Invoke-Tool {
  param(
    [string]$FilePath,
    [string[]]$Arguments
  )

  $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
  if ($null -eq $process -or $process.ExitCode -ne 0)
  {
    $argsString = $Arguments -join " "
    throw "Command failed: $FilePath $argsString (exit code: $($process.ExitCode))"
  }
}

$Location = "$PSScriptRoot/.."
Push-Location $Location

Write-Host "Running from ${Location}"

$OutputDirectory = Join-Path $args[0] "Shaders"
Write-Host "Compiling to ${OutputDirectory}"

if (Test-Path $OutputDirectory)
{
  Remove-Item -Path $OutputDirectory -Force -Recurse
}
New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null

$Shaders = Get-ChildItem -Path "Shaders" -Filter "*.metal" | Select-Object -ExpandProperty Name
foreach ($Shader in $Shaders)
{
  $shaderCompileArgs = @("-sdk", "macosx", "metal", "-o", "${OutputDirectory}/${Shader}.air", "-c", "Shaders/${Shader}", "-frecord-sources", "-gline-tables-only")
  Invoke-Tool -FilePath "xcrun" -Arguments $shaderCompileArgs
}

$AirFiles = Get-ChildItem -Path $OutputDirectory -Filter "*.air" | Select-Object -ExpandProperty FullName

$metallibArgs = @("-sdk", "macosx", "metallib", "-o", "${OutputDirectory}/Output.metallib") + $AirFiles
Invoke-Tool -FilePath "xcrun" -Arguments $metallibArgs

Pop-Location
