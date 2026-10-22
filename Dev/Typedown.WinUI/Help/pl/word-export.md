# Eksport do Worda

**Plik** > **Eksportuj** > **Dokument Word (.docx)** zapisuje dokument jako prawdziwy plik Worda. Powstaje na Twoim komputerze: Word nie musi być zainstalowany i nic nie jest wysyłane.

## Czym staje się każdy element

- Nagłówki są nagłówkami Worda, więc znajdują je okienko nawigacji, spis treści i czytnik ekranu. Listy, tabele (wiersz nagłówka powtarza się na każdej stronie), cytaty, kod, łącza, pogrubienie, kursywa i reszta zostają zachowane.
- Obrazy trafiają do pliku w rozmiarze, jaki mają na ekranie, z tekstem alternatywnym. Są czytane z Twojego komputera; obraz z internetu nie jest pobierany, a obraz, którego nie udało się dodać, jest wymieniony w komunikacie, a jego opis zostaje w tekście.
- Przypisy dolne są przypisami dolnymi Worda. Wzory (`$x^2$`) są równaniami Worda. Diagramy (mermaid, flowchart, sequence, vega-lite) są obrazami; PlantUML wymaga serwera, więc zostaje kodem.
- Nagłówek na początku pliku („front matter”) daje tytuł, autora, temat, opis i słowa kluczowe pliku Worda i nie jest drukowany.
- Wiersz z `[TOC]` staje się spisem treści. Łącze do nagłówka, takie jak `[zobacz niżej](#budzet)`, prowadzi do tego nagłówka. Word uzupełnia numery stron po otwarciu pliku.

## Recenzja i przemówienie

Komentarze i zmiany zapisane jako znaczniki (zobacz **Recenzja: komentarze i zmiany**) stają się prawdziwymi komentarzami i śledzonymi zmianami Worda, z autorami i datami, więc współpracownik może je zaakceptować lub odrzucić w Wordzie.

Gdy śledzisz zmiany w dokumencie, Caret pyta, czy je wyeksportować: **Ze śledzonymi zmianami** pokazuje każdą zmianę od początku śledzenia, a **Tylko bieżący tekst** eksportuje tekst taki, jaki jest. W obu przypadkach nic nie jest zapisywane w Twoim dokumencie.

Znaczniki przemówienia (zobacz **Znaczniki przemówienia**) są małymi szarymi notatkami w pliku.

## Opcje

Opcje otwierają się najpierw i są zapamiętywane na następny raz.

- **Wygląd**: Prosty, Raport, Pismo lub Nowoczesny. Zmieniają czcionki, rozmiary, kolory i odstępy; struktura jest taka sama.
- **Rozmiar strony**, **Orientacja** i **Marginesy**.
- **Tekst nagłówka** i **Tekst stopki**: `{title}` jest zastępowane tytułem dokumentu, a `{date}` dzisiejszą datą. **Numery stron** są po prawej stronie stopki.
- **Spis treści na początku**.
- **Szablon**: wybierz własny plik lub szablon Worda (`.docx`, `.dotx`) przez **Wybierz szablon...**, a eksport zostanie zapisany na nim. Używane są jego style, strona, nagłówek i stopka, a jego własny tekst jest usuwany; opcje wyglądu i strony nie obowiązują. **Usuń** wraca do wyglądów.
