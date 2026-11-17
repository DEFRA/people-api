[CmdletBinding()]
param(
    [string] $BicepPath = 'bicep',
    [string] $FoundationParameters,
    [string] $ApplicationParameters
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Condition([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Get-Ipv4Range([string] $Cidr) {
    Assert-Condition ($Cidr -match '^\d{1,3}(\.\d{1,3}){3}/\d{1,2}$') 'Expected a canonical IPv4 CIDR.'
    $parts = $Cidr.Split('/')
    $ip = [System.Net.IPAddress]::Parse($parts[0])
    $bits = [int] $parts[1]
    Assert-Condition ($bits -ge 8 -and $bits -le 29) 'IPv4 CIDR prefix must be between /8 and /29.'
    $octets = $ip.GetAddressBytes()
    [long] $number = ([long] $octets[0] * 16777216) + ([long] $octets[1] * 65536) + ([long] $octets[2] * 256) + $octets[3]
    [long] $size = [math]::Pow(2, 32 - $bits)
    Assert-Condition (($number % $size) -eq 0) 'CIDR must identify the network address, not a host address.'
    @{ First = $number; Last = $number + $size - 1; Bits = $bits }
}

function Test-Network([hashtable] $Values) {
    $vnet = Get-Ipv4Range $Values.vnetCidr
    $aca = Get-Ipv4Range $Values.acaSubnetCidr
    $pe = Get-Ipv4Range $Values.privateEndpointSubnetCidr
    Assert-Condition ($aca.Bits -le 27) 'ACA workload-profile subnet must be /27 or larger.'
    foreach ($subnet in @($aca, $pe)) {
        Assert-Condition ($subnet.First -ge $vnet.First -and $subnet.Last -le $vnet.Last) 'Both subnets must be inside the VNet.'
    }
    Assert-Condition ($aca.Last -lt $pe.First -or $pe.Last -lt $aca.First) 'ACA and private-endpoint subnets must not overlap.'
    foreach ($range in @($vnet, $aca, $pe)) {
        $private = ($range.First -ge 167772160 -and $range.Last -le 184549375) -or
            ($range.First -ge 2886729728 -and $range.Last -le 2887778303) -or
            ($range.First -ge 3232235520 -and $range.Last -le 3232301055)
        Assert-Condition $private 'Use explicitly approved RFC1918 IPv4 address ranges.'
        foreach ($reserved in @((Get-Ipv4Range '172.30.0.0/16'), (Get-Ipv4Range '172.31.0.0/16'))) {
            Assert-Condition ($range.Last -lt $reserved.First -or $reserved.Last -lt $range.First) 'Network overlaps an ACA/AKS reserved range.'
        }
    }
}

function Test-Common([hashtable] $Values) {
    Assert-Condition ($Values.prefix -cmatch '^[a-z0-9]{2,6}$') 'prefix must be 2-6 lowercase alphanumeric characters.'
    Assert-Condition ($Values.environmentName -cmatch '^[a-z0-9]{2,3}$') 'environmentName must be 2-3 lowercase alphanumeric characters.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($Values.location)) 'location must be explicitly supplied.'
}

function Test-Foundation([hashtable] $Values) {
    Test-Common $Values
    Test-Network $Values
    Assert-Condition (-not $Values.acrZoneRedundant -or $Values.acrSku -eq 'Premium') 'ACR zone redundancy requires Premium.'
}

function Test-Application([hashtable] $Values) {
    Test-Common $Values
    Assert-Condition ($Values.minReplicas -le $Values.maxReplicas) 'Minimum replicas exceed maximum replicas.'
    Assert-Condition ($Values.imageDigest -cmatch '^sha256:[0-9a-f]{64}$') 'Expected an immutable lowercase SHA-256 image digest.'
    Assert-Condition ($Values.imageRepository -cmatch '^[a-z0-9]+([._/-][a-z0-9]+)*$') 'Invalid registry image repository.'
    foreach ($name in @('entraTenantId', 'entraClientId', 'graphApiClientId', 'graphApiTenantId')) {
        $id = [guid]::Empty
        Assert-Condition ([guid]::TryParse($Values[$name], [ref] $id) -and $id -ne [guid]::Empty) "Expected a nonempty GUID for $name."
    }
    foreach ($name in @('peopleReadRole', 'peopleAdminRole')) {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($Values[$name]) -and $Values[$name] -notmatch '\s') "Expected an explicit whitespace-free value for $name."
    }
    Assert-Condition ($Values.peopleReadRole -cne $Values.peopleAdminRole) 'peopleReadRole and peopleAdminRole must be distinct Entra app-role values.'
    Assert-Condition ($Values.graphApiScopes -is [array] -and $Values.graphApiScopes.Count -ge 1) 'graphApiScopes must list at least one Microsoft Graph application permission.'
    # graphApiClientSecretName may legitimately be an explicit empty string (passwordless, managed-identity Graph auth - the recommended default) or a Key Vault secret name.
    Assert-Condition ([string]::IsNullOrEmpty($Values.graphApiClientSecretName) -or $Values.graphApiClientSecretName -match '^[A-Za-z0-9-]{1,127}$') 'Invalid Key Vault secret name for graphApiClientSecretName.'
    Assert-Condition ($Values.corsAllowedOrigins -is [array]) 'corsAllowedOrigins must be an array (may be empty - CORS stays denied by default).'
    foreach ($origin in $Values.corsAllowedOrigins) {
        Assert-Condition ($origin -match '^https://[A-Za-z0-9.-]+(:[0-9]{1,5})?$') "Invalid CORS origin: $origin."
    }
}

function Read-ParameterValues([string] $Path, [hashtable] $Template) {
    $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    Assert-Condition ($document.ContainsKey('parameters') -and $document.parameters -is [hashtable]) 'Expected an ARM deployment-parameters JSON file.'
    $values = @{}
    foreach ($name in $Template.parameters.Keys) {
        $schema = $Template.parameters[$name]
        if (-not $document.parameters.ContainsKey($name)) {
            Assert-Condition ($schema.ContainsKey('defaultValue')) "Missing required parameter: $name."
            $values[$name] = $schema.defaultValue
            continue
        }
        $entry = $document.parameters[$name]
        Assert-Condition ($entry -is [hashtable] -and $entry.ContainsKey('value') -and $entry.Count -eq 1) "Expected a value for parameter: $name."
        $value = $entry.value
        $expectedType = if ($schema.ContainsKey('type')) { $schema.type } else { 'object' }
        $correctType = switch ($expectedType) {
            'string' { $value -is [string] }
            'int' { $value -is [long] -or $value -is [int] }
            'bool' { $value -is [bool] }
            'array' { $value -is [array] }
            'object' { $value -is [hashtable] }
            default { throw "Unsupported parameter type for $name." }
        }
        Assert-Condition $correctType "Wrong parameter type: $name."
        if ($schema.ContainsKey('allowedValues')) {
            Assert-Condition ($value -cin $schema.allowedValues) "Unsupported parameter value: $name."
        }
        foreach ($bound in @('minValue', 'maxValue', 'minLength', 'maxLength')) {
            if (-not $schema.ContainsKey($bound)) { continue }
            $measurement = if ($bound.EndsWith('Length')) { $value.Length } else { $value }
            $passes = if ($bound.StartsWith('min')) { $measurement -ge $schema[$bound] } else { $measurement -le $schema[$bound] }
            Assert-Condition $passes "Parameter outside $bound constraint: $name."
        }
        $values[$name] = $value
    }
    foreach ($name in $document.parameters.Keys) {
        Assert-Condition ($Template.parameters.ContainsKey($name)) "Unknown parameter: $name."
    }
    $values
}

function Expect-Failure([scriptblock] $Action, [string] $Message) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    Assert-Condition $failed $Message
}

Get-Command $BicepPath -ErrorAction Stop | Out-Null
$temporaryFiles = [System.Collections.Generic.List[string]]::new()
try {
    $templates = @{}
    foreach ($file in @('main', 'app')) {
        $output = Join-Path ([System.IO.Path]::GetTempPath()) "people-api-$file-$([guid]::NewGuid().ToString('N')).json"
        $temporaryFiles.Add($output)
        & $BicepPath build (Join-Path $PSScriptRoot "$file.bicep") --outfile $output
        Assert-Condition ($LASTEXITCODE -eq 0) "Bicep compilation failed: $file."
        $templates[$file] = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json -AsHashtable
    }

    $syntheticNetwork = @{ vnetCidr = '10.60.0.0/16'; acaSubnetCidr = '10.60.0.0/27'; privateEndpointSubnetCidr = '10.60.1.0/28' }
    Test-Network $syntheticNetwork
    $overlap = $syntheticNetwork.Clone()
    $overlap.privateEndpointSubnetCidr = '10.60.0.0/28'
    Expect-Failure { Test-Network $overlap } 'Overlapping subnets were accepted.'
    $small = $syntheticNetwork.Clone()
    $small.acaSubnetCidr = '10.60.0.0/28'
    Expect-Failure { Test-Network $small } 'An undersized ACA subnet was accepted.'
    $outside = $syntheticNetwork.Clone()
    $outside.privateEndpointSubnetCidr = '10.61.0.0/28'
    Expect-Failure { Test-Network $outside } 'A subnet outside the VNet was accepted.'
    Expect-Failure { Get-Ipv4Range '10.60.0.1/27' } 'A host-address CIDR was accepted.'
    Expect-Failure { Get-Ipv4Range 'not-a-cidr' } 'A malformed CIDR was accepted.'
    $reservedNetwork = @{ vnetCidr = '172.30.0.0/16'; acaSubnetCidr = '172.30.0.0/27'; privateEndpointSubnetCidr = '172.30.1.0/28' }
    Expect-Failure { Test-Network $reservedNetwork } 'An ACA-reserved network was accepted.'

    $syntheticFoundation = $syntheticNetwork + @{
        prefix = 'test'
        environmentName = 't01'
        location = 'synthetic-not-a-deployment'
        acrZoneRedundant = $false
        acrSku = 'Basic'
    }
    Test-Foundation $syntheticFoundation
    $badRegistry = $syntheticFoundation.Clone()
    $badRegistry.acrZoneRedundant = $true
    Expect-Failure { Test-Foundation $badRegistry } 'Basic ACR with zone redundancy was accepted.'

    $syntheticApp = @{
        prefix = 'test'
        environmentName = 't01'
        location = 'synthetic-not-a-deployment'
        imageRepository = 'test/application'
        imageDigest = 'sha256:' + ('a' * 64)
        minReplicas = 0
        maxReplicas = 1
        entraTenantId = '00000000-0000-0000-0000-000000000001'
        entraClientId = '00000000-0000-0000-0000-000000000002'
        peopleReadRole = 'Test.PeopleRead'
        peopleAdminRole = 'Test.PeopleAdmin'
        graphApiClientId = '00000000-0000-0000-0000-000000000003'
        graphApiTenantId = '00000000-0000-0000-0000-000000000001'
        graphApiScopes = @('User.Read.All')
        graphApiClientSecretName = ''
        corsAllowedOrigins = @()
    }
    Test-Application $syntheticApp
    $badScale = $syntheticApp.Clone()
    $badScale.minReplicas = 2
    Expect-Failure { Test-Application $badScale } 'Minimum replicas above maximum were accepted.'
    $badImage = $syntheticApp.Clone()
    $badImage.imageDigest = 'latest'
    Expect-Failure { Test-Application $badImage } 'A mutable image tag was accepted as a digest.'
    $badTenant = $syntheticApp.Clone()
    $badTenant.entraTenantId = [guid]::Empty.ToString()
    Expect-Failure { Test-Application $badTenant } 'An empty application tenant was accepted.'
    $duplicateRole = $syntheticApp.Clone()
    $duplicateRole.peopleAdminRole = $duplicateRole.peopleReadRole
    Expect-Failure { Test-Application $duplicateRole } 'Duplicate Entra app-role values were accepted.'
    $noScopes = $syntheticApp.Clone()
    $noScopes.graphApiScopes = @()
    Expect-Failure { Test-Application $noScopes } 'An empty Graph scopes list was accepted.'
    $badOrigin = $syntheticApp.Clone()
    $badOrigin.corsAllowedOrigins = @('not-a-url')
    Expect-Failure { Test-Application $badOrigin } 'An invalid CORS origin was accepted.'
    $withSecret = $syntheticApp.Clone()
    $withSecret.graphApiClientSecretName = 'test-secret-name'
    Test-Application $withSecret

    foreach ($template in $templates.Values) {
        foreach ($schema in $template.parameters.Values) {
            Assert-Condition (-not $schema.ContainsKey('defaultValue')) 'An entry-point parameter has an unapproved default.'
        }
        foreach ($name in $template.outputs.Keys) {
            Assert-Condition ($name -notmatch '(?i)password|accesskey|connectionstring|secretvalue') 'An entry point exposes a credential-shaped output.'
        }
    }

    $topLevelFoundationResources = if ($templates.main.resources -is [hashtable]) { @($templates.main.resources.Values) } else { @($templates.main.resources) }
    $vaultModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and
        $_.properties.parameters.ContainsKey('enableVaultForDeployment')
    })
    Assert-Condition ($vaultModule.Count -eq 1) 'Expected the foundation Key Vault module.'
    Assert-Condition (-not $vaultModule[0].properties.parameters.ContainsKey('keys')) 'Foundation must not ask the AVM module to create keys.'
    Assert-Condition ($vaultModule[0].properties.parameters.publicNetworkAccess.value -ceq 'Disabled') 'Key Vault public access was enabled.'
    Assert-Condition ($vaultModule[0].properties.parameters.networkAcls.value.bypass -ceq 'None') 'Key Vault trusted-service bypass was enabled.'

    $foundation = $null
    if ($FoundationParameters) {
        $foundation = Read-ParameterValues $FoundationParameters $templates.main
        Test-Foundation $foundation
        Write-Output 'Foundation parameter structure and local constraints passed.'
    }
    if ($ApplicationParameters) {
        $application = Read-ParameterValues $ApplicationParameters $templates.app
        Test-Application $application
        if ($foundation) {
            foreach ($name in @('location', 'prefix', 'environmentName')) {
                Assert-Condition ($foundation[$name] -ceq $application[$name]) "Foundation and application disagree on $name."
            }
        }
        Write-Output 'Application parameter structure and local constraints passed.'
    }
    Write-Output 'Both Bicep entry points compile; network/cross-field scenarios and no-default/no-credential-output checks passed. No Azure operations performed.'
}
finally {
    foreach ($file in $temporaryFiles) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
}
