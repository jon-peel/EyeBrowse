# Windows package (MSIX)

The `MSIX package` job in `.github/workflows/build.yml` builds two bundles (x64 and ARM64)
with `Build-Msix.ps1`, kept as workflow run artifacts for 14 days and never put in GitHub
releases, which offer the plain programs instead:

- `msix-store`: unsigned, with the app's Store identity, for uploading to the Microsoft
  Store (which signs it). Built only when the Store identity is set (below).
- `msix-test`: signed with your test certificate, for installing directly to try a build
  before it goes to the Store. Built only when the certificate secret is set (below). Its
  identity is `EyeBrowse.Test`, so it installs beside the Store's copy, not over it.

Pushes that only change documentation, `TestImages/` or `LICENSE` don't build. A tag
(`v1.2.3`) builds version `1.2.3.0`; other builds are `0.0.<run number>.0`. The app inside
shows the same version without the last `.0` (`1.2.3`, like the other platforms' builds); a
local `dotnet build` shows `0.0.0-dev`.

A pre-release tag (`v1.2.3-beta`) makes a GitHub pre-release. Package versions can only be
numbers, so its packages are also `1.2.3.0`, while the app shows `1.2.3-beta`. Windows won't
install a different package over one with the same version, so uninstall the pre-release
before installing the release of the same number. Upload only release (not pre-release)
bundles to the Store: each upload must have a higher version than the last.

Installing it adds EyeBrowse to the Start menu and to **Open with** for images (`.png .jpg
.jpeg .bmp .gif .tif .tiff .webp`) and comic archives (`.cbz .cbr .cb7 .cbt .zip`). Windows
never makes it the default by itself: choose **Open with → Choose another app** and tick
"Always" if you want that. Uninstall it like any other app.

## Signing test builds

Windows only installs a package signed by a certificate it trusts. The Store signs its
packages itself; for installing test builds directly, sign them with your own certificate:

1. On Windows, make one (once):
   `./Packaging/New-TestCertificate.ps1 -Password (Read-Host -AsSecureString)`
   This writes `EyeBrowse-test.pfx` (keep it private, and out of the repository) and
   `EyeBrowse-test.cer`, and prints the `.pfx` as base64.
2. In the repository's **Settings → Secrets and variables → Actions**, add the secrets
   `MSIX_CERTIFICATE` (the base64 text) and `MSIX_CERTIFICATE_PASSWORD`. The package's
   publisher is taken from the certificate.
3. On each PC you install on, trust the certificate once, in an administrator PowerShell:
   `Import-Certificate -FilePath EyeBrowse.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`
   (`EyeBrowse.cer` comes in the `msix-test` artifact, next to the bundle).

Then double-click the `.msixbundle` to install.

## Building locally

On Windows with the .NET 10 SDK and the Windows SDK:

```
./Packaging/Build-Msix.ps1 -Version 0.2.0.0 -CertificatePath EyeBrowse-test.pfx `
    -CertificatePassword (Read-Host -AsSecureString)
```

The bundle is written to `msix/`.

## For the Microsoft Store

Reserve the app name in Partner Center, then add these to the repository's **Settings →
Secrets and variables → Actions** (as variables or secrets), with the values under
**Product management → Product identity** (they must match exactly):

- `MSIX_IDENTITY_NAME` (Package/Identity/Name)
- `MSIX_PUBLISHER` (Package/Identity/Publisher, `CN=` followed by an ID)
- `MSIX_PUBLISHER_DISPLAY_NAME` (Package/Properties/PublisherDisplayName)

For each release, push its tag, then upload the `.msixbundle` from that run's `msix-store`
artifact to a new submission in Partner Center. (Microsoft's GitHub action for submitting
updates only supports free apps so far, and only once the app is live in the Store.)

## Images

`Images/` holds the tile and logo images at each scale, rendered from `Assets/eyebrowse.svg`
by `render-images.js` (`node Packaging/render-images.js`, needs Node and Playwright).
