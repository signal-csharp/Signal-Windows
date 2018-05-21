$userdir = $env:USERPROFILE

$protobufPath = $userdir + '/.nuget/packages/google.protobuf.tools'

if (-not (Test-Path $protobufPath))
{
    Write-Error ('Could not find protobuf tools path at ' + $protobufPath + "`nTry restoring nuget packages")
    exit
}

$versions = Get-ChildItem $protobufPath
$protoc = $protobufPath + "/$($versions[-1].Name)/tools/windows_x64/protoc.exe"

if (-not (Test-Path $protoc))
{
    Write-Error ('Could not find protoc at ' + $protoc + "`nTry restoring nuget packages")
    exit
}

& $protoc --csharp_out=. WebRtcData.proto

Move-Item -Force WebRtcData.cs ../WebRTC/SignalWebRtcData.cs
