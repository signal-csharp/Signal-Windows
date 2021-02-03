# Quirks

## SSL Certificates

Open Whisper Systems uses its own certificate issuer for its SSL certificates. These certs are not trusted by OSes by default meaning SSL certificate validation with any Signal URLs will fail without some intervention. With the version of UWP we're currently targeting (10.0.15063) the typical way of changing certificate validation using HttpClientHandler.ServerCertificateCustomValidationCallback doesn't work because of [unimplemented code in UWP](https://github.com/dotnet/runtime/issues/18819). [It is possible to create a custom HttpMessageHandler that uses the WinRT HttpBaseProtocolFilter](https://github.com/novotnyllc/WinRtHttpClientHandler) but that isn't a great solution. Instead we install the Signal cert into the OS trusted store when installing the app. See [textsecure-servicewhispersystemsorg.crt](../Signal-Windows/textsecure-servicewhispersystemsorg.crt) and [Package.appxmanifest](../Signal-Windows/Package.appxmanifest).

### Adding/Updating Signal Certificates

1. Go to the Signal URL in your browser (https://textsecure-service.whispersystems.org, https://api.directory.signal.org, etc.)
2. View the certificate in your browser
3. Download the certificate.
    - Base64 encoded .CER in Chrome/Edge and PEM (cert) in Firefox
- If adding
  - Copy the certificate to the Signal-Windows directory
  - Change the extension to .crt
  - Open [Package.appxmanifest](../Signal-Windows/Package.appxmanifest) and add a new `<Certificate>` tag with `StoreName` as `TrustedPeople` and `Content` being the filename of the certificate.
- If updating rename the certificate to match what is currently in the repo and copy the certificate to the repo
