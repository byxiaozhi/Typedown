# Recenzja: komentarze i zmiany

Recenzja działa jak narzędzia do recenzowania w edytorze tekstu, ale wszystko jest zapisane w dokumencie jako zwykły tekst, więc może to przeczytać także współpracownik, inny edytor i asystent AI. Znaczniki korzystają z publicznej konwencji o nazwie CriticMarkup:

| Co widzisz | Jak jest zapisane w pliku |
|---|---|
| zielony dodany tekst | `{++added++}` |
| czerwony usunięty tekst | `{--deleted--}` |
| zamiana | `{~~old~>new~~}` |
| żółty komentarz | `{==the text==}{>>@Name 2026-10-07: the note<<}` |

Nic nie jest przechowywane w innym miejscu, a recenzja nigdy nie włącza się sama: pisanie i usuwanie nie tworzą znaczników.

## Dodawanie komentarza

Zaznacz tekst, kliknij prawym przyciskiem myszy i wybierz **Dodaj komentarz** (albo **Recenzja > Dodaj komentarz**). Wpisz uwagę i sprawdź swoje imię (na początku jest to nazwa Twojego użytkownika w systemie Windows). Tekst zostanie podświetlony na żółto, a obok pojawi się uwaga. Jest wpisana w dokumencie, więc **Cofnij** działa.

## Porównanie z innym plikiem

**Recenzja > Porównaj z innym plikiem...** prosi o wcześniejszą wersję dokumentu (na przykład kopię, którą wysłano) i o to, kto wprowadził zmiany. Caret zapisuje różnice między tamtą wersją a dokumentem widocznym na ekranie jako recenzję *w nowej karcie*; Twój dokument pozostaje bez zmian. Użyj tego, aby zobaczyć, co współpracownik zmienił w zwróconym pliku. Jeśli karty są wyłączone (**Ustawienia > Karty i okna**), recenzja zastępuje Twój dokument w oknie, więc najpierw zapisz swój dokument.

## Śledzenie zmian podczas edycji

**Recenzja > Śledź zmiany** (także przycisk w panelu Recenzja) zapamiętuje dokument w obecnej postaci. Gdy edytujesz, chwilę po przerwaniu pisania Caret pokazuje, co zmieniłeś: prawy panel widoku **Podział** rysuje dokument z dodanymi fragmentami na zielono i usuniętymi na czerwono, każdy z Twoim imieniem i dniem, a panel Recenzja wymienia zmiany i je liczy (także na pasku stanu). Nic nie jest zapisywane w pliku. **Zatrzymaj śledzenie** zapomina wersję wyjściową i zostawia dokument bez zmian. Lista służy też do poruszania się po dokumencie: *kliknij zmianę*, a Caret przeskoczy do niej w edytorze i wyróżni ją w prawym panelu, niezależnie od długości dokumentu. **Poprzednia zmiana** i **Następna zmiana** przechodzą od jednej do drugiej. ✓ obok zmiany ją akceptuje (staje się częścią wersji wyjściowej i znika z listy); ✗ ją odrzuca (dawny tekst wraca do dokumentu, a **Cofnij** działa). **Cofnij akceptację** przywraca ostatnio zaakceptowane zmiany. **Zaakceptuj wszystkie zmiany** i **Odrzuć wszystkie zmiany** robią to samo ze wszystkimi śledzonymi zmianami naraz. Różnice w blokach kodu można zaakceptować lub odrzucić tylko wszystkie razem.

W widoku Wizualnym zmiany są rysowane w samym tekście: dodany tekst ma zielone tło, a mały czerwony znak stoi tam, gdzie tekst usunięto. Wskaż zmianę lub znak, a karta pokaże dawny tekst, kto i kiedy, z przyciskami **Zaakceptuj zmianę** i **Odrzuć zmianę**. Kliknięcie zmiany w panelu Recenzja przewija do niej dokument i na chwilę ją obwodzi. Zmiana, której Caret nie znajdzie w tekście strony, nie jest tam rysowana, ale zostaje na liście i w widoku Podział.

Śledzenie trwa dalej, gdy zamkniesz plik i otworzysz go ponownie: Caret przechowuje wersję wyjściową, Twoje imię i dzień, w którym każda zmiana została zobaczona po raz pierwszy, we własnym folderze danych (nigdy w Twoim pliku i nie dla dokumentu, który nigdy nie był zapisany), a po otwarciu pliku zmiany są znów wypisane. Zmiana zachowuje dzień, w którym została zobaczona po raz pierwszy; nie staje się „dzisiejszą” każdego ranka. Jeśli plik został w międzyczasie zmieniony w innym programie, te zmiany też pojawiają się jako zmiany, a panel o tym informuje. **Zatrzymaj śledzenie** usuwa to, co zostało zachowane, tak samo jak przeniesienie pliku do Kosza z poziomu Carety; to, czego nie otwarto przez 90 dni, jest sprzątane.

**Zapisz zmiany w dokumencie** zamienia śledzone zmiany w znaczniki recenzji w samym tekście: każda zmiana staje się `{++dodane++}`, `{--usunięte--}` lub `{~~stare~>nowe~~}`, a po niej Twoje imię i dzień, w którym została zobaczona po raz pierwszy, czyli te same znaczniki, które zapisuje **Porównaj z innym plikiem...**. Najpierw pyta, to jeden krok **Cofnij**, a śledzenie się zatrzymuje. Od tej chwili to zwykłe znaczniki: wyślij plik koledze, a zmiany zaakceptuj lub odrzuć po jednej (prawy przycisk myszy na zmianie) albo wszystkie naraz (**Zaakceptuj wszystkie zmiany**, **Odrzuć wszystkie zmiany**). Różnice, których nie dało się oznaczyć, zostają zwykłym tekstem, o czym mówi okno.

## Akceptowanie i odrzucanie

- *Kliknij zmianę prawym przyciskiem myszy* (zieloną, czerwoną lub zamianę): **Zaakceptuj zmianę** zachowuje to, co zmiana mówi (dodania zostają, usunięcia znikają); **Odrzuć zmianę** przywraca stary tekst.
- *Kliknij komentarz prawym przyciskiem myszy:* **Usuń komentarz**.
- **Recenzja > Zaakceptuj wszystkie zmiany**, **Odrzuć wszystkie zmiany** i **Usuń wszystkie komentarze** robią to w całym dokumencie, jako jeden krok Cofnij.

## Ukrywanie znaczników

**Ustawienia > Edytor > Pokaż znaczniki recenzji** wyłącza kolory; znaczniki są wtedy widoczne jako zwykły tekst, którym w rzeczywistości są.

## Warto wiedzieć

- Znaczniki są tekstem, więc plik z recenzją można zapisać, wysłać i otworzyć w dowolnym edytorze.
- Zwrócony plik ze znacznikami współpracownika czyta się tak samo: jego zmiany są widoczne w kolorze, a Ty je akceptujesz lub odrzucasz.