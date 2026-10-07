# Deploying Caret in an organization

A guide for whoever looks after the PCs, whether that's an IT department in a large enterprise, one person in a growing SME or a solo-preneur setting up their own laptop. It covers what Caret is, what it does on a device and on the network, and how to deploy it with Microsoft Intune or another tool. *(Version française : [deployment.fr.md](deployment.fr.md).)*

## Pick your route

| You are | Suggested route |
| --- | --- |
| **A solo-preneur or a small team without managed devices** | Install Caret from the [Microsoft Store](https://apps.microsoft.com/detail/9n617shlqm8g) or with `winget` ([Option A](#option-a-microsoft-store-app-recommended)): no admin rights needed. Only if you can't use the Store, take the package from [GitHub Releases](https://github.com/fegyenc/Caret/releases): it is signed with the project's own certificate, and trusting it once (`Caret.cer`, as the release notes explain) needs admin rights; after that, installs and updates don't. |
| **An SME with Microsoft 365 Business Premium or Intune** | Offer Caret in Company Portal as a Microsoft Store app ([Option A](#option-a-microsoft-store-app-recommended)); optionally set the [defaults](#set-the-defaults) so everyone starts with the same look. |
| **An enterprise** | Store app through Intune, or your own signed package ([Option B](#option-b-line-of-business-msix)) if every version must be approved. Use the [policies](#policies) to switch off update checks and set defaults. |

## At a glance

| | |
| --- | --- |
| **What** | A Markdown editor with tabs and a built-in converter from Word, Excel, PowerPoint, PDF, CSV and emails (Outlook `.msg`, `.eml`) to Markdown, for pasting into AI assistants with far fewer tokens |
| **Publisher / source** | Open source, MIT licence: <https://github.com/fegyenc/Caret> |
| **Package** | MSIX, per-user. No administrator rights needed; installs no services, drivers or scheduled tasks. |
| **Architectures** | x64 and ARM64 |
| **Requirements** | Windows 10 version 1809 or later; Windows 11 recommended. The Microsoft Edge WebView2 Runtime, which is part of Windows 11 and installed with Microsoft 365 Apps on Windows 10. .NET and the Windows App SDK are included in the package. |
| **Languages** | English, French, Spanish, Polish (follows the Windows display language; users can change it) |
| **Account / sign-in** | None |
| **Telemetry** | None |

## Data and network

- **Documents never leave the device.** Editing, saving and conversion all happen locally. There is no cloud service behind Caret.
- **No AI inside.** Caret doesn't call any AI service or language model and needs no API key. Masking personal data in emails (names, email addresses, phone numbers, IBANs and ID numbers become placeholders such as `[PERSON-1]`) is done on the device with fixed rules. Names are recognised from the email's senders and recipients and from greetings and sign-offs, so someone mentioned only inside a sentence keeps their name. Users check the result and decide what they paste into an assistant.
- **App data**: settings (including the list of documents open in tabs, so they reopen), favourites, recent files, templates and crash-recovery backups are stored in the package's data folder (`%LOCALAPPDATA%\Packages\<package family name>\LocalState`). Removing the app removes them.
- **Outbound connections Caret can make:**

| Destination | When | Microsoft Store version | GitHub version or a package you deploy yourself | Can be disabled |
| --- | --- | --- | --- | --- |
| `api.github.com` | Update check: after a successful one, none for 20 hours; a failed one is retried at the next start | Never | Yes | Policy `DisableUpdateCheck` (below), or per user in Settings |
| `pypi.org`, `files.pythonhosted.org` | Installing the optional MarkItDown converter, only when the user clicks *Install* | Never | On request | Policy `DisableMarkItDownInstall` |
| Websites referenced in a note | Images from the web shown in a note (like a browser) | Yes | Yes | No (content-driven) |

Word, Excel, PowerPoint, PDF, CSV and email conversion is built in and needs no network, no Outlook and no Python. MarkItDown only adds rarer formats.

The privacy policy is at [PRIVACY.md](../PRIVACY.md).

## Option A: Microsoft Store app (recommended)

Caret is in the [Microsoft Store](https://apps.microsoft.com/detail/9n617shlqm8g) (Store ID `9N617SHLQM8G`). Intune deploys and updates it directly from the Store.

1. Intune admin center → **Apps** → **Windows** → **Add** → app type **Microsoft Store app (new)**.
2. **Search the Microsoft Store app (new)** → search for *Caret* → select it. The Store ID (starting with `9`) is filled in.
3. **Install behavior: User.** Caret is a per-user app.
4. Assign it to groups as **Available for enrolled devices** (users install it from Company Portal) or **Required**.

Intune keeps the app up to date through the Store; users need no Store account. The same Store ID works with winget:

```
winget install --source msstore --id 9N617SHLQM8G
```

## Option B: Line-of-business MSIX

For organizations that don't use the Store, or want to control each version:

1. Download the `.msix` from [GitHub Releases](https://github.com/fegyenc/Caret/releases), or build it from source.
2. **Sign it with your organization's code-signing certificate.** The package's `Publisher` must match the certificate's subject, so this means rebuilding with your publisher name. Build from source with the signing options in [PACKAGING.md](../PACKAGING.md), after setting `Identity/Publisher` in `Package.appxmanifest` to your certificate's subject.
3. Make sure the certificate is trusted on the devices (it usually already is for an internal code-signing CA).
4. Intune → Apps → Windows → Add → **Line-of-business app** → upload the `.msix` → assign.

Updates are redeployed the same way. Set the `DisableUpdateCheck` policy so users aren't told about GitHub releases your organization hasn't approved.

## Policies

Caret reads values under `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Caret` (or the same path under `HKEY_CURRENT_USER`). Set them with Group Policy Preferences, an Intune remediation script or a configuration profile.

### Switch features off

DWORD values:

| Value | Effect |
| --- | --- |
| `DisableUpdateCheck` = `1` | No GitHub update check; the setting is hidden and Settings says updates are managed by your organization |
| `DisableMarkItDownInstall` = `1` | Caret never runs `pip`; for the rare formats that need MarkItDown it explains how to install it instead |
| `DisableExplorerMenu` = `1` | No **Convert to Markdown** in File Explorer's right-click menu; the switch on the Convert page is off and can't be turned on |

In a Microsoft Store install the first two behaviours are already on and can't be turned off. `DisableExplorerMenu` works in every install; the menu item is added by the package, so it is the only way to remove it centrally.

Example (run as administrator):

```
reg add HKLM\SOFTWARE\Policies\Caret /v DisableUpdateCheck /t REG_DWORD /d 1 /f
reg add HKLM\SOFTWARE\Policies\Caret /v DisableMarkItDownInstall /t REG_DWORD /d 1 /f
reg add HKLM\SOFTWARE\Policies\Caret /v DisableExplorerMenu /t REG_DWORD /d 1 /f
```

### Set the defaults

String (`REG_SZ`) values that choose what people start with, so a whole team gets the same look from day one. They're defaults, not locks: anyone can still change them in Settings, and their own choice is kept. Values aren't case-sensitive; an unknown value is ignored. When both hives have a valid value, `HKEY_LOCAL_MACHINE` comes first; an unknown value under `HKEY_LOCAL_MACHINE` doesn't block a valid one under `HKEY_CURRENT_USER`.

| Value | Choices | Built-in default |
| --- | --- | --- |
| `DefaultLayout` | `classic`, `streamlined` (menu and formatting toolbar on one row) | `streamlined` on a new install, `classic` for people updating from an earlier version |
| `DefaultColorScheme` | `copper`, `paper`, `sage`, `harbour` (or `harbor`), `graphite` | `copper` |
| `DefaultAccentColor` | `scheme` (the scheme's own accent), `windows` (the accent colour chosen in Windows) | `scheme` |
| `DefaultTheme` | `system`, `light`, `dark` | `system` |

Example: the Streamlined layout with the Harbor scheme and the Windows accent colour (run as administrator):

```
reg add HKLM\SOFTWARE\Policies\Caret /v DefaultLayout /t REG_SZ /d streamlined /f
reg add HKLM\SOFTWARE\Policies\Caret /v DefaultColorScheme /t REG_SZ /d harbour /f
reg add HKLM\SOFTWARE\Policies\Caret /v DefaultAccentColor /t REG_SZ /d windows /f
```

Policies are read when Caret starts. A default applies to a setting only while the user's own settings file has no saved value for it, so set them before people first start Caret: a new install without a layout policy saves Streamlined straight away, and changing any appearance setting in Settings saves the scheme, accent colour and window material together. A default set later doesn't change what is already saved.

## Security notes

- **Capabilities:** only `runFullTrust`, the standard capability for packaged desktop apps (needed to open and save files anywhere the user chooses). Like any desktop program it can use the network; the only connections it makes are listed above.
- **File associations:** Caret registers as an *Open with* option for `.md`, `.markdown` and similar extensions. It doesn't take over the default app.
- **Links in notes:** web links open in the default browser. Links to local files open only documents and media; executables and scripts are revealed in File Explorer and never run.
- **Third-party components** and their licences: [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). The document converters are Microsoft's Open XML SDK (MIT) and PdfPig (Apache 2.0).

## Removing Caret

Uninstall from Settings → Apps, or remove the Intune assignment (Uninstall). App data is removed with the package; the user's own notes and converted `.md` files stay where they were saved.
