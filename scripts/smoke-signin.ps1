#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Origin = 'http://localhost:8080',
    [string]$EnvFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy/dev.env'),
    [string]$Project = 'inventory-and-order'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Net.Http

$settings = @{}
foreach ($line in Get-Content -Path $EnvFile) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') { $settings[$Matches[1]] = $Matches[2] }
}

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(30)
$script:jar = [ordered]@{}

function Assert-That([bool]$condition, [string]$message) {
    if (-not $condition) { throw "FAILED: $message" }
    Write-Output "ok: $message"
}

function Resolve-Url([string]$url) {
    if ($url -match '^https?://') { return [Uri]$url }
    return [Uri]::new([Uri]$Origin, $url)
}

function Invoke-Request([string]$method, [string]$url, [hashtable]$form = $null, [hashtable]$headers = @{}, [string]$cookieOverride = '') {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), (Resolve-Url $url))
    $cookie = if ($cookieOverride.Length -gt 0) { $cookieOverride } else { ($script:jar.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; ' }
    if ($cookie) { [void]$request.Headers.TryAddWithoutValidation('Cookie', $cookie) }
    foreach ($name in $headers.Keys) { [void]$request.Headers.TryAddWithoutValidation($name, $headers[$name]) }
    if ($null -ne $form) {
        $pairs = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, string]]]::new()
        foreach ($key in $form.Keys) { $pairs.Add([System.Collections.Generic.KeyValuePair[string, string]]::new($key, [string]$form[$key])) }
        $request.Content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    }

    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $values = $null
    if ($cookieOverride.Length -eq 0 -and $response.Headers.TryGetValues('Set-Cookie', [ref]$values)) {
        foreach ($setCookie in $values) {
            $pair = ($setCookie -split ';')[0]
            $name, $value = $pair -split '=', 2
            if ([string]::IsNullOrEmpty($value) -or $setCookie -match 'expires=Thu, 01 Jan 1970') { $script:jar.Remove($name) } else { $script:jar[$name] = $value }
        }
    }

    [pscustomobject]@{
        Status   = [int]$response.StatusCode
        Location = if ($response.Headers.Location) { $response.Headers.Location.ToString() } else { $null }
        Body     = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Uri      = $request.RequestUri
        Method   = $method
    }
}

function Get-HiddenFields([string]$html) {
    $fields = @{}
    foreach ($match in [regex]::Matches($html, '<input[^>]*>')) {
        $tag = $match.Value
        if ($tag -notmatch 'type="hidden"') { continue }
        $name = [regex]::Match($tag, 'name="([^"]+)"').Groups[1].Value
        $value = [regex]::Match($tag, 'value="([^"]*)"').Groups[1].Value
        if ($name) { $fields[$name] = [System.Net.WebUtility]::HtmlDecode($value) }
    }

    return $fields
}

function Invoke-Flow([string]$start, [string]$landing, [hashtable]$credentials, [switch]$Register) {
    $current = Invoke-Request 'GET' $start
    for ($hop = 0; $hop -lt 25; $hop++) {
        if ($current.Uri.AbsolutePath -eq $landing) { return $current }
        if ($current.Status -in 301, 302, 303, 307) {
            $current = Invoke-Request 'GET' ([Uri]::new($current.Uri, $current.Location).ToString())
            continue
        }

        if ($current.Status -ne 200) { throw "Unexpected $($current.Status) for $($current.Method) $($current.Uri.AbsolutePath): $($current.Body.Substring(0, [Math]::Min(400, $current.Body.Length)))" }
        $action = [regex]::Match($current.Body, '<form[^>]*action="([^"]+)"')
        $fields = Get-HiddenFields $current.Body
        if ($fields.ContainsKey('__RequestVerificationToken')) {
            if ($Register -and $current.Uri.AbsolutePath -eq '/account/login') {
                $link = [regex]::Match($current.Body, 'href="(/account/register[^"]*)"')
                if (-not $link.Success) { throw 'The storefront login page has no registration link.' }
                $current = Invoke-Request 'GET' ([System.Net.WebUtility]::HtmlDecode($link.Groups[1].Value))
                continue
            }

            foreach ($key in $credentials.Keys) { $fields[$key] = $credentials[$key] }
            $target = if ($action.Success) { [System.Net.WebUtility]::HtmlDecode($action.Groups[1].Value) } else { $current.Uri.PathAndQuery }
            $current = Invoke-Request 'POST' ([Uri]::new($current.Uri, $target).ToString()) $fields
            continue
        }

        if ($action.Success) {
            $current = Invoke-Request 'POST' ([Uri]::new($current.Uri, [System.Net.WebUtility]::HtmlDecode($action.Groups[1].Value)).ToString()) $fields
            continue
        }

        throw "No way forward at $($current.Uri): $($current.Body.Substring(0, [Math]::Min(300, $current.Body.Length)))"
    }

    throw "Too many hops from $start"
}

