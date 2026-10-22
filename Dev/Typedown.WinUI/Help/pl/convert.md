# Konwersja dokumentów i e-maili

Caret zamienia dokumenty na Markdown: czysty tekst, który zachowuje nagłówki, listy, tabele, łącza i przypisy, a pomija czcionki i układ strony. Wynik zajmuje zwykle drobną część rozmiaru oryginału, jest łatwy do czytania dla ludzi, a asystenci AI przyjmują go przy znacznie mniejszej liczbie tokenów. Wszystko jest konwertowane na Twoim komputerze: nic nie jest wysyłane.

## Dokumenty

Otwórz **Konwertuj na Markdown** na pasku bocznym i upuść pliki albo cały folder, lub użyj **Wybierz pliki...**. Obsługiwane są Word, Excel, PowerPoint, PDF i CSV. Każdy wynik pokazuje rozmiar przed i po oraz szacunkową liczbę tokenów AI. **Kopiuj wszystko dla AI** umieszcza całość w schowku jako jeden tekst.

Możesz też kliknąć prawym przyciskiem myszy plik, kilka plików lub folder w *Eksploratorze plików* i wybrać **Konwertuj na Markdown** (niedostępne, gdy Twoja organizacja to wyłączy).

Pliki Markdown są zapisywane obok oryginałów albo w wybranym przez Ciebie folderze. Plik, który już tam jest, nigdy nie zostanie nadpisany.

## E-maile z programu Outlook

Otwórz **E-maile z programu Outlook** i wybierz e-maile zapisane z programu Outlook (`.msg`) lub z innych programów pocztowych (`.eml`). Caret zamienia całą korespondencję w jeden plik: każda odpowiedź jako osobna wiadomość, od najstarszej, bez podpisów, klauzul prawnych i banerów „nadawca zewnętrzny”, a załączniki są konwertowane w miejscu.

**Maskuj dane osobowe** (domyślnie włączone) zastępuje imiona i nazwiska, adresy e-mail, numery telefonów, numery rachunków bankowych i numery dokumentów znacznikami takimi jak `[PERSON-1]`, tym samym znacznikiem dla tej samej osoby, dzięki czemu rozmowa pozostaje czytelna po wklejeniu jej do asystenta AI. Działa na podstawie stałych reguł i cyfr kontrolnych, nie AI. Osoby są rozpoznawane po nadawcach i odbiorcach e-maila oraz po powitaniach i pożegnaniach, więc imię i nazwisko, które pojawia się tylko w środku zdania, nie zostanie znalezione. Sprawdź wynik, zanim go udostępnisz.

Rozumiane są e-maile po angielsku, francusku, hiszpańsku, polsku i portugalsku, a reguły to zwykłe pliki, które firma może rozszerzyć.

## Upuść pliki w dowolnym miejscu

Nie trzeba najpierw otwierać strony konwersji: przeciągnij pliki Word, PDF, Excel, PowerPoint lub wiadomości e-mail (albo cały folder) nad okno Caret, w dowolne miejsce, i puść je, gdy pojawi się **Konwertuj na Markdown**. Mała taca w prawym dolnym rogu wyświetla je na liście, każdy z paskiem postępu i — gdy wskaźnik jest nad nim lub klawiatura jest w nim — z przyciskami **Otwórz**, **Pokaż w Eksploratorze plików** (przycisk folderu) i **Porównaj**. Plik, którego nie udało się przekonwertować, podaje powód i ma przycisk **Ponów**; **Wyczyść listę** opróżnia tacę, a Esc ją zamyka.

**Porównaj** pokazuje oryginał jako zwykły tekst po lewej, a Markdown po prawej, i przewija je razem. Nad nimi widać użyte opcje: czy obrazy zostały wyodrębnione oraz — w wiadomości e-mail — czy dane osobowe zostały zamaskowane i ile znaleziono imion i nazwisk, adresów, numerów i identyfikatorów (w Markdown są pogrubione i podkreślone). Esc go zamyka. Użyj go, aby zobaczyć, co konwersja zachowała, pominęła lub zamaskowała, zanim udostępnisz wynik.

## Gdy coś się nie konwertuje

Plik zabezpieczony hasłem lub uszkodzony jest wymieniony razem z przyczyną. Zeskanowanego pliku PDF bez tekstu nie da się odczytać: Caret nie rozpoznaje tekstu na obrazach.
