# Caret editor

The Markdown editing surface that Caret hosts in WebView2. It's a React + TypeScript app built on the Muya WYSIWYG engine (from MarkText) and CodeMirror, and it came from [Typedown](https://github.com/byxiaozhi/Typedown).

## Scripts

| Command | What it does |
| --- | --- |
| `yarn install` | Installs dependencies. |
| `yarn build` | Builds the production bundle into `../Typedown.WinUI/Resources/Statics`, where the Caret app picks it up (see `config-overrides.js`). |
| `yarn start` | Serves the editor at `http://localhost:3000` with hot reload, for the app's `Debug` configuration. |

The bundle is build output and isn't checked in. Run `yarn build` after changing anything here, then rebuild the app.

## How it talks to the app

The app sends messages with `PostMessage` (name + JSON arguments). The editor calls back into the app through remote functions (`src/services/remote`) and raises events the app listens for. See the root [CHANGES.md](../../CHANGES.md) for details.