function Get-Json($response) { $response.Body | ConvertFrom-Json }

$tokenPattern = 'eyJ[A-Za-z0-9_-]{10,}\.'

$discovery = Get-Json (Invoke-Request 'GET' '/.well-known/openid-configuration')
$addresses = @($discovery.PSObject.Properties | Where-Object { $_.Value -is [string] -and $_.Value -match '^https?://' } | ForEach-Object { $_.Value })
Assert-That ($addresses.Count -gt 3 -and @($addresses | Where-Object { -not $_.StartsWith("$Origin/") }).Count -eq 0) 'the discovery document lists only the gateway origin'

$published = @(docker ps --filter "label=com.docker.compose.project=$Project" --format '{{.Names}} {{.Ports}}' | Where-Object { $_ -match '->8080/tcp' })
Assert-That ($published.Count -eq 1 -and $published[0] -match 'gateway') "only the gateway publishes an application port ($($published -join ', '))"

$spa = Invoke-Request 'GET' '/backoffice/orders'
Assert-That ($spa.Status -eq 200 -and $spa.Body -match '<base href="/backoffice/"') 'the backoffice app is served through the gateway'

$landed = Invoke-Flow '/bff/backoffice/login' '/backoffice/' @{ Email = $settings['DEMO_ADMIN_EMAIL']; Password = $settings['DEMO_ADMIN_PASSWORD'] }
Assert-That ($landed.Uri.AbsolutePath -eq '/backoffice/' -and $landed.Uri.Query -eq '') "backoffice sign-in lands on /backoffice/ ($($landed.Uri.PathAndQuery))"
Assert-That ($script:jar.Contains('bff-backoffice')) "the browser holds the backoffice session cookie ($($script:jar.Keys -join ', '))"

$userResponse = Invoke-Request 'GET' '/bff/backoffice/user'
Assert-That ($userResponse.Status -eq 200 -and $userResponse.Body -notmatch $tokenPattern) 'the user endpoint answers without tokens'
$user = Get-Json $userResponse
Assert-That ($user.userType -eq 'staff' -and $user.email -eq $settings['DEMO_ADMIN_EMAIL'] -and $user.tenantId) 'the user is the demo tenant admin'

$api = Invoke-Request 'GET' '/api/identity/users' $null @{ 'X-Bff-App' = 'backoffice' }
Assert-That ($api.Status -eq 200) "an API call through the gateway succeeds ($($api.Status))"
$noHeader = Invoke-Request 'GET' '/api/identity/users'
Assert-That ($noHeader.Status -eq 400) 'an API call without the app header is rejected'

$sessionCookie = ($script:jar.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; '
$signedOut = Invoke-Flow $user.logoutUrl '/backoffice/' @{}
Assert-That ($signedOut.Uri.AbsolutePath -eq '/backoffice/') 'sign-out lands on /backoffice/'
$old = Invoke-Request 'GET' '/bff/backoffice/user' $null @{} $sessionCookie
Assert-That ($old.Status -eq 401) 'the old session cookie is refused after sign-out'

$script:jar.Clear()
$slug = $settings['DEMO_TENANT_SLUG']
$email = "smoke-$([guid]::NewGuid().ToString('N').Substring(0, 8))@mail.test"
$password = 'Smoke#Pass1'
$shop = Invoke-Flow "/bff/shop/$slug/login" "/shop/$slug/" @{ Email = $email; Password = $password; ConfirmPassword = $password } -Register
Assert-That ($shop.Uri.AbsolutePath -eq "/shop/$slug/") 'storefront registration lands in the demo shop'
$customer = Get-Json (Invoke-Request 'GET' "/bff/shop/$slug/user")
Assert-That ($customer.userType -eq 'customer' -and $customer.email -eq $email -and $customer.slug -eq $slug) 'the storefront user is the new customer'

Write-Output 'Sign-in smoke passed.'
