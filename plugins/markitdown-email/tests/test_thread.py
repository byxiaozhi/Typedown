from markitdown_caret_email._dates import parse_date
from markitdown_caret_email._thread import clean_body, normalise_subject, split_thread


def test_outlook_header_blocks_in_four_languages(rules):
    body = """Latest reply.

________________________________
From: Anna Nowak <anna@client.fr>
Sent: Monday, March 3, 2025 4:05 PM
To: Jan Kowalski <jan@acme.pl>
Subject: RE: Budget

English reply.

________________________________
De : Pedro García <pedro@acme.es>
Envoyé : lundi 3 mars 2025 10:12
À : Anna Nowak <anna@client.fr>; Jan Kowalski <jan@acme.pl>
Cc : Marta Wiśniewska <marta@acme.pl>
Objet : RE: Budget

French reply.

-----Mensaje original-----
De: Marta Wiśniewska <marta@acme.pl>
Enviado el: viernes, 28 de febrero de 2025 11:00
Para: Pedro García <pedro@acme.es>
Asunto: RE: Budget

Spanish reply.

Od: Jan Kowalski <jan@acme.pl>
Wysłano: czwartek, 27 lutego 2025 09:15
Do: Marta Wiśniewska <marta@acme.pl>
DW: Anna Nowak <anna@client.fr>
Temat: Budget

Polish original.
"""
    messages = split_thread(body, rules)
    assert [m.body for m in messages] == [
        "Latest reply.",
        "English reply.",
        "French reply.",
        "Spanish reply.",
        "Polish original.",
    ]
    assert [m.sender.email if m.sender else None for m in messages] == [
        None,
        "anna@client.fr",
        "pedro@acme.es",
        "marta@acme.pl",
        "jan@acme.pl",
    ]
    assert [m.date for m in messages[1:]] == [
        "2025-03-03 16:05",
        "2025-03-03 10:12",
        "2025-02-28 11:00",
        "2025-02-27 09:15",
    ]
    assert [a.email for a in messages[2].to] == ["anna@client.fr", "jan@acme.pl"]
    assert [a.name for a in messages[2].cc] == ["Marta Wiśniewska"]
    assert [a.name for a in messages[4].cc] == ["Anna Nowak"]


def test_wrote_lines_and_nested_quotes(rules):
    body = """Top answer.

Le lun. 3 mars 2025 à 10:12, Anna Nowak <anna@client.fr> a écrit :
> Réponse française.
>
> W dniu pon., 3 mar 2025 o 09:00 Jan Kowalski <jan@acme.pl> napisał(a):
>> Polska wiadomość.
>>
>> El vie, 28 feb 2025 a las 11:00, Pedro García (<pedro@acme.es>) escribió:
>>> Mensaje en español.
"""
    messages = split_thread(body, rules)
    assert [m.body for m in messages] == [
        "Top answer.",
        "Réponse française.",
        "Polska wiadomość.",
        "Mensaje en español.",
    ]
    assert [str(m.sender) for m in messages[1:]] == [
        "Anna Nowak <anna@client.fr>",
        "Jan Kowalski <jan@acme.pl>",
        "Pedro García <pedro@acme.es>",
    ]
    assert messages[1].date == "2025-03-03 10:12"
    assert messages[3].date == "2025-02-28 11:00"


def test_gmail_wrote_line_wrapped_over_two_lines(rules):
    body = "Sure.\n\nOn Mon, Mar 3, 2025 at 4:05 PM Anna Nowak <\nanna@client.fr> wrote:\n> Can you check?\n"
    messages = split_thread(body, rules)
    assert [m.body for m in messages] == ["Sure.", "Can you check?"]
    assert messages[1].sender.email == "anna@client.fr"
    assert messages[1].date == "2025-03-03 16:05"


def test_bottom_posted_text_stays_with_the_newer_message(rules):
    body = "On Mon, 3 Mar 2025 at 10:00, Anna <anna@x.fr> wrote:\n> Question?\n\nAnswer below the quote.\n"
    messages = split_thread(body, rules)
    assert messages[0].body == "Answer below the quote."
    assert messages[1].body == "Question?"


def test_wrote_line_without_quote_marks(rules):
    body = "Fine by me.\n\nOn Mon, 3 Mar 2025 at 10:00, Anna <anna@x.fr> wrote:\n\nShall we meet?\n"
    messages = split_thread(body, rules)
    assert [m.body for m in messages] == ["Fine by me.", "Shall we meet?"]


def test_forwarded_message_is_marked(rules):
    body = """FYI

---------- Forwarded message ---------
From: Anna Nowak <anna@client.fr>
Date: Mon, 3 Mar 2025 at 10:12
Subject: Offer
To: Jan Kowalski <jan@acme.pl>

The offer.
"""
    messages = split_thread(body, rules)
    assert [m.body for m in messages] == ["FYI", "The offer."]
    assert messages[1].forwarded
    assert not messages[0].forwarded


def test_from_line_in_ordinary_text_is_not_a_boundary(rules):
    body = "From: our side everything is ready.\nTo: be confirmed tomorrow.\n\nThanks"
    assert len(split_thread(body, rules)) == 1


def test_signature_cut_after_closing_and_name(rules):
    body = """Please find the numbers attached.

Best regards,
Anna Nowak
Purchasing Manager | Client SA
+33 1 23 45 67 89
www.client.fr"""
    assert clean_body(body, rules) == "Please find the numbers attached.\n\nBest regards,\nAnna Nowak"


