<#
    .SYNOPSIS
    Builds and runs the event bindings' tests - C, C++, Java and Python -
    against the runtime's built library.

    .DESCRIPTION
    Each binding of xmip_operate.h section 11 (ADR-0065) has one test that
    proves a program subscribing names no Party of its own: the subscriber
    is the account the process runs as, resolved through the Parties of the
    node serving in it (ADR-0019, amendment 2026-11-11), and no node serves
    in a test's process, so subscribe and listen are refused XMIP_E_AUTH
    with the sentence; publish through xmip_event_publish_v1, the policy and
    who is unheard cross. C's example runs too, and says the refusal where
    no node serves. C and C++ build
    with the compiler Rust's toolchain already requires (prerequisite.toml,
    linker): the MSVC build tools' cl.exe on Windows, cc and c++ elsewhere.
    Java builds with the JDK's javac (--release 27, where FFM is final),
    Python 3.15 runs with python. Build output goes to build/, which git
    ignores; each test audits into a fresh folder under the system's
    temporary directory, removed afterwards, never the operating system's
    log. Exit 0 when every test that ran passed.

    Every language but Rust is optional for Xmip itself (the owner,
    2026-10-20: *every language except Rust is optional*), so each tool is
    an optional prerequisite (prerequisite.toml): a binding whose tool is
    absent here, or older than its floor, is SKIPPED and said so in words —
    "SKIPPED: Java not installed; the Java Event binding is not verified" —
    and fails nothing.

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

$script:XmipCompilerEnvironment = $null

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
    <#
        .SYNOPSIS
        A fresh, empty folder for one binding's audit records, made by
        PowerShell's New-TemporaryDirectory; the caller removes it.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return (New-TemporaryDirectory).FullName
}

function Get-XmipCompilerEnvironment {
    <#
        .SYNOPSIS
        The environment cl.exe compiles in, read once from the MSVC build
        tools Rust's msvc target links with; empty when they are absent.

        .DESCRIPTION
        vswhere.exe, at the path the Visual Studio Installer documents, names
        the newest installation with the C and C++ tools for this machine;
        its VsDevCmd.bat sets PATH, INCLUDE and LIB for its tools and Windows
        SDK, without which cl.exe finds no header. VsDevCmd calls vswhere by
        name, so its folder goes on the path VsDevCmd runs with.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    if ($null -ne $script:XmipCompilerEnvironment) {
        return $script:XmipCompilerEnvironment
    }
    $script:XmipCompilerEnvironment = @{}
    [string] $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio' 'Installer'
    [bool] $arm = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64'
    [string] $arch = if ($arm) { 'arm64' } else { 'amd64' }
    [string[]] $find = @('-latest', '-products', '*', '-property', 'installationPath', '-requires',
        "Microsoft.VisualStudio.Component.VC.Tools.$(if ($arm) { 'ARM64' } else { 'x86.x64' })")
    [string] $install = ''
    if (Test-Path -LiteralPath (Join-Path $installer 'vswhere.exe')) {
        $install = & (Join-Path $installer 'vswhere.exe') @find
    }
    if ($install) {
        [string] $devcmd = "`"$(Join-Path $install 'Common7' 'Tools' 'VsDevCmd.bat')`" -no_logo"
        [string] $setup = "$devcmd -arch=$arch -host_arch=$arch >nul && set"
        foreach ($line in (& cmd.exe /d /c "set `"PATH=$installer;%PATH%`" && $setup")) {
            if ($LASTEXITCODE -eq 0 -and $line -match '^([^=]+)=(.*)$') {
                $script:XmipCompilerEnvironment[$Matches[1]] = $Matches[2]
            }
        }
    }
    return $script:XmipCompilerEnvironment
}

function Invoke-XmipCompiler {
    <#
        .SYNOPSIS
        Compiles one C or C++ source into a program with the compiler Rust
        requires, cl.exe on Windows, cc and c++ elsewhere: the binding's
        standard, optimized, every warning in its own code an error.

        .DESCRIPTION
        /W4 is cl's -Wall -Wextra, /external:W0 leaves the system headers'
        warnings out as cc does; C11's <stdatomic.h> still needs
        /experimental:c11atomics, C++ /EHsc, and _CRT_SECURE_NO_WARNINGS
        keeps the standard getenv from being an error.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('C', 'Cpp')]
        [string] $Name,

        [Parameter(Mandatory)]
        [string] $Source,

        [Parameter(Mandatory)]
        [string] $Directory,

        [Parameter()]
        [string[]] $Include = @()
    )

    [bool] $c = $Name -eq 'C'
    [string] $output = Join-Path $Directory ([System.IO.Path]::GetFileNameWithoutExtension($Source))
    [string] $option = if ($IsWindows) { '/I' } else { '-I' }
    [string[]] $paths = @('include') + $Include | ForEach-Object { $option, $_ }
    if (-not $IsWindows) {
        [string[]] $flags = @('-O2', '-Wall', '-Wextra', '-Werror', '-pthread')
        [string] $standard = if ($c) { '-std=c11' } else { '-std=c++17' }
        [string] $compiler = if ($c) { 'cc' } else { 'c++' }
        & $compiler $standard @flags @paths $Source '-o' $output
        return
    }
    [string[]] $flags = @('/nologo', '/O2', '/W4', '/WX', '/external:anglebrackets',
        '/external:W0', '/D_CRT_SECURE_NO_WARNINGS')
    $flags += if ($c) { '/std:c11', '/experimental:c11atomics' } else { '/std:c++17', '/EHsc' }
    [hashtable] $environment = Get-XmipCompilerEnvironment
    [hashtable] $saved = @{}
    foreach ($key in $environment.Keys) {
        $saved[$key] = [Environment]::GetEnvironmentVariable($key)
        [Environment]::SetEnvironmentVariable($key, $environment[$key])
    }
    try {
        & cl.exe @flags @paths $Source "/Fo$output.obj" "/Fe$output.exe"
    }
    finally {
        foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    }
}

