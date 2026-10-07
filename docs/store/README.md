# Publishing Caret in the Microsoft Store

Everything needed for the submission is in this folder:

| File | What it is |
| --- | --- |
| [listing.en.md](listing.en.md), [listing.fr.md](listing.fr.md), [listing.es.md](listing.es.md), [listing.pl.md](listing.pl.md) | Store listing texts, field by field |
| [screenshots/](screenshots) | Six 1920×1080 screenshots for each language (`en`, `fr`, `es`, `pl`), made with fictional documents and emails |
| [../../PRIVACY.md](../../PRIVACY.md) | Privacy policy (English, French, Spanish, Polish) |

Caret is in the Store (Store ID `9N617SHLQM8G`, page: <https://apps.microsoft.com/detail/9n617shlqm8g>), so companies can deploy it with Intune: see [../deployment.md](../deployment.md).

## Before you start

- **Turn on GitHub Issues** for the repository (Settings → Features → Issues). The privacy policy and the Store listing give it as the support contact.
- **Privacy policy URL:** `https://github.com/fegyenc/Caret/blob/main/PRIVACY.md` (valid once this branch is merged).

## 1. Create a Partner Center account

Go to <https://partner.microsoft.com/dashboard/registration> and sign up with a Microsoft account.

- **Individual** account: registration is free for individual developers (check the current terms when you sign up). The publisher name shown in the Store is your own name or the name you choose.
- **Company** account: a one-time fee and verification of the company. Only choose this if the company itself will publish Caret.

Identity verification can take from a few hours to a few days.

## 2. Reserve the name

Partner Center → Apps and games → **New product** → MSIX or PWA app → reserve **Caret**. If it's taken, try **Caret Markdown** (the listing files mention alternatives).

## 3. Package identity

The app is reserved as **Caret – Markdown & Document Converter** (Store ID `9N617SHLQM8G`). Its Partner Center identity is built into the project and used only for Store builds (`Typedown.WinUI.csproj`, `StoreBuild`):

```xml
<Identity Name="FerencWorks.CaretMarkdownDocumentConverter" Publisher="CN=5B5605EC-AA08-41C3-8C90-78B9E7C00DEF" />
<PublisherDisplayName>Ferenc Works</PublisherDisplayName>
```

`Package.appxmanifest` itself keeps the GitHub identity (`Caret`, `CN=Caret`), because the GitHub releases are signed with the project's own certificate and the publisher has to match it. To Windows the two are different apps, so a GitHub install and a Store install can sit side by side; someone switching to the Store version can uninstall the GitHub one.

## 4. Build the Store package

`-p:StoreBuild=true` writes a copy of the manifest with the Store identity and the reserved name (`obj\StoreManifest`) and leaves the package unsigned, because the Store signs it. Build x64 and ARM64 (Snapdragon laptops), then bundle them:

```ps
cd Dev\Typedown.WinUI
msbuild Typedown.WinUI.csproj -restore -p:Configuration=Release -p:Platform=x64 -p:StoreBuild=true -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir=bin\Store\x64\
msbuild Typedown.WinUI.csproj -restore -p:Configuration=Release -p:Platform=ARM64 -p:StoreBuild=true -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir=bin\Store\ARM64\
mkdir bin\Store\bundle
copy bin\Store\x64\*\*.msix bin\Store\bundle\
copy bin\Store\ARM64\*\*.msix bin\Store\bundle\
makeappx bundle /d bin\Store\bundle /p bin\Store\Caret_Store.msixbundle /bv <version>
```

`makeappx.exe` is in the Windows SDK (`C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\`). Both packages must have the same version. Bump `Package.appxmanifest`'s version for every submission. The Store only accepts a higher version than the last one it published.

**Optional but recommended:** run the Windows App Certification Kit on the package before uploading (`appcert.exe`, also in the Windows SDK). It runs the same basic tests as Store certification.

## 5. Fill in the submission

| Section | What to enter |
| --- | --- |
| **Pricing and availability** | Free. All markets (or pick yours). Visibility: public. |
| **Properties** | Category **Productivity**. Privacy policy URL (above). Website: `https://github.com/fegyenc/Caret`. Support contact: `https://github.com/fegyenc/Caret/issues`. |
| **Age ratings** | Answer the questionnaire; see below. Expected result: suitable for everyone (3+ / Everyone). |
| **Packages** | Upload the `.msixupload` or `.msixbundle`. Device family: Desktop. |
| **Store listings** | Add **English**, **French** and **Spanish**; paste from the listing files (plain text, so nothing needs stripping; the Store shows any `*` or `#` as typed) and upload the six screenshots from `screenshots/<language>/` in order (1 to 6) with their captions. **Polish** (offered once the package includes `Strings\pl`, from the version after 1.6.0.0): paste `listing.pl.md` the same way and upload the six from `screenshots/pl/`. |
| **Submission options → restricted capabilities** | Justification for `runFullTrust`, below. |
| **Notes for certification** | Below. |

### Age rating questionnaire

Caret is a productivity app: choose the category **Productivity / utility (not a game)**, then answer:

- Violence, fear, sexuality, profanity, drugs, gambling: **No**
- Does the app let users interact or exchange content with other users? **No**
- Does the app share the user's location? **No**
- Does the app allow digital purchases? **No**
- Does the app collect personal information? **No**
- Unrestricted internet access (web browsing)? **No.** Links in notes open in the user's own browser.

### runFullTrust justification

> Caret is a desktop app built with WinUI 3 and the Windows App SDK, packaged as MSIX. runFullTrust is the standard capability for packaged desktop apps. Caret needs it to open, edit and save the user's Markdown and Office files anywhere on their disk (including a folder workspace), to host the editor in WebView2, and to use the Windows clipboard and file dialogs. It installs no services or drivers and needs no administrator rights.

### Notes for certification

> No account or sign-in is needed.
> To test the main feature, click "Convert to Markdown" in the left sidebar (just below Home), then "Choose files..." and pick any .docx, .xlsx, .pptx or .pdf file. Conversion runs entirely offline; a .md file is written next to the original and listed with its size and an estimated token count. "Open" shows the result in the editor.
> In the Microsoft Store version the app never downloads code: the optional MarkItDown installer and the GitHub update check are turned off automatically.

## 6. After certification

Certification usually takes a few days. Caret went live with 1.2.1; 1.5.0.0 was the first update and 1.6.0.0 the second. Its Store page is <https://apps.microsoft.com/detail/9n617shlqm8g> and its Store ID `9N617SHLQM8G`; give both to your IT department for Intune: see [../deployment.md](../deployment.md).

For updates: bump the version, rebuild, and create a new submission with the new package. Listings and screenshots carry over; replace them when the listing files or screenshots here change.

**Version 1.5.0.0** (the first update after 1.2.1; 1.4.0.0 was never submitted): build the bundle as in step 4 with `/bv 1.5.0.0` and upload `bin\Store\Caret_Store.msixbundle`. In each language, paste the new description, "What's new", features and captions, and replace the three old screenshots with the six in `screenshots/<language>/`. The "What's new" text covers everything since 1.2.1 (Outlook emails from 1.3, tabs and the new interface from 1.4, dragging tabs, the collapsible sidebar and names found in greetings from 1.5).

**Version 1.6.0.0** (the second update): build the bundle as in step 4 with `/bv 1.6.0.0` and upload `bin\Store\Caret_Store.msixbundle`. In each language, paste the new "What's new", the description paragraph about converting files, and features 1, 7 and 9 (right-click in File Explorer, PDF structure, dropping a tab on another window). The six screenshots per language are unchanged: nothing they show was redesigned. "What's new" covers 1.6 only (right-click Convert to Markdown, merging tabs, the editor's right-click menu, and better conversion of PDF, Word, Excel, PowerPoint and email).

**Version 1.7.0.0** (the third update): build the bundle as in step 4 with `/bv 1.7.0.0` and upload `bin\Store\Caret_Store.msixbundle`. The package now carries Polish, so **add a Polish listing** (listing.pl.md: every field, including the description, "What's new", features and captions; the six screenshots are in `screenshots/pl/`). In English, French and Spanish paste the new "What's new", the two new description paragraphs (review, spelling), and features 15, 16, 17 and 20. The six screenshots per language are kept; a review screenshot would be a good addition for a later version.
