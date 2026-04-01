$ErrorActionPreference = 'Stop'

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory
$tailwindWorkspace = Join-Path $repositoryRoot 'Tailwind'

if (-not (Test-Path (Join-Path $tailwindWorkspace 'package.json'))) {
    throw "Tailwind workspace was not found at '$tailwindWorkspace'."
}

$tailwindCli = Join-Path $tailwindWorkspace 'node_modules\.bin\tailwindcss.cmd'
if (-not (Test-Path $tailwindCli)) {
    Write-Host 'Installing Tailwind workspace dependencies...'
    Push-Location $tailwindWorkspace
    try {
        npm install
    }
    finally {
        Pop-Location
    }
}

Push-Location $repositoryRoot
try {
    npm run tailwind:watch
}
finally {
    Pop-Location
}
