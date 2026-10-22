# Localization

Caret is available in **English**, **French**, **Spanish**, **Polish** and **Brazilian Portuguese**. People choose their language in **Settings → Language**; *Use Windows display language* (the default) picks French, Spanish, Polish or Portuguese automatically on a French, Spanish, Polish or Portuguese Windows (the Portuguese is the Brazilian one, for Portugal too) and falls back to English otherwise. A change applies the next time Caret starts.

## How it works

| Piece | Where |
| --- | --- |
| Caret's own text | `Dev/Typedown.WinUI/Strings/<lang>/AppResources.resw` |
| Text inherited from Typedown (mostly used by the editor: placeholders, footnote tool, tooltips) | `Strings/en/CommonResources.resw`, `DialogResources.resw`, `SettingsResources.resw`. The translated versions of the ones the editor still uses live in `Strings/fr`, `Strings/es` and `Strings/pl` `AppResources.resw`. |
| Loading | `Utilities/Locale.cs` reads the `.resw` files as XML at startup. English is always loaded underneath the chosen language, so a string that isn't translated yet shows in English instead of disappearing. |
| In XAML | `Text="{u:Loc Key=SaveMenuItem_Text}"` (`Utilities/LocExtension.cs`) |
| In code | `Locale.GetString("Untitled")`, or `Locale.Format("SaveChangesPrompt", name)` for text with `{0}` placeholders |

Rules that keep translations working:

- **Never build a sentence out of pieces.** Use one string with `{0}` placeholders, so each language can put the name where its grammar needs it (`"Voulez-vous enregistrer les modifications apportées à {0} ?"`).
- **Plurals are separate strings** (`WordCountOne` / `WordCountMany`). French treats 0 as singular; the code handles that. Polish has a third form (2 to 4), which the code does not distinguish: the Polish "many" strings are worded to work for every count but 1 (*Liczba wyrazów: {0}*, "number of words: {0}"), so no number gets a wrong ending.
- **Give an element its own key when the same English word means different things.** "View" is both a menu (*Affichage*, *Ver*) and an editing mode (*Visuel*, *Visual*).
- Keys and placeholders must match across languages; `Strings/en/AppResources.resw` is the reference.

## Adding a language

1. Copy `Strings/en/AppResources.resw` to `Strings/<code>/AppResources.resw` (two-letter code, e.g. `de`) and translate every `<value>`, keeping `{0}` and `{identifier}` placeholders exactly as they are.
2. Add the editor strings at the end of `fr/AppResources.resw` (from `InputFootnoteDefine` onward) to the new file too, translated.
3. Add the code to `Locale.SupportedLanguages` and a `ComboBoxItem` (language name written in that language) to `LanguageComboBox` in `MainWindow.xaml`, plus its tag to the index list in `LoadSettingsIntoDialog`.
4. Build: the `.resw` is picked up and copied automatically.

## Style

| | French | Spanish | Polish | Portuguese (Brazil) |
| --- | --- | --- | --- | --- |
| Address | *vous* | *tú* (Microsoft's current Spanish style) | impersonal commands (*Zapisz*, *Otwórz*) and *Ty* in sentences, as in current Windows and Office | *você* (informal, as in current Windows and Office) |
| Punctuation | non-breaking space before `: ; ? !` and inside `« »` | opening `¿ ¡`; quotes `« »` | quotes „ ”; decimal comma, space between thousands | quotes “ ”; decimal comma, dot between thousands |
| Shift key | Maj (`Ctrl+Maj+S`) | Mayús (`Ctrl+Mayús+S`) | Shift (`Ctrl+Shift+S`), unchanged | Shift (`Ctrl+Shift+S`), unchanged |
| OK button | OK | Aceptar | OK | OK |
| Page sizes | centimetres | centimetres | centimetres | centimetres (Carta, A4, Ofício) |
| Terminology source | Microsoft Windows / Office French | Microsoft Windows / Office Spanish (Spain) | Microsoft Windows / Office Polish | Microsoft Windows / Office Portuguese (Brazil) |

## Glossary

Terms that must stay consistent everywhere, including the Store listing and documentation.

| English | French | Spanish | Polish | Portuguese (Brazil) |
| --- | --- | --- | --- | --- |
| Note | Note | Nota | Notatka | Nota |
| File / Folder | Fichier / Dossier | Archivo / Carpeta | Plik / Folder | Arquivo / Pasta |
| Save / Save As | Enregistrer / Enregistrer sous | Guardar / Guardar como | Zapisz / Zapisz jako | Salvar / Salvar como |
| Open | Ouvrir | Abrir | Otwórz | Abrir |
| Settings | Paramètres | Configuración | Ustawienia | Configurações |
| Favorites | Favoris | Favoritos | Ulubione | Favoritos |
| Templates | Modèles | Plantillas | Szablony | Modelos |
| Trash / Recycle Bin | Corbeille | Papelera / Papelera de reciclaje | Kosz | Lixeira |
| Heading 1 | Titre 1 | Título 1 | Nagłówek 1 | Título 1 |
| Bulleted / Numbered / Task list | Liste à puces / numérotée / de tâches | Lista con viñetas / numerada / de tareas | Lista punktowana / numerowana / zadań | Lista com marcadores / numerada / de tarefas |
| Code block | Bloc de code | Bloque de código | Blok kodu | Bloco de código |
| Table | Tableau | Tabla | Tabela | Tabela |
| Link | Lien | Vínculo | Link | Link |
| Alt text | Texte de remplacement | Texto alternativo | Tekst alternatywny | Texto alternativo |
| Find and Replace | Rechercher et remplacer | Buscar y reemplazar | Znajdź i zamień | Localizar e substituir |
| Speech mode | Mode discours | Modo discurso | Tryb przemówienia | Modo de apresentação |
| Speech mark | Marque (de discours) | Marca (de discurso) | Znacznik (przemówienia) | Marca (de fala) |
| Pause / Beat | Pause / Souffle | Pausa / Pausa corta | Pauza / Chwila | Pausa / Pausa curta |
| Pace | Rythme | Ritmo | Tempo | Ritmo |
| Cue (a note to the speaker) | Repère | Señal | Wskazówka | Sinal |
| View / Code / Split (editing modes) | Visuel / Code / Fractionné | Visual / Código / Dividido | Wizualny / Kod / Podział | Visual / Código / Dividido |
| Preview | Aperçu | Vista previa | Podgląd | Visualização |
| Import / Export | Importer / Exporter | Importar / Exportar | Importuj / Eksportuj | Importar / Exportar |
| Plain text | Texte brut | Texto sin formato | Zwykły tekst | Texto simples |
| Update | Mise à jour | Actualización | Aktualizacja | Atualização |
| Convert to Markdown | Convertir en Markdown | Convertir a Markdown | Konwertuj na Markdown | Converter para Markdown |
| Token (AI) | Jeton | Token | Token | Token |
| Slide | Diapositive | Diapositiva | Slajd | Slide |
| KB / MB | Ko / Mo | KB / MB | KB / MB | KB / MB |
| Markdown, MarkItDown, Caret | unchanged | unchanged | unchanged | unchanged |

Native-speaker review is welcome: open a pull request against the `.resw` file, or an issue quoting the key and the suggested wording.
