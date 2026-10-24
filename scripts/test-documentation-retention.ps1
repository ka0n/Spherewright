# Offline documentation validation fixtures. No game/Bridge access.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo ('.local/doc-retention-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory (Join-Path $root 'docs') -Force
$record = Join-Path $root 'docs/record.md'
$manifest = Join-Path $root 'docs/fork-retained-records.json'
$report = Join-Path $root 'report.json'
[IO.File]::WriteAllText((Join-Path $root 'LICENSE.zh-CN.md'), '# License')
[IO.File]::WriteAllText($record, "# Original record`nHistorical [link](missing-at-original-context.md).`n")
function Write-Manifest {
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($record).Replace("`r`n", "`n"))))
    @{records=@(@{path='docs/record.md';sha256Utf8Lf=$hash})} |
        ConvertTo-Json -Depth 5 | Set-Content $manifest
}
function Check([bool]$Pass, [string]$Rule) {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'verify-documentation.ps1') -Root $root -ReportPath $report *> $null
    $exit = $LASTEXITCODE
    if (($exit -eq 0) -ne $Pass) { throw "Unexpected validation result: $Rule" }
    if ($Rule -and -not @((Get-Content $report -Raw | ConvertFrom-Json).errors | Where-Object rule -eq $Rule).Count) {
        throw "Expected validation finding absent: $Rule"
    }
}
try {
    Write-Manifest
    Check $true ''
    [IO.File]::WriteAllText($record, [IO.File]::ReadAllText($record).Replace("`n", "`r`n"))
    Check $true ''
    [IO.File]::AppendAllText($record, 'edited')
    Check $false 'retained-fork-record-changed'
    # Even explicitly retained content must pass the private-path check.
    [IO.File]::WriteAllText($record, ('# Historical record' + "`n" + ('C:' + '\Users\synthetic-user\private')))
    Write-Manifest
    Check $false 'private-user-path'
    Remove-Item -LiteralPath $record
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'verify-documentation.ps1') -Root $root *> $null
    if ($LASTEXITCODE -eq 0) { throw 'A missing retained record must reject.' }
    [pscustomobject]@{passed=5;gameCalls=0} | ConvertTo-Json -Compress
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $allowed = [IO.Path]::GetFullPath((Join-Path $repo '.local')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
# Do not leak the expected negative child's exit code to a dot-invoking CI host.
exit 0
