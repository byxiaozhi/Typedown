import io

from markitdown import MarkItDown, StreamInfo

from markitdown_caret_email import EmailConverter, Options, load_rules, render
from markitdown_caret_email._msg import read_msg
from markitdown_caret_email.cli import main

from conftest import FakeOle, make_eml, make_msg_streams, unicode_prop

THREAD = """Hi Anna,

Approved. Pedro will send the IBAN FR76 3000 6000 0112 3456 7890 189 today.

Pozdrawiam
Jan Kowalski
Kierownik projektu | ACME
Tel. kom. +48 601 234 567

________________________________
De : Anna Nowak <anna.nowak@client.fr>
Envoyé : lundi 3 mars 2025 16:05
À : Jan Kowalski <jan.kowalski@acme.pl>
Cc : Pedro García <pedro@acme.es>
Objet : TR: [EXTERNAL] Budget 2026

Vous ne recevez pas souvent de courriers de anna.nowak@client.fr. Découvrez pourquoi ceci est important

Bonjour Jan,

Pouvez-vous valider le budget ? Mon numéro : 06 12 34 56 78.

Bien cordialement,
Anna Nowak
Responsable achats

Ce message et toutes les pièces jointes sont confidentiels et établis à l'intention exclusive de ses destinataires.

Envoyé de mon iPhone

-----Mensaje original-----
De: Pedro García <pedro@acme.es>
Enviado el: viernes, 28 de febrero de 2025 11:00
Para: Anna Nowak <anna.nowak@client.fr>
Asunto: Budget 2026

Hola Anna,

Te adjunto el presupuesto. Mi DNI es 12345678Z.

Un saludo,
Pedro García
Tel: 912 34 56 78

Este mensaje y sus archivos adjuntos son confidenciales.
"""


def convert(data: bytes, extension=".eml", **kwargs):
    md = MarkItDown(enable_plugins=False)
    md.register_converter(EmailConverter(md), priority=-1.0)
    return md.convert_stream(io.BytesIO(data), stream_info=StreamInfo(extension=extension, filename="mail" + extension), **kwargs)


def eml(**kwargs):
    kwargs.setdefault("subject", "RE: TR: [EXTERNAL] Budget 2026")
    kwargs.setdefault("to", "Nowak, Anna <anna.nowak@client.fr>")
    kwargs.setdefault("cc", "Pedro García <pedro@acme.es>")
    return make_eml(THREAD, **kwargs)


def test_thread_is_split_cleaned_and_redacted():
    result = convert(eml(attachments=[("budget.csv", b"col1,col2\n1,2\n", "text/csv")]))
    md = result.markdown
    assert result.title == "Budget 2026"

    # Oldest first, with no clutter left
    assert md.index("Hola [PERSON-") < md.index("Bonjour [PERSON-") < md.index("Approved.")
    for clutter in ("Responsable achats", "Kierownik", "iPhone", "confidentiels", "confidenciales", "Découvrez"):
        assert clutter not in md

    # Nothing personal survives
    for secret in ("Anna", "Nowak", "Pedro", "García", "Kowalski", "@", "12345678Z", "FR76", "601 234", "06 12", "912 34"):
        assert secret not in md, secret

    # Stable placeholders: Pedro sends the first mail and is mentioned in the last
    first = md.split("## 1. ")[1].split(" ")[0]
    assert md.count(first) >= 4

    assert 'messages: 3' in md
    assert 'started: "2025-02-28 11:00"' in md
    assert 'last: "2025-03-04 09:30"' in md
    assert "| col1 | col2 |" in md
    assert "### budget.csv" in md


def test_redaction_can_be_turned_off():
    md = convert(eml(), email_redact=False).markdown
    assert "## 1. Pedro García <pedro@acme.es> · 2025-02-28 11:00" in md
    assert "*To: Anna Nowak <anna.nowak@client.fr> · Cc: Pedro García <pedro@acme.es>*" in md
    assert "Te adjunto el presupuesto. Mi DNI es 12345678Z." in md
    assert "redacted: false" in md


