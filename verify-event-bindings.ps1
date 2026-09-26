<#
    .SYNOPSIS
    Builds and runs the event bindings' tests - C, C++, Java and Python -
    against the runtime's built library.

    .DESCRIPTION
    Each binding of xmip_operate.h section 11 (ADR-0065) has one test that
    subscribes, publishes through xmip_event_publish_v1, receives the Event
    by next and by callback, and holds publish to receive to a millisecond
    beside a plain thread wake measured under the same load. C and C++ build
    with zig (prerequisite.toml, c), Java with the JDK's javac (FFM is a
    preview API in 21, so --release 21 --enable-preview), Python with
    python. Build output goes to build/, which git ignores; each test audits
    into a fresh folder under the system's temporary directory, removed
    afterwards, never the operating system's log. Exit 0 when every test
    passed.

    .PARAMETER Library
    The runtime's library, xmip_core_runtime. Defaults to
    XMIP_RUNTIME_LIBRARY, then to the estate's build of
    module/platform/runtime, whichever of release and debug was built last.

    .PARAMETER Language
    Which bindings to test; all four by default.

    .EXAMPLE
    ./verify-event-bindings.ps1 -Language C, Python
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $Library = $env:XMIP_RUNTIME_LIBRARY,

    [Parameter()]
    [ValidateSet('C', 'Cpp', 'Java', 'Python')]
    [string[]] $Language = @('C', 'Cpp', 'Java', 'Python')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-XmipRuntimeLibrary {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    [string] $name = 'libxmip_core_runtime.so'
    if ($IsWindows) {
        $name = 'xmip_core_runtime.dll'
    }
    elseif ($IsMacOS) {
        $name = 'libxmip_core_runtime.dylib'
    }
    [string] $target = Join-Path $PSScriptRoot '..' '..' 'platform' 'runtime' 'target'
    # The newer of the two builds: a stale release lacks what debug has.
    $built = @(
        foreach ($flavor in @('release', 'debug')) {
            Get-Item -LiteralPath (Join-Path $target $flavor $name) -ErrorAction SilentlyContinue
        }
    ) | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($built) {
        return $built.FullName
    }
    return ''
}

function New-XmipAuditDirectory {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $Name
    )

    [string] $folder = "xmip-event-$Name-$PID-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    [string] $path = Join-Path ([System.IO.Path]::GetTempPath()) $folder
    if ($PSCmdlet.ShouldProcess($path, 'Create audit directory')) {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
    return $path
}

function Invoke-XmipBindingTest {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [scriptblock] $Build,

        [Parameter(Mandatory)]
        [scriptblock] $Run
    )

    Write-Host "== $Name"
    & $Build 2>&1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED. $Name did not build."
        return $false
    }
    [string] $audit = New-XmipAuditDirectory -Name $Name.ToLowerInvariant()
    try {
        [System.Diagnostics.Stopwatch] $clock = [System.Diagnostics.Stopwatch]::StartNew()
        & $Run $audit | ForEach-Object { Write-Host $_ }
        [int] $code = $LASTEXITCODE
        Write-Host "   ($Name ran in $($clock.ElapsedMilliseconds) ms)"
    }
    finally {
        Remove-Item -LiteralPath $audit -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($code -ne 0) {
        Write-Host "FAILED. $Name exited $code."
        return $false
    }
    Write-Host "OK. $Name"
    return $true
}

if ([string]::IsNullOrWhiteSpace($Library)) {
    $Library = Find-XmipRuntimeLibrary
}
if ([string]::IsNullOrWhiteSpace($Library) -or -not (Test-Path -LiteralPath $Library)) {
    Write-Host 'FAILED. No runtime library; build module/platform/runtime or pass -Library.'
    exit 2
}
$Library = (Resolve-Path -LiteralPath $Library).Path
Write-Host "   runtime library: $Library"

Set-Location -LiteralPath $PSScriptRoot
[string] $include = Join-Path $PSScriptRoot 'include'
[string] $build = Join-Path $PSScriptRoot 'build'
[string] $exe = ''
if ($IsWindows) {
    $exe = '.exe'
}
[string[]] $warnings = @('-Wall', '-Wextra', '-Werror')
[string[]] $failed = @()

if ($Language -contains 'C') {
    [string] $out = Join-Path $build 'c'
    [bool] $ok = Invoke-XmipBindingTest -Name 'C' -Build {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        [string[]] $c = @('cc', '-std=c11', '-O2') + $warnings + @('-I', $include, '-I', 'c')
        & zig @c 'c/xmip_event_test.c' '-o' (Join-Path $out "xmip_event_test$exe")
        if ($LASTEXITCODE -eq 0) {
            & zig @c 'c/xmip_event_example.c' '-o' (Join-Path $out "xmip_event_example$exe")
        }
    } -Run {
        param($audit)
        & (Join-Path $out "xmip_event_example$exe") $Library $audit
        if ($LASTEXITCODE -eq 0) {
            & (Join-Path $out "xmip_event_test$exe") $Library $audit
        }
    }
    if (-not $ok) {
        $failed += 'C'
    }
}

if ($Language -contains 'Cpp') {
    [string] $out = Join-Path $build 'cpp'
    [bool] $ok = Invoke-XmipBindingTest -Name 'Cpp' -Build {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        [string[]] $cpp = @('c++', '-std=c++17', '-O2') + $warnings + @('-I', $include)
        & zig @cpp 'cpp/xmip_event_test.cpp' '-o' (Join-Path $out "xmip_event_test$exe")
    } -Run {
        param($audit)
        & (Join-Path $out "xmip_event_test$exe") $Library $audit
    }
    if (-not $ok) {
        $failed += 'Cpp'
    }
}

if ($Language -contains 'Java') {
    [string] $bin = ''
    if ($env:JAVA_HOME -and (Test-Path -LiteralPath $env:JAVA_HOME)) {
        $bin = Join-Path $env:JAVA_HOME 'bin'
    }
    [string] $javac = if ($bin) { Join-Path $bin 'javac' } else { 'javac' }
    [string] $java = if ($bin) { Join-Path $bin 'java' } else { 'java' }
    [string] $out = Join-Path $build 'java'
    [bool] $ok = Invoke-XmipBindingTest -Name 'Java' -Build {
        Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction SilentlyContinue
        [string[]] $sources = (Get-ChildItem -Path java -Recurse -Filter '*.java').FullName
        & $javac --release 21 --enable-preview '-Xlint:all,-preview' -Werror -d $out @sources
    } -Run {
        param($audit)
        & $java --enable-preview --enable-native-access=ALL-UNNAMED -cp $out `
            se.xmip.event.EventBindingTest $Library $audit $include
    }
    if (-not $ok) {
        $failed += 'Java'
    }
}

if ($Language -contains 'Python') {
    [bool] $ok = Invoke-XmipBindingTest -Name 'Python' -Build {
        & python -c 'import sys; sys.exit(sys.version_info < (3, 10))'
    } -Run {
        param($audit)
        $env:XMIP_RUNTIME_LIBRARY = $Library
        $env:XMIP_EVENT_AUDIT_DIRECTORY = $audit
        $env:PYTHONDONTWRITEBYTECODE = '1'
        Push-Location -LiteralPath (Join-Path $PSScriptRoot 'python')
        try {
            & python -m unittest -v test_xmip_event 2>&1
        }
        finally {
            Pop-Location
        }
    }
    if (-not $ok) {
        $failed += 'Python'
    }
}

if ($failed.Count -gt 0) {
    Write-Host "FAILED. $($failed -join ', ')"
    exit 1
}
Write-Host "OK. $($Language -join ', ')"
exit 0
