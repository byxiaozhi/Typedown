<#
.SYNOPSIS
    A direct client of Typedown's local automation API (docs/automation-api-spec.md) for Windows PowerShell 5.1+.

.EXAMPLE
    . .\Typedown-Client.ps1
    $c = Connect-Typedown
    Initialize-Typedown $c @('document.read', 'document.write')
    (Invoke-Typedown $c 'document.list').documents
    Set-TypedownText $c $documentId 'old words' 'new words'
    $c.Pipe.Dispose()

    Set-TypedownText follows the pattern every writer should: read, write against the revision it read, and when the
    document moved on meanwhile (revision_conflict) or the text no longer matches (match_count_mismatch), read again
    and decide again from the new text - never resend an edit computed from old text.
#>

function Connect-Typedown([string]$Endpoint) {
    if (-not $Endpoint) {
        $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $Endpoint = "Typedown.Automation.v1.$sid"
    }
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', $Endpoint, [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(3000)
    [pscustomobject]@{ Pipe = $pipe; NextId = 1 }
}

function Send-TypedownFrame($Client, [string]$Json) {
    $body = [System.Text.UTF8Encoding]::new($false).GetBytes($Json)
    $header = [System.Text.Encoding]::ASCII.GetBytes("Content-Length: $($body.Length)`r`n`r`n")
    $Client.Pipe.Write($header, 0, $header.Length)
    $Client.Pipe.Write($body, 0, $body.Length)
    $Client.Pipe.Flush()
}

function Read-TypedownFrame($Client) {
    $length = -1
    while ($true) {
        $line = New-Object System.Collections.Generic.List[byte]
        while ($line.Count -lt 2 -or $line[$line.Count - 2] -ne 13 -or $line[$line.Count - 1] -ne 10) {
            $b = $Client.Pipe.ReadByte()
            if ($b -lt 0) { throw 'Typedown closed the connection' }
            $line.Add([byte]$b)
        }
        $text = [System.Text.Encoding]::ASCII.GetString($line.ToArray()).TrimEnd("`r", "`n")
        if ($text -eq '') { break }
        $name, $value = $text -split ':', 2
        if ($name.Trim() -eq 'Content-Length') { $length = [int]$value.Trim() }
    }
    $body = New-Object byte[] $length
    $read = 0
    while ($read -lt $length) {
        $n = $Client.Pipe.Read($body, $read, $length - $read)
        if ($n -le 0) { throw 'Typedown closed the connection' }
        $read += $n
    }
    [System.Text.UTF8Encoding]::new($false).GetString($body) | ConvertFrom-Json
}

# The result, or a terminating error whose TargetObject is the JSON-RPC error ({code, message, data.kind}).
function Invoke-Typedown($Client, [string]$Method, $Params = @{}) {
    $id = $Client.NextId
    $Client.NextId++
    Send-TypedownFrame $Client (@{ jsonrpc = '2.0'; id = $id; method = $Method; params = $Params } | ConvertTo-Json -Depth 20 -Compress)
    while ($true) {
        $reply = Read-TypedownFrame $Client
        if ($reply.id -eq $id -or ($null -eq $reply.id -and $reply.error)) {
            if ($reply.error) {
                $record = [System.Management.Automation.ErrorRecord]::new(
                    [System.Exception]::new("$($reply.error.message) [$($reply.error.data.kind)]"), $reply.error.data.kind, 'NotSpecified', $reply.error)
                throw $record
            }
            return $reply.result
        }
    }
}

function Initialize-Typedown($Client, [string[]]$Scopes) {
    Invoke-Typedown $Client 'system.initialize' @{
        apiVersion = 1
        client = @{ id = '0f5a8b43-6b71-4f4c-9a55-2f1e3c6c5d20'; name = 'Typedown-Client.ps1'; version = '1' }
        requestedScopes = $Scopes
    }
}

function Set-TypedownText($Client, [string]$DocumentId, [string]$Find, [string]$Replacement, [switch]$Save) {
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        $doc = Invoke-Typedown $Client 'document.get' @{ documentId = $DocumentId; consistency = 'latest'; include = @('text') }
        $count = ([regex]::Matches($doc.text, [regex]::Escape($Find))).Count
        if ($count -eq 0) { throw "'$Find' is not in the document (revision $($doc.revision))" }
        try {
            return Invoke-Typedown $Client 'document.replaceText' @{
                documentId = $DocumentId; baseRevision = $doc.revision
                find = $Find; replacement = $Replacement; expectedCount = $count; save = [bool]$Save
            }
        }
        catch {
            $kind = $_.TargetObject.data.kind
            if ($kind -ne 'revision_conflict' -and $kind -ne 'match_count_mismatch') { throw }
            Write-Warning "the document changed meanwhile ($kind); reading it again"
        }
    }
    throw 'the document keeps changing; giving up'
}