function Get-XmipBindingAbsence {
    <#
        .SYNOPSIS
        Why a binding's tool is not here, in words; empty when it is.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('C', 'Cpp', 'Java', 'Python')]
        [string] $Name,

        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Javac
    )

    switch ($Name) {
        { $_ -in 'C', 'Cpp' } {
            [string] $compiler = if ($Name -eq 'C') { 'cc' } else { 'c++' }
            if ($IsWindows -and (Get-XmipCompilerEnvironment).Count -eq 0) {
                return 'the MSVC build tools not installed (prerequisite.toml, linker)'
            }
            if (-not ($IsWindows -or (Get-Command -Name $compiler -ErrorAction Ignore))) {
                return "$compiler not installed"
            }
        }
        'Java' {
            if (-not (Get-Command -Name $Javac -ErrorAction SilentlyContinue)) {
                return 'Java not installed'
            }
            [string] $said = "$(& $Javac -version 2>&1)"
            if ($said -notmatch '(\d+)' -or [int] $Matches[1] -lt 27) {
                return "Java 27 not installed ($said found)"
            }
        }
        'Python' {
            if (-not (Get-Command -Name 'python' -ErrorAction SilentlyContinue)) {
                return 'Python not installed'
            }
            & python -c 'import sys; sys.exit(sys.version_info < (3, 15))' 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                return "Python 3.15 not installed ($(& python --version 2>&1) found)"
            }
        }
    }
    return ''
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
    [string] $audit = New-XmipAuditDirectory
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
[string[]] $failed = @()

[string] $bin = ''
if ($env:JAVA_HOME -and (Test-Path -LiteralPath $env:JAVA_HOME)) {
    $bin = Join-Path $env:JAVA_HOME 'bin'
}
[string] $javac = if ($bin) { Join-Path $bin 'javac' } else { 'javac' }
[string] $java = if ($bin) { Join-Path $bin 'java' } else { 'java' }

# A binding whose tool is absent is said and left out; it fails nothing.
[hashtable] $spoken = @{ C = 'C'; Cpp = 'C++'; Java = 'Java'; Python = 'Python' }
[string[]] $absent = @(
    foreach ($name in $Language) {
        [string] $why = Get-XmipBindingAbsence -Name $name -Javac $javac
        if ($why) {
            Write-Host "SKIPPED: $why; the $($spoken[$name]) Event binding is not verified"
            $name
        }
    }
)
[string[]] $present = @($Language | Where-Object { $_ -notin $absent })

if ($present -contains 'C') {
    [string] $out = Join-Path $build 'c'
    [bool] $ok = Invoke-XmipBindingTest -Name 'C' -Build {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        foreach ($program in 'xmip_event_test', 'xmip_event_example') {
            Invoke-XmipCompiler -Name C -Source "c/$program.c" -Directory $out -Include 'c'
            if ($LASTEXITCODE -ne 0) {
                break
            }
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

if ($present -contains 'Cpp') {
    [string] $out = Join-Path $build 'cpp'
    [bool] $ok = Invoke-XmipBindingTest -Name 'Cpp' -Build {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        Invoke-XmipCompiler -Name Cpp -Source 'cpp/xmip_event_test.cpp' -Directory $out
    } -Run {
        param($audit)
        & (Join-Path $out "xmip_event_test$exe") $Library $audit
    }
    if (-not $ok) {
        $failed += 'Cpp'
    }
}

if ($present -contains 'Java') {
    [string] $out = Join-Path $build 'java'
    [bool] $ok = Invoke-XmipBindingTest -Name 'Java' -Build {
        Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction SilentlyContinue
        [string[]] $sources = (Get-ChildItem -Path java -Recurse -Filter '*.java').FullName
        # Every restricted method is the binding's purpose, and the run grants
        # it by --enable-native-access, so that one lint is off.
        & $javac --release 27 '-Xlint:all,-restricted' -Werror -d $out @sources
    } -Run {
        param($audit)
        & $java --enable-native-access=ALL-UNNAMED -cp $out `
            se.xmip.event.EventBindingTest $Library $audit $include
    }
    if (-not $ok) {
        $failed += 'Java'
    }
}

if ($present -contains 'Python') {
    [bool] $ok = Invoke-XmipBindingTest -Name 'Python' -Build {
        & python -c 'import sys; sys.exit(sys.version_info < (3, 15))'
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
if ($present.Count -eq 0) {
    Write-Host 'OK. No binding verified here: every tool is absent, as said above.'
    exit 0
}
Write-Host "OK. $($present -join ', ')"
exit 0
