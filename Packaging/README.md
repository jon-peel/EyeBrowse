# Windows package (MSIX)

The `MSIX package` job in `.github/workflows/build.yml` builds `EyeBrowse_<version>.msixbundle`
(x64 and ARM64) with `Build-Msix.ps1`: unsigned, with the app's Store identity, for uploading
to the Microsoft Store (which signs it). It's kept as the `msix-store` workflow run artifact
for 14 days and never put in GitHub releases, which offer the plain programs instead. It's
built only when the Store identity is set (below).

Pushes that only change documentation, `TestImages/` or `LICENSE` don't build. A tag
(`v1.2.3`) builds version `1.2.3.0`; other builds are `0.0.<run number>.0`. The app inside
shows the same version without the last `.0` (`1.2.3`, like the other platforms' builds); a
local `dotnet build` shows `0.0.0-dev`.

A pre-release tag (`v1.2.3-beta`) makes a GitHub pre-release. Package versions can only be
numbers, so its bundle is also `1.2.3.0`, while the app shows `1.2.3-beta`. Upload only
release (not pre-release) bundles to the Store: each upload must have a higher version than
the last.

Installing it adds EyeBrowse to the Start menu and to **Open with** for images (`.png .jpg
.jpeg .bmp .gif .tif .tiff .webp`) and comic archives (`.cbz .cbr .cb7 .cbt .zip`). Windows
never makes it the default by itself: choose **Open with → Choose another app** and tick
"Always" if you want that. Uninstall it like any other app.

## Signing for local testing

Windows only installs a package signed by a certificate it trusts, so the Store bundle can't
be installed directly. To try a package on your own PC, build and sign one locally with a test
certificate: make one (once) with
`./Packaging/New-TestCertificate.ps1 -Password (Read-Host -AsSecureString)`, which writes
`EyeBrowse-test.pfx` (keep it private, and out of the repository) and `EyeBrowse-test.cer`.
Trust the certificate once, in an administrator PowerShell:
`Import-Certificate -FilePath EyeBrowse-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`

## Building locally

On Windows with the .NET 10 SDK and the Windows SDK:

```
./Packaging/Build-Msix.ps1 -Version 0.2.0.0 -CertificatePath EyeBrowse-test.pfx `
    -CertificatePassword (Read-Host -AsSecureString)
```

The bundle is written to `msix/`; double-click it to install. (The publisher, by default
`CN=EyeBrowse Test`, must be the certificate's subject.)

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
