<p align="center">
  <img alt="Caret" src="./logo.png" width="96" />
</p>

<h1 align="center">Caret</h1>

<p align="center">
  <strong>Napisz to. Śledź zmiany. Powiedz to.</strong><br />Edytor Markdown dla Windows ze śledzeniem zmian i teleprompterem, który zamienia dokumenty Office na Markdown gotowy dla AI, a Markdown na prawdziwe pliki Worda.
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · Polski · <a href="README.pt.md">Português</a>
</p>

<p align="center">
  <img alt="Śledzenie zmian w programie Caret: dodane fragmenty na zielono, usunięte na czerwono i lista zmian" src="docs/store/screenshots/pl/1-track-changes.png" width="880" />
</p>

---

## Śledzenie zmian, wbudowane

Włącz **Recenzja > Śledź zmiany**, a Caret zapamięta wersję, od której zaczynasz. Podczas edycji każde dodanie i każde usunięcie jest rysowane kolorem: na zielono to, co zostało dodane, na czerwono to, co zostało usunięte, w widoku Wizualnym oraz obok kodu w widoku Podział.

- **Lista zmian**, po której poruszasz się po dokumencie: kliknij zmianę, aby do niej przejść, i zaakceptuj ją lub odrzuć, albo zaakceptuj lub odrzuć wszystkie naraz
- **Każda zmiana pamięta dzień, w którym zobaczono ją po raz pierwszy**, a śledzenie trwa po zamknięciu programu Caret (wersję początkową Caret przechowuje we własnym folderze danych, nigdy w Twoim pliku)
- **Zapisz zmiany w dokumencie** poleceniem **Recenzja > Zapisz zmiany w dokumencie**: autor i data stają się zwykłym tekstem w pliku, więc współpracownik albo asystent AI może je przeczytać bez programu Caret
- **Komentarze i porównywanie**: dodaj komentarz do zaznaczonego tekstu albo porównaj plik z wcześniejszą wersją, aby zobaczyć, co zmienił współpracownik
- Bez wtyczek, bez serwera, bez konta. Znaczniki to [CriticMarkup](https://criticmarkup.com), otwarta konwencja zwykłego tekstu, którą rozumieją także inne narzędzia

## Znaczniki przemówienia i teleprompter

<p align="center">
  <img alt="Teleprompter w programie Caret: tekst przesuwający się w Twoim tempie, pasek czytania i zegar przemówienia" src="docs/store/screenshots/pl/2-teleprompter.png" width="880" />
</p>

Dla każdego, kto wygłasza przemówienia. Napisz przemówienie w programie Caret i zaznacz w samym tekście, jak je wygłosić:

- **Znaczniki przemówienia**: pauzy, wolniejsze lub szybsze tempo, nacisk, fragmenty głośne i ciche, ton i wskazówki, zapisane jako małe znaczniki zwykłego tekstu. Własne znaczniki i przepisy na jedno kliknięcie stworzysz w **Ustawienia > Znaczniki przemówienia**
- **Czas**: Caret zlicza czas przemówienia (Twoje słowa w Twoim tempie plus pauzy) i porównuje go z minutami, które masz na każdą sekcję, z sygnalizacją świetlną
- **Teleprompter** (**Widok > Teleprompter**): otwiera się na drugim ekranie, jeśli jest. Tekst przesuwa się płynnie w Twoim tempie, od 25% do 300%, z odliczaniem, paskiem czytania, odliczaniem do następnej pauzy, listą sekcji do przeskoku oraz do wyboru szerokością tekstu, interlinią i zestawem kolorów (także o wysokim kontraście), a także lustrem i odwróceniem dla szyby teleprompteru. Może podążać za planem albo poruszać się ze stałą prędkością
- **Zegar przemówienia** (**Widok > Zegar przemówienia** lub w samym teleprompterze): czas wypowiedzi, czas, który został, przed planem lub za nim oraz godzina zakończenia
- **Próba**: przeczytaj przemówienie na głos i naciśnij kilka klawiszy, a Caret zmierzy, ile naprawdę trwał każdy akapit, i Twoje prawdziwe tempo. Dźwięk nie jest nagrywany ani rozpoznawany; podsumowanie zapisuje się w pliku `.rehearsal.md` obok przemówienia (albo trafia do schowka, gdy przemówienie nie ma jeszcze pliku)
- **Szablony startowe** przemówienia i prezentacji, we wszystkich językach programu Caret

| Klawisz w teleprompterze | Co robi |
| --- | --- |
| `Spacja` | Start lub stop (najpierw jest odliczanie) |
| `←` `→` | Poprzedni lub następny akapit (działa też pilot do prezentacji) |
| `↑` `↓` | Szybkość |
| Kółko myszy, kliknięcie | Przesuwa linię czytania lub przechodzi do klikniętego wiersza |
| `J`, `O` | Lista sekcji, opcje |
| `M`, `V`, `F` | Lustro, odwróć, pełny ekran |
| `E` | Próba |

Więcej w **Pomoc > Teleprompter** i **Pomoc > Znaczniki przemówienia**.

## Eksport do Worda

<p align="center">
  <img alt="Opcje eksportu do Worda w Caret: wygląd, strona, nagłówek i stopka oraz szablon" src="docs/store/screenshots/pl/10-word.png" width="880" />
</p>

**Plik > Eksportuj > Dokument Word (.docx)** zapisuje dokument jako prawdziwy plik Worda, na Twoim komputerze: Word nie musi być zainstalowany i nic nie jest wysyłane.

- **Prawdziwa struktura Worda**: nagłówki Worda (więc działa okienko nawigacji), listy, tabele z powtarzanym wierszem nagłówka, cytaty, kod, łącza i obrazy z tekstem alternatywnym
- **Przypisy, równania i diagramy**: przypisy są przypisami Worda, wzory `$...$` są równaniami Worda, a diagramy mermaid, flowchart, sequence i vega-lite są obrazami
- **Twoja recenzja idzie razem z nim**: komentarze i zmiany stają się komentarzami i śledzonymi zmianami Worda, z autorami i datami. Dokument, w którym śledzisz zmiany, można wyeksportować razem z nimi, bez zapisywania ich w pliku
- **Spis treści i łącza w dokumencie**: wiersz `[TOC]` i łącza `[tekst](#nagłówek)` działają w Wordzie
- **Wygląd i własny szablon**: cztery wyglądy (Prosty, Raport, Pismo, Nowoczesny), A4, Letter lub Legal, orientacja, marginesy, nagłówek, stopka i numery stron, albo zapis na własnym szablonie Worda, tak aby użyć czcionek, strony, nagłówka i stopki Twojej firmy
- W drugą stronę **Konwersja na Markdown** zamienia plik Worda na Markdown

Więcej w **Pomoc > Eksport do Worda**.

## Odnajdź się w programie

- **Paleta poleceń** (`Ctrl+Shift+P`, **Widok > Paleta poleceń**): wpisz kilka liter tego, co chcesz zrobić, polecenia, ustawienia lub notatki, i naciśnij Enter
- **Panel inspektora** (`Ctrl+Shift+I`): kolumna obok notatki z jej konspektem, znacznikami recenzji, czasem przemówienia, wszystkimi trafieniami wyszukiwania i **Historią** notatki: wcześniejszymi zapisanymi wersjami, które można porównać z obecnym tekstem i przywrócić. Wersje zostają na Twoim komputerze (50 na notatkę, żadna starsza niż 30 dni), a historię można wyłączyć na karcie Historia
- **Mapa dokumentu**: cienki pasek przy prawej krawędzi pokazuje, w którym miejscu notatki jesteś i gdzie są nagłówki, trafienia wyszukiwania, komentarze i zmiany
- **Mały pasek narzędzi nad zaznaczonymi słowami** ze zwykłymi formatami i **Kopiuj jako tekst WhatsApp** oraz **pasek stanu**, który mówi to, co ważne (zapisano, słowa, czas czytania, liczby recenzji) i nic więcej
- **Upuść pliki w dowolnym miejscu okna** (Word, PDF, Excel, PowerPoint, CSV, e-maile lub folder), aby zamienić je na Markdown. Taca pokazuje zadania, z Otwórz, Pokaż w folderze, **Porównaj** (źródło i Markdown obok siebie) i Ponów
- **Strona główna**, o jedno kliknięcie przyciskiem domku po lewej od kart (`Ctrl+Shift+H`): notatka do dokończenia, pole na szybką notatkę, komentarze czekające w Twoich notatkach, wskazówka dnia, ostatnie pliki i foldery. Pokazanie strony głównej niczego nie zamyka
- **Najpierw klawiatura**: naciśnij `Alt`, aby zobaczyć, który klawisz dociera do każdego przycisku, oraz `Ctrl+Shift+/`, aby zobaczyć listę wszystkich skrótów
- **Karty wyglądu** w Ustawieniach, z podglądem schematu kolorów na żywo i powrotem do niego jednym kliknięciem
- **Co nowego**: Pomoc > Co nowego wymienia, co zmieniło się w każdej wersji

## Dlaczego Caret

Większość naszej wiedzy znajduje się w dokumentach Word, arkuszach Excel, prezentacjach PowerPoint i plikach PDF. Asystenci AI, tacy jak Copilot i ChatGPT, lepiej pracują ze zwykłym tekstem, a każdy dołączony plik kosztuje tokeny, czas i limity przesyłania.

**Caret konwertuje te pliki na Markdown**: czysty tekst, który zachowuje nagłówki, listy, tabele, łącza i przypisy, a pomija resztę. Wynik jest zwykle **o 90–99% mniejszy** niż plik oryginalny, czytelny dla ludzi i dla AI, i nigdy nie opuszcza Twojego komputera.

Następnie Caret daje Ci natywny, spokojny edytor Markdown do czytania, edytowania i porządkowania wyniku.

| Przykład (testy programu Caret) | Oryginał | Markdown | ≈ Tokeny |
| --- | --- | --- | --- |
| Raport kwartalny, Word | 77 KB | 8,8 KB | 2 183 |
| Ten sam raport w formacie PDF | 278 KB | 8,7 KB | 2 161 |
| Arkusz sprzedaży, Excel | 9,3 KB | 0,2 KB | 56 |
| Prezentacja, PowerPoint | 41 KB | 0,2 KB | 58 |

Liczba tokenów jest szacunkowa (około czterech znaków na token); dokładna liczba zależy od modelu AI.

## Konwersja na Markdown

<p align="center">
  <img alt="Caret konwertuje pliki Word, Excel, PowerPoint i PDF na Markdown" src="docs/store/screenshots/pl/4-convert.png" width="880" />
</p>

Otwórz **Konwertuj na Markdown** na pasku bocznym, tuż pod pozycją Strona główna, i upuść pliki lub cały folder. Możesz też obejść się bez otwierania okna programu Caret: w Eksploratorze plików kliknij prawym przyciskiem myszy plik, kilka plików lub folder i wybierz **Konwertuj na Markdown** (w wersji ze sklepu Microsoft Store i w wersji instalowanej; administrator może to wyłączyć, zobacz [przewodnik wdrażania](docs/deployment.md)).

- **Word** (.docx): nagłówki, listy zagnieżdżone, pogrubienie i kursywa, łącza, tabele (także ze scalonymi komórkami), przypisy dolne i obrazy
- **Excel** (.xlsx): każdy widoczny arkusz jako tabela, z czytelnymi datami, wartościami procentowymi i wynikami formuł
- **PowerPoint** (.pptx): jedna sekcja na slajd, z poziomami punktorów, tabelami i notatkami prelegenta
- **PDF**: nagłówki, listy, kod, tabele (z liniami i bez, z zachowanymi nagłówkami obejmującymi kilka kolumn) oraz kolumny, odtworzone i czytane we właściwej kolejności; łącza internetowe zachowane, nagłówki i stopki stron, numery stron i pieczątki na marginesach usunięte
- **CSV**: przecinek lub średnik, wykrywane automatycznie
- **E-maile z programu Outlook** (.msg, .eml): cały wątek w jednym pliku, każda odpowiedź jako osobna wiadomość, od najstarszej do najnowszej, bez podpisów, stopek prawnych i banerów „nadawca zewnętrzny”, a załączniki przekonwertowane w tym samym miejscu. Mają własne wejście: **E-maile z programu Outlook** na pasku bocznym i na stronie głównej oraz karta z poleceniem **Wybierz e-maile...** na stronie konwersji. E-maile zapisane z programu Outlook (przeciągnięte z programu Outlook do folderu) można też upuścić jak każdy inny plik

**Maskuj dane osobowe** (domyślnie włączone, na karcie e-maili) zastępuje imiona i nazwiska, adresy e-mail, numery telefonów, numery IBAN, numery kart i dokumentów tożsamości symbolami zastępczymi, takimi jak `[PERSON-1]`, zawsze tym samym dla tej samej osoby, aby wątek pozostał czytelny po wklejeniu do asystenta AI. Działa to na podstawie reguł i sum kontrolnych, a nie AI: osoby są rozpoznawane po nadawcach i odbiorcach e-maila oraz po imionach w zwrotach powitalnych („Cześć Marto,”) i grzecznościowych, więc imię, które pojawia się tylko w środku zdania, nie zostanie znalezione. Rozumiane są e-maile po polsku, angielsku, francusku i hiszpańsku; reguły to [pliki JSON](plugins/markitdown-email/src/markitdown_caret_email/rules), które firma może uzupełnić.

Przy każdym pliku widać jego rozmiar przed konwersją i po niej oraz szacunkową liczbę tokenów. **Kopiuj wszystko dla AI** umieszcza całość w Schowku jako jeden tekst, gotowy do wklejenia w asystencie. Pliki Markdown są zapisywane obok oryginałów lub w wybranym folderze, a żaden istniejący plik nie jest nigdy nadpisywany.

**Wszystko jest wbudowane**: bez środowiska Python, bez dodatków i bez połączenia z internetem; nic nie jest wysyłane online. Dzięki temu Caret nadaje się do komputerów firmowych, na których instalowanie narzędzi jest ograniczone.

## Spokojny edytor Markdown

<p align="center">
  <img alt="Przekonwertowany raport otwarty w programie Caret, na kartach obok innych dokumentów" src="docs/store/screenshots/pl/6-tabs.png" width="880" />
</p>

- **Wizualny, Kod lub Podział**: edycja z formatowaniem, surowy Markdown albo oba widoki obok siebie z podglądem na żywo
- **Pasek narzędzi formatowania**, menu Akapit i Format oraz znane skróty klawiszowe
- **Tabele, wzory, przypisy dolne i diagramy** (Mermaid, schematy blokowe, diagramy sekwencji, PlantUML, Vega-Lite)
- **Wklejaj obrazy i zrzuty ekranu** bezpośrednio do notatki
- **Karty**: kilka dokumentów w jednym oknie, każdy z własną historią cofania. `Ctrl+Tab` przełącza między nimi, `Ctrl+W` zamyka dokument, a okno pozostaje otwarte ze stroną główną. Zapisane dokumenty otwierają się ponownie po następnym uruchomieniu (można to wyłączyć w Ustawieniach)
- **Obszar roboczy oparty na folderze**, **Przejdź do pliku** (`Ctrl+K`), ulubione, ostatnie pliki, szablony i kosz
- **Zapisywanie automatyczne** i **odzyskiwanie po awarii**, także dla notatek bez nazwy
- **Sprawdzanie pisowni** z czerwonym falistym podkreśleniem i propozycjami po kliknięciu prawym przyciskiem myszy, oparte na wbudowanym sprawdzaniu pisowni systemu Windows, bez połączenia z internetem
- **Recenzowanie jak w programie Word, tylko w kolorach**: komentarze na żółto, dodany tekst na zielono, usunięty na czerwono; zaakceptuj lub odrzuć każdą zmianę albo wszystkie naraz, a po otrzymaniu zmienionego pliku porównaj go z wersją wysłaną. Wszystko jest zapisane w samym dokumencie jako zwykły tekst ([CriticMarkup](https://criticmarkup.com)), który czyta zarówno człowiek, jak i asystent AI
- **Polski, angielski, francuski i hiszpański**, motywy jasny i ciemny, eksport do HTML, PDF lub zwykłego tekstu

## Pobierz program Caret

- **Microsoft Store** (zalecane): [pobierz program Caret ze sklepu Microsoft Store](https://apps.microsoft.com/detail/9n617shlqm8g) lub uruchom `winget install --source msstore --id 9N617SHLQM8G`. Bez certyfikatu do zatwierdzania, a aktualizacje instalują się same.
- **GitHub**: pobierz najnowszy plik `.msix` i `Caret.cer` z [opublikowanych wersji](https://github.com/fegyenc/Caret/releases/latest), a następnie wykonaj dwa kroki instalacji opisane w [angielskim README](README.md#get-caret).
- **Dla dużych firm, małych i średnich przedsiębiorstw oraz osób prowadzących jednoosobową działalność**: [docs/deployment.md](docs/deployment.md) opisuje wdrażanie za pomocą usługi Intune i Portalu firmy, wykorzystanie sieci oraz zasady, które wyłączają sprawdzanie aktualizacji albo ustawiają dla wszystkich domyślny układ i kolory (po angielsku).

Wymaga systemu Windows 10 w wersji 1809 lub nowszej (x64 lub ARM64); zalecany jest system Windows 11.

## Prywatność

Caret niczego nie zbiera: bez konta, bez telemetrii. Dokumenty są konwertowane i edytowane na Twoim komputerze. Jedyne połączenie, które Caret nawiązuje samodzielnie, to sprawdzanie aktualizacji mniej więcej raz dziennie w wersjach niezainstalowanych ze sklepu Microsoft Store (z serwisu GitHub albo pakiet wdrożony przez Twoją organizację); można je wyłączyć, a wersja ze sklepu w ogóle go nie wykonuje. Szczegóły: [PRIVACY.md](PRIVACY.md#polski).

## Licencja i współpraca

[Licencja MIT](LICENSE). Caret jest pochodną projektu [Typedown](https://github.com/byxiaozhi/Typedown) autorstwa ZZF. Składniki innych firm wymieniono w pliku [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Uwagi, pomysły i poprawki tłumaczenia są mile widziane w [zgłoszeniach na GitHubie](https://github.com/fegyenc/Caret/issues); zobacz też [docs/localization.md](docs/localization.md).
