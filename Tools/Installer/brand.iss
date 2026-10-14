; The product's names for the installer (Typedown.iss includes this file); the same names are in Branding.props and
; Dev/Typedown.Automation/Brand.cs, and Tools/Branding/check-brand.py checks that they agree.
#define MyAppName "Typedown"
#define MyAppPublisher "Typedown Community"
#define MyAppExeName "Typedown.exe"
; An edition under another name has its own AppId: installed side by side, neither replaces the other.
#define MyAppId "{{B6F0C3A1-4E27-4D8B-9C11-7A2E5D8F1B30}"