def test_signature_cut_in_each_language(rules):
    for closing in ("Cordialement,", "Un saludo,", "Pozdrawiam", "Z poważaniem"):
        body = f"Texte.\n\n{closing}\nMarta Wiśniewska\nDyrektor finansowy\ntel. 612 345 678"
        assert clean_body(body, rules) == f"Texte.\n\n{closing}\nMarta Wiśniewska", closing


def test_closing_with_name_on_the_same_line(rules):
    body = "Done.\n\nThanks, Anna\nPurchasing | Client SA\n+33 1 23 45 67 89"
    assert clean_body(body, rules) == "Done.\n\nThanks, Anna"


def test_uncertain_signature_is_left_alone(rules):
    # "Thanks!" is followed by a question, not a name: nothing may be cut
    body = "Hi,\n\nThanks!\nCan you send the file?\nAnna"
    assert clean_body(body, rules) == body


def test_keep_signatures(rules):
    body = "Text.\n\nBest regards,\nAnna Nowak\nManager"
    assert clean_body(body, rules, keep_signatures=True) == body


def test_dash_dash_signature(rules):
    assert clean_body("Text.\n\n-- \nAnna\nwww.example.com", rules) == "Text."


def test_banners_mobile_signatures_and_disclaimers(rules):
    body = """CAUTION: External email. Do not click links unless you trust the sender.

Hola,

Te confirmo la reunión.

Enviado desde mi iPhone

Este mensaje y sus archivos adjuntos son confidenciales y están dirigidos exclusivamente a su destinatario."""
    assert clean_body(body, rules) == "Hola,\n\nTe confirmo la reunión."


def test_disclaimer_phrase_inside_the_message_is_kept(rules):
    body = "If you are not the intended recipient of the invoice, tell me and I'll fix the address.\n\nThanks"
    assert clean_body(body, rules) == body


def test_typographic_apostrophes_and_nbsp_in_phrases(rules):
    body = "Merci.\n\nSi vous n’êtes pas le destinataire de ce message, merci de le détruire."
    assert clean_body(body, rules) == "Merci."


def test_subject_prefixes(rules):
    assert normalise_subject("RE: TR: [EXTERNAL] Budget", rules) == "Budget"
    assert normalise_subject("RV: RE : Presupuesto", rules) == "Presupuesto"
    assert normalise_subject("Odp: PD: Budżet", rules) == "Budżet"
    assert normalise_subject("Report: Q1", rules) == "Report: Q1"


def test_dates(rules):
    m = rules.months
    assert parse_date("Monday, March 3, 2025 4:05 PM", m) == "2025-03-03 16:05"
    assert parse_date("lundi 3 mars 2025 10:12", m) == "2025-03-03 10:12"
    assert parse_date("mardi 4 février 2025 09h30", m) == "2025-02-04 09:30"
    assert parse_date("viernes, 28 de febrero de 2025 11:00", m) == "2025-02-28 11:00"
    assert parse_date("vie, 28 feb 2025 a las 11:00 p. m.", m) == "2025-02-28 23:00"
    assert parse_date("czwartek, 27 lutego 2025 09:15", m) == "2025-02-27 09:15"
    assert parse_date("Tue, 04 Mar 2025 09:30:00 +0000", m) == "2025-03-04 09:30"
    assert parse_date("2025-03-04 09:30", m) == "2025-03-04 09:30"
    assert parse_date("25/03/2025 10:00", m) == "2025-03-25 10:00"
    assert parse_date("03/04/2025 10:00", m) is None  # March 4th or April 3rd: unknowable
    assert parse_date("sometime next week", m) is None


def test_outlook_html_header_block_with_a_blank_line(rules):
    # What an HTML-only Outlook mail looks like once turned into text: bold labels, and a
    # blank line after "From:"
    body = (
        "Reply.\n\n**From:** Anna Nowak <anna@client.fr>\n\n**Sent:** Monday, March 3, 2025 4:05 PM\n"
        "**To:** Jan Kowalski <jan@acme.pl>\n**Subject:** RE: Budget\n\nOriginal.\n"
    )
    messages = split_thread(body, rules)
    assert [m.body for m in messages] == ["Reply.", "Original."]
    assert messages[1].sender.email == "anna@client.fr"


def test_bold_name_bilingual_closing_and_website_under_disclaimer(rules):
    assert clean_body("Done.\n\nThank you\n\n**Anna Nowak**\n\nGeneral Secretary", rules) == (
        "Done.\n\nThank you\n\n**Anna Nowak**"
    )
    assert clean_body("Done.\n\nPozdrawiam / With Regards\n\nAN\n\nManager", rules) == (
        "Done.\n\nPozdrawiam / With Regards\n\nAN"
    )
    body = (
        "Numbers attached.\n\nKind regards,\n\n**Anna Nowak**\n\nAnalyst\n\n"
        "This message is for the designated recipient only and may contain privileged information.\n"
        "______________________\n\n[www.example.com](http://www.example.com/)"
    )
    assert clean_body(body, rules) == "Numbers attached.\n\nKind regards,\n\n**Anna Nowak**"


def test_a_link_ending_the_message_stays(rules):
    body = "See the report:\n\nhttps://example.com/report"
    assert clean_body(body, rules) == body


def test_bare_names_in_quoted_recipient_lines():
    from markitdown_caret_email._model import parse_address_list

    assert [a.name for a in parse_address_list("Jan Kowalski")] == ["Jan Kowalski"]
    assert [a.name for a in parse_address_list("Anna Nowak, Jan Kowalski")] == ["Anna Nowak", "Jan Kowalski"]
