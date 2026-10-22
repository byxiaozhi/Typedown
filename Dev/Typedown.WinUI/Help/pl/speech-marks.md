# Znaczniki przemówienia

Znaczniki przemówienia to krótkie notatki w tekście wystąpienia, które mówią, jak je wygłosić: gdzie zrobić pauzę, co powiedzieć wolno, co podkreślić, ile czasu może zająć każda część. To zwykły tekst, więc możesz je wpisać albo dodać z panelu przemówienia, a zaznaczony tekst możesz sam pokazać asystentowi AI: Caret niczego nie wysyła.

## Włączanie

**Widok > Tryb przemówienia** pokazuje znaczniki jako małe znaczki i otwiera panel przemówienia (także pod pozycją **Znaczniki przemówienia** na pasku bocznym). Panel wyświetla listę wszystkich znaczników, czas każdej sekcji i listę wskazówek.

## Znaczniki

| Znacznik | Znaczenie |
|---|---|
| `{pause 3s}` | pauza trzysekundowa (`{beat}` to pół sekundy) |
| `{wait 5s: applause}` | czas zostawiony dla publiczności |
| `{slow}...{/slow}`, `{fast}...{/fast}` | wolniej lub szybciej |
| `{loud}...{/loud}`, `{soft}...{/soft}` | głośniej lub ciszej |
| `{emphasis}...{/emphasis}` | podkreśl to |
| `{tone: dry humour}...{/tone}` | dowolny ton, własnymi słowami |
| `{cue: look at the back row}` | coś do zrobienia lub zapamiętania |
| `{wpm 130}` | tempo mówienia od tego miejsca, w słowach na minutę |
| `{budget 3m}` | czas przewidziany na tę sekcję, umieszczany po jej nagłówku |

Słowa są zawsze angielskie, niezależnie od języka wystąpienia, dzięki czemu plik znaczy dla wszystkich to samo. Źle zapisany znacznik pozostaje zwykłym tekstem; panel je zlicza.

## Dodawanie znaczników

- *Kliknięcie prawym przyciskiem myszy* w trybie przemówienia otwiera pierścień **Znaczniki przemówienia** ze znacznikami do wyboru. Zaznaczony tekst jest otaczany parą znaczników.
- `Ctrl+Shift+.` dodaje pauzę, `Ctrl+Shift+,` krótką pauzę (beat), a `Ctrl+Shift+E` akcent.
- **Ustawienia > Znaczniki przemówienia** pozwalają tworzyć własne znaczniki i przepisy (kilka znaczników jednym kliknięciem).

## Czas, teleprompter, próba

Panel sumuje czas (słowa w Twoim tempie plus pauzy) i porównuje go z budżetem każdej sekcji. **Widok > Teleprompter** otwiera okno, które przewija tekst w Twoim tempie, a próba zapisuje, ile naprawdę trwała każda część. **Kopiuj dla AI** kopiuje wystąpienie z krótkim objaśnieniem znaczników. Teleprompter ma własny temat w menu Pomoc.

## Punkty wyjścia

**Biblioteka > Szablony > Dodaj szablony startowe** zawiera już oznaczone przemówienie (toast) i prezentację (wystąpienie lub pitch) w każdym języku programu Caret.