def test_environment_variables_for_the_markitdown_command(monkeypatch, tmp_path):
    names = tmp_path / "names.txt"
    names.write_text("# customers\nClient SA\n", encoding="utf-8")
    monkeypatch.setenv("CARET_EMAIL_REDACT", "0")
    assert "Pedro García" in convert(eml()).markdown
    monkeypatch.setenv("CARET_EMAIL_REDACT", "1")
    monkeypatch.setenv("CARET_EMAIL_NAMES", str(names))
    md = convert(make_eml("Offer for Client SA attached.")).markdown
    assert "Client SA" not in md


def test_extra_rules_file(tmp_path):
    rules = tmp_path / "company.json"
    rules.write_text('{"disclaimer_phrases": ["acme internal use only"]}', encoding="utf-8")
    body = "The numbers.\n\nACME internal use only. Do not forward."
    assert "internal use" in convert(make_eml(body)).markdown
    assert "internal use" not in convert(make_eml(body), email_rules=[str(rules)]).markdown


def test_html_only_mail():
    html = "<html><body><p>Hello <b>team</b>,</p><ul><li>one</li><li>two</li></ul></body></html>"
    md = convert(make_eml("", html=html), email_redact=False).markdown
    assert "Hello **team**," in md
    assert "* one" in md or "- one" in md


def test_attached_email_is_rendered_and_its_people_masked():
    inner = make_eml("Inner text from Marta.", subject="Original", sender="Marta Wiśniewska <marta@acme.pl>")
    outer = make_eml("See below.", attachments=[("Original.eml", inner, "message/rfc822")])
    md = convert(outer).markdown
    assert "Inner text from [PERSON-" in md
    assert "Marta" not in md
    assert "Wiśniewska" not in md


def test_attachments_can_be_left_unconverted():
    data = make_eml("See file.", attachments=[("budget.csv", b"a,b\n1,2\n", "text/csv")])
    md = convert(data, email_attachments=False).markdown
    assert "### budget.csv" in md
    assert "| a | b |" not in md


def test_msg_reader_with_recipients_date_and_embedded_item():
    streams = make_msg_streams(
        recipients=[
            ("Jan Kowalski", "jan.kowalski@acme.pl", 1),
            ("Pedro García", "pedro@acme.es", 2),
            ("Marta Wiśniewska", "marta@acme.pl", 3),
        ],
        body="Bonjour Jan,\n\nVoici l'offre.\n\nCordialement,\nAnna Nowak\nResponsable achats",
    )
    attach = "__attach_version1.0_#00000000"
    streams[f"{attach}/__properties_version1.0"] = b"\x00" * 8
    streams[f"{attach}/__substg1.0_3001001F"] = unicode_prop("Offre initiale")
    streams.update(
        make_msg_streams(
            prefix=f"{attach}/__substg1.0_3701000D",
            header_size=24,
            subject="Offre initiale",
            sender=("Pedro García", "pedro@acme.es"),
            body="Primera versión.",
        )
    )
    email = read_msg(FakeOle(streams), lambda html: html)
    assert email.subject == "RE: Offre"
    assert str(email.sender) == "Anna Nowak <anna.nowak@client.fr>"
    assert [a.email for a in email.to] == ["jan.kowalski@acme.pl"]
    assert [a.email for a in email.cc] == ["pedro@acme.es"]
    assert [a.email for a in email.bcc] == ["marta@acme.pl"]
    assert email.date is not None and email.date.startswith("2025-03-0")
    # The recipients of the attached item are its own, not the outer mail's
    assert len(email.attachments) == 1
    assert email.attachments[0].email.body == "Primera versión."

    md, title = render(email, load_rules(), Options())
    assert title == "Offre"
    assert "Responsable achats" not in md
    assert "Primera versión." in md
    assert "Pedro" not in md and "Anna" not in md
    assert "Bcc: [PERSON-" in md and "Marta" not in md


def test_msg_reader_non_unicode_polish_body():
    text = "Dzień dobry, proszę o zaświadczenie."
    streams = make_msg_streams(body=None, body8=(text.encode("cp1250"), 1250))
    assert read_msg(FakeOle(streams), lambda h: h).body == text


