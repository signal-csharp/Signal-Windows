function Remove-SignalFiles {
    [CmdletBinding(
        SupportsShouldProcess,
        ConfirmImpact = "High"
    )]
    param (
        [Switch]$Force
    )

    if ($Force -and -not $Confirm) {
        $ConfirmPreference = "None"
    }

    $localCachePath = "$env:LOCALAPPDATA/Packages/2383BenediktRadtke.SignalPrivateMessenger_teak1p7hcx9ga/LocalCache"
    if ($PSCmdlet.ShouldProcess($localCachePath, "Delete databases, logs, and attachments")) {
        Remove-Item -Recurse -Force "$localCachePath/*"
        Write-Host "Removed items"
    }
}

Remove-SignalFiles
