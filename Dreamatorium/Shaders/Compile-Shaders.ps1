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
  Start-Process -FilePath "xcrun" -ArgumentList $shaderCompileArgs -Wait
}

$AirFiles = Get-ChildItem -Path $OutputDirectory -Filter "*.air" | Select-Object -ExpandProperty FullName

$metallibArgs = @("-sdk", "macosx", "metallib", "-o", "${OutputDirectory}/Output.metallib") + $AirFiles
Start-Process -FilePath "xcrun" -ArgumentList $metallibArgs

Pop-Location