def test_plugin_is_registered_and_takes_over_msg_and_eml(tmp_path):
    md = MarkItDown(enable_plugins=True)
    names = [type(r.converter).__name__ for r in sorted(md._converters, key=lambda r: r.priority)]
    assert names.index("EmailConverter") < names.index("OutlookMsgConverter")
    path = tmp_path / "mail.eml"
    path.write_bytes(eml())
    assert md.convert(str(path)).markdown.startswith('---\ntype: "email-thread"')


def test_cli_never_overwrites(tmp_path, capsys):
    (tmp_path / "mail.eml").write_bytes(eml())
    (tmp_path / "notes.txt").write_text("ignored")
    assert main([str(tmp_path)]) == 0
    assert main([str(tmp_path)]) == 0
    assert sorted(p.name for p in tmp_path.glob("*.md")) == ["mail (2).md", "mail.md"]
    assert (tmp_path / "mail.md").read_text(encoding="utf-8").startswith("---")


def test_cli_stdout_and_output_folder(tmp_path, capsys):
    source = tmp_path / "mail.eml"
    source.write_bytes(eml())
    assert main([str(source), "--stdout", "--no-redact"]) == 0
    assert "Pedro García" in capsys.readouterr().out
    out = tmp_path / "out"
    assert main([str(source), "-o", str(out)]) == 0
    assert (out / "mail.md").exists()


def test_cli_reports_missing_input(tmp_path, capsys):
    assert main([str(tmp_path / "missing.msg")]) == 1


def test_cli_says_no_ai_is_used(tmp_path, capsys):
    source = tmp_path / "mail.eml"
    source.write_bytes(eml())
    assert main([str(source), "--stdout"]) == 0
    err = capsys.readouterr().err
    assert "No AI is used" in err
    assert "not masked" in err


def test_attached_mail_with_another_display_name_for_a_known_address():
    # The outer mail knows jan.kowalski@acme.pl as "Jan Kowalski"; the attached one
    # shows the same address under a name that shares no word with it
    inner = make_eml("Regards from Johnny.", subject="Original", sender="Johnny Walker <jan.kowalski@acme.pl>")
    outer = make_eml("See below.", attachments=[("Original.eml", inner, "message/rfc822")])
    md = convert(outer).markdown
    assert "Johnny" not in md
    assert "Walker" not in md


def test_cli_stdout_is_utf8(tmp_path, capfdbinary):
    source = tmp_path / "mail.eml"
    source.write_bytes(make_eml("Dzień dobry, proszę o zaświadczenie."))
    assert main([str(source), "--stdout"]) == 0
    assert "proszę o zaświadczenie".encode("utf-8") in capfdbinary.readouterr().out


PARTICIPANT_FORMS = (
    "Thanks.\n\nOn Mon, 3 Mar 2025 at 10:00, Jan Kowalski wrote:\n> Middle text.\n>\n"
    "> From: jan.kowalski@acme.pl\n> Sent: Monday, March 3, 2025 9:00 AM\n"
    "> To: Anna Nowak <anna.nowak@client.fr>\n> Subject: Budget\n>\n> Oldest text.\n"
)


def test_participant_seen_as_address_then_name_then_both_is_listed_once():
    data = make_eml(PARTICIPANT_FORMS, sender="Anna Nowak <anna.nowak@client.fr>", to="Jan Kowalski <jan.kowalski@acme.pl>")
    md = convert(data, email_redact=False).markdown
    assert 'participants: ["Jan Kowalski <jan.kowalski@acme.pl>", "Anna Nowak <anna.nowak@client.fr>"]' in md


def test_empty_plain_alternative_falls_back_to_html():
    md = convert(make_eml(" ", html="<p>The real text.</p>"), email_redact=False).markdown
    assert "The real text." in md
    assert "*(no text)*" not in md


def test_names_missing_from_the_headers_are_found_in_greetings_and_signatures():
    body = "Hi Sofia,\n\nThe order ships Friday. Sofia, please confirm.\n\nKind regards,\nDaniel Moore\nSales | Brightline"
    md = convert(make_eml(body, to="sofia@lumen.example", sender="<daniel@brightline.example>")).markdown
    assert "Sofia" not in md
    assert "Daniel" not in md and "Moore" not in md
    assert "Hi [PERSON-" in md and "@" not in md
