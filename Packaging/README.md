# Windows package (MSIX)

The `MSIX package` job in `.github/workflows/build.yml` builds `EyeBrowse_<version>.msixbundle`
(x64 and ARM64) on every push, using `Build-Msix.ps1` (pushes that only change documentation,
`TestImages/` or `LICENSE` don't build). Tagged releases (`v1.2.3`) attach it to the GitHub
release as version `1.2.3.0`; other builds are `0.0.<run number>.0`, kept as workflow run
artifacts for 14 days. The app inside shows the same version without the last `.0` (`1.2.3`,
like the other platforms' builds); a local `dotnet build` shows `0.0.0-dev`.

Installing it adds EyeBrowse to the Start menu and to **Open with** for images (`.png .jpg
.jpeg .bmp .gif .tif .tiff .webp`) and comic archives (`.cbz .cbr .cb7 .cbt .zip`). Windows
never makes it the default by itself: choose **Open with → Choose another app** and tick
"Always" if you want that. Uninstall it like any other app.

## Signing

Windows only installs a package signed by a certificate it trusts. The Microsoft Store
signs packages itself, so for the Store the bundle is uploaded unsigned. For installing
test builds directly, sign them with your own test certificate:

1. On Windows, make one (once):
   `./Packaging/New-TestCertificate.ps1 -Password (Read-Host -AsSecureString)`
   This writes `EyeBrowse-test.pfx` (keep it private, and out of the repository) and
   `EyeBrowse-test.cer`, and prints the `.pfx` as base64.
2. In the repository's **Settings → Secrets and variables → Actions**, add:
   - secret `MSIX_CERTIFICATE`: the base64 text
   - secret `MSIX_CERTIFICATE_PASSWORD`: the password
   - variable `MSIX_PUBLISHER`: `CN=EyeBrowse Test` (must match the certificate's subject)
3. On each PC you install on, trust the certificate once, in an administrator PowerShell:
   `Import-Certificate -FilePath EyeBrowse.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`
   (signed builds also include `EyeBrowse.cer` next to the bundle).

Then double-click the `.msixbundle` to install.

## Building locally

On Windows with the .NET 10 SDK and the Windows SDK:

```
./Packaging/Build-Msix.ps1 -Version 0.2.0.0 -CertificatePath EyeBrowse-test.pfx `
    -CertificatePassword (Read-Host -AsSecureString)
```

The bundle is written to `msix/`.

## For the Microsoft Store

Reserve the app name in Partner Center, then set these repository variables to the values
under **Product identity** (they must match exactly):

- `MSIX_IDENTITY_NAME` (Package/Identity/Name)
- `MSIX_PUBLISHER` (Package/Identity/Publisher, `CN=` followed by an ID)
- `MSIX_PUBLISHER_DISPLAY_NAME` (Package/Properties/PublisherDisplayName)

Remove the certificate secrets (or leave the Store build unsigned) and upload the
`.msixbundle` from a tagged release.

## Images

`Images/` holds the tile and logo images at each scale, rendered from `Assets/eyebrowse.svg`
by `render-images.js` (`node Packaging/render-images.js`, needs Node and Playwright).
