from markitdown_caret_email import Redactor
from markitdown_caret_email._model import Address


def redact(text, **kwargs):
    return Redactor(**kwargs).redact(text)


def test_emails_get_stable_numbers():
    text = "Write to anna.nowak@client.fr or jan@acme.pl, then ANNA.NOWAK@client.fr again."
    assert redact(text) == "Write to [EMAIL-1] or [EMAIL-2], then [EMAIL-1] again."


def test_iban_only_with_valid_check_digits():
    assert redact("IBAN: PL61 1090 1014 0000 0712 1981 2874") == "IBAN: [IBAN-1]"
    assert redact("Ref GB82WEST12345698765433") == "Ref GB82WEST12345698765433"


def test_card_numbers_with_luhn():
    assert redact("Card 4111 1111 1111 1111.") == "Card [CARD-1]."
    assert redact("Order 4111 1111 1111 1112.") == "Order 4111 1111 1111 1112."


def test_national_ids():
    assert redact("PESEL 44051401359") == "PESEL [ID-1]"
    assert redact("Numer 44051401358") == "Numer 44051401358"  # wrong check digit
    assert redact("DNI 12345678Z, NIE X1234567L") == "DNI [ID-1], NIE [ID-2]"
    assert redact("DNI 12345678A") == "DNI 12345678A"
    assert redact("NIR 1 84 03 76 451 089 96") == "NIR [ID-1]"
    assert redact("NINO AB 12 34 56 C") == "NINO [ID-1]"
    assert redact("NINO QQ 12 34 56 C") == "NINO QQ 12 34 56 C"  # Q is never issued


def test_phone_numbers():
    assert redact("Call +48 601 234 567 or +33 (0)1 23 45 67 89") == "Call [PHONE-1] or [PHONE-2]"
    assert redact("Tél : 01 23 45 67 89") == "Tél : [PHONE-1]"
    assert redact("Móvil: 612 345 678") == "Móvil: [PHONE-1]"
    assert redact("Fijo 912 34 56 78") == "Fijo [PHONE-1]"
    assert redact("tel. kom. 601-234-567") == "tel. kom. [PHONE-1]"
    assert redact("UK office 020 7946 0958") == "UK office [PHONE-1]"


def test_things_that_are_not_phone_numbers():
    for text in (
        "Budget: 100 000 000 EUR",
        "Invoice 2025-03-04",
        "Meeting at 10:30 on 03.04.2025",
        "Version 1.2.3",
        "Order number 4411",
    ):
        assert redact(text) == text, text


def test_people_from_headers_in_all_their_forms():
    r = Redactor(people=[Address("Anna Nowak", "anna@client.fr"), Address("Kowalski, Jan", "jan@acme.pl")])
    text = "Anna Nowak, Nowak, Anna, NOWAK Anna and Anna agreed with Jan Kowalski (Jan) and Kowalski."
    assert r.redact(text) == (
        "[PERSON-1], [PERSON-1], [PERSON-1] and [PERSON-1] agreed with [PERSON-2] ([PERSON-2]) and [PERSON-2]."
    )


def test_single_names_only_when_capitalised():
    r = Redactor(people=[Address("Will Smith", "will@x.com")])
    assert r.redact("Will said it will work.") == "[PERSON-1] said it will work."


def test_name_wrapped_over_two_lines():
    r = Redactor(people=[Address("Anna Nowak", "")])
    assert r.redact("Signed by Anna\nNowak") == "Signed by [PERSON-1]"


def test_extra_names_from_the_user():
    r = Redactor(names=["ACME Polska", "Marta Wiśniewska"])
    assert r.redact("Marta Wiśniewska from ACME Polska called; Marta will write.") == (
        "[PERSON-1] from [PERSON-2] called; [PERSON-1] will write."
    )


def test_person_known_by_name_and_by_email_is_one_placeholder():
    r = Redactor(people=[Address("Anna Nowak", "anna@client.fr")], names=["Anna Nowak"])
    assert r.redact("Anna Nowak / Nowak") == "[PERSON-1] / [PERSON-1]"


def test_role_senders_are_masked_as_a_whole():
    r = Redactor(people=[Address("Microsoft Azure", "azure-noreply@microsoft.com")])
    assert r.redact("Microsoft Azure: deploy to Azure today.") == "[PERSON-1]: deploy to Azure today."


def test_placeholders_are_not_redacted_again():
    once = redact("anna@client.fr +48 601 234 567")
    assert redact(once) == once


def test_hyphenated_first_names_and_capitalised_surnames():
    # Directories write "Anna Maria NOWAK"; signatures and greetings write "Anna-Maria Nowak"
    r = Redactor(people=[Address("Anna Maria NOWAK", "anna@x.fr")])
    assert r.redact("Hi Anna-Maria, regards **Anna-Maria Nowak** / Nowak / NOWAK") == (
        "Hi [PERSON-1], regards **[PERSON-1]** / [PERSON-1] / [PERSON-1]"
    )


# --- names in a greeting or under a sign-off ---------------------------------------------------

from markitdown_caret_email import load_rules  # noqa: E402


def learned(text, **kwargs):
    r = Redactor(**kwargs)
    r.learn_names(text, load_rules())
    return r.redact(text)


def test_a_greeted_name_is_masked_everywhere():
    assert learned("Hi Daniel,\n\nPlease ask Daniel about it.") == "Hi [PERSON-1],\n\nPlease ask [PERSON-1] about it."


def test_several_greeted_names_and_titles():
    assert learned("Hello Daniel and Emma,\nDear Mr. Smith,") == "Hello [PERSON-1] and [PERSON-2],\nDear Mr. [PERSON-3],"
    assert learned("Bonjour Julien et Claire,") == "Bonjour [PERSON-1] et [PERSON-2],"
    assert learned("Cher Monsieur Dupont,") == "Cher Monsieur [PERSON-1],"
    assert learned("Buenos días Marta:") == "Buenos días [PERSON-1]:"
    assert learned("Hola Lucía,") == "Hola [PERSON-1],"
    assert learned("Cześć Marto,") == "Cześć [PERSON-1],"
    assert learned("Szanowny Panie Kowalski,") == "Szanowny Panie [PERSON-1],"


def test_a_full_greeted_name_masks_its_parts_too():
    assert learned("Hi John Smith,\nregards, Smith") == "Hi [PERSON-1],\nregards, [PERSON-1]"


def test_greetings_that_are_not_a_person():
    for text in ("Hi all,", "Hello team,", "Dear Sir or Madam,", "Hi everyone,", "Bonjour à tous,", "Hola a todos,",
                 "Dear Customer,", "Szanowni Państwo,", "Hi there,", "Bonjour Madame, Monsieur,"):
        assert learned(text) == text, text


def test_a_greeting_must_be_a_name_and_nothing_else():
    assert learned("Hi Daniel how are you") == "Hi Daniel how are you"
    assert learned("Hi daniel,") == "Hi daniel,"  # no capital: not taken for a name
    assert learned("Then Hello Daniel, later") == "Then Hello Daniel, later"  # not at the start of a line


def test_a_name_written_under_a_closing_is_masked():
    assert learned("See you Monday.\n\nKind regards,\nAnna Nowak\n\nAnna will call.") == (
        "See you Monday.\n\nKind regards,\n[PERSON-1]\n\n[PERSON-1] will call."
    )
    assert learned("Thanks, Marta\nMarta") == "Thanks, [PERSON-1]\n[PERSON-1]"
    assert learned("Pozdrawiam\nJan Kowalski") == "Pozdrawiam\n[PERSON-1]"


def test_emphasis_around_a_greeted_name():
    assert learned("Hi **Sofia**,\nSofia") == "Hi **[PERSON-1]**,\n[PERSON-1]"
    assert learned("Hello **Daniel** and _Emma_,") == "Hello **[PERSON-1]** and _[PERSON-2]_,"
    assert learned("Dear **Mr. Smith**,") == "Dear **Mr. [PERSON-1]**,"
    assert learned("**Hi Sofia,**") == "**Hi [PERSON-1],**"


def test_punctuation_after_an_inline_sign_off_name():
    assert learned("Thanks, Anna!\nAnna") == "Thanks, [PERSON-1]!\n[PERSON-1]"
    assert learned("Cheers, **Marta**.") == "Cheers, **[PERSON-1]**."
    assert learned("Thanks, Anna K.") == "Thanks, [PERSON-1]."


def test_names_in_other_alphabets():
    assert learned("Hi Ольга,\nОльга") == "Hi [PERSON-1],\n[PERSON-1]"
    assert learned("Hello Νίκος and Łukasz,") == "Hello [PERSON-1] and [PERSON-2],"
    assert learned("Kind regards,\nОльга Иванова\nИванова") == "Kind regards,\n[PERSON-1]\n[PERSON-1]"


def test_a_name_inside_an_identifier_is_left_alone_but_emphasis_is_masked():
    assert learned("Hi Anna,\nuser_Anna_id and _Anna_ and Anna_x") == "Hi [PERSON-1],\nuser_Anna_id and _[PERSON-1]_ and Anna_x"


def test_a_team_signature_is_not_a_person():
    assert learned("Best regards,\nSupport Team") == "Best regards,\nSupport Team"


def test_a_learned_name_is_the_same_person_as_the_header_name():
    r = Redactor(people=[Address("Daniel Moore", "daniel@x.example")])
    text = "Hi Daniel,\nDaniel Moore"
    r.learn_names(text, load_rules())
    assert r.redact(text) == "Hi [PERSON-1],\n[PERSON-1]"


def test_a_name_only_in_a_sentence_is_still_not_found():
    text = "Please ask Marta from finance."
    assert learned(text) == text


# --- Latin America ---------------------------------------------------------------------------


def test_chilean_rut_with_its_check_digit():
    assert redact("RUT 12.345.678-5") == "RUT [ID-1]"
    assert redact("Rut: 76086428-5 y 14.000.006-K") == "Rut: [ID-1] y [ID-2]"
    assert redact("12.345.678-5 y 12345678-5") == "[ID-1] y [ID-1]"  # the same number written two ways
    assert redact("14.000.006-k") == "[ID-1]"
    assert redact("Ref 12.345.678-0") == "Ref 12.345.678-0"  # wrong check digit and no label


def test_argentine_cuit_and_cuil():
    assert redact("CUIT 30-50001091-2") == "CUIT [ID-1]"
    assert redact("CUIL 20-12345678-6 / 23-12345600-9 / 23-12345600-4") == "CUIL [ID-1] / [ID-2] / [ID-3]"
    assert redact("Pedido 20-12345678-7") == "Pedido 20-12345678-7"  # wrong check digit
    assert redact("Pedido 11-12345678-6") == "Pedido 11-12345678-6"  # not a prefix of a person or a company


def test_colombian_nit():
    assert redact("NIT 800.197.268-4 y 890.903.938-8") == "NIT [ID-1] y [ID-2]"
    assert redact("Factura 800.197.268-5") == "Factura 800.197.268-5"


def test_mexican_curp_and_rfc():
    assert redact("CURP HEGG560427MVZRRL04") == "CURP [ID-1]"
    assert redact("CURP HEGG560427MVZRRL05") == "CURP HEGG560427MVZRRL05"  # wrong check digit
    assert redact("RFC GODE561231GR8 y SAT970701NN3") == "RFC [ID-1] y [ID-2]"
    assert redact("RFC GODE561231GR9") == "RFC GODE561231GR9"
    assert redact("ESTAMOS EN ENERO2024") == "ESTAMOS EN ENERO2024"


def test_honduran_identity_number_and_rtn():
    assert redact("Identidad 0801-1990-12345") == "Identidad [ID-1]"
    assert redact("RTN 0801-1990-123456") == "RTN [ID-1]"
    assert redact("Vigente 2023-2024-12345") == "Vigente 2023-2024-12345"


def test_numbers_without_a_check_digit_are_masked_only_after_their_label():
    assert redact("DNI 12.345.678") == "DNI [ID-1]"
    assert redact("D.N.I. Nº 30123456") == "D.N.I. Nº [ID-1]"
    assert redact("Cédula de ciudadanía No. 1.234.567.890") == "Cédula de ciudadanía No. [ID-1]"
    assert redact("C.C. 1234567890 y CC: 52.123.456") == "C.C. [ID-1] y CC: [ID-2]"
    assert redact("Documento de identidad 87654321") == "Documento de identidad [ID-1]"
    assert redact("RUC 20131312955") == "RUC [ID-1]"
    assert redact("Pasaporte Nº 123456789") == "Pasaporte Nº [ID-1]"
    # the same number, unlabelled, is not an ID
    assert redact("Pedido 12.345.678 y 1234567890") == "Pedido 12.345.678 y 1234567890"


def test_labels_that_only_look_like_ids():
    assert redact("Cc: 1234567") == "Cc: 1234567"  # carbon copy, and lower case
    assert redact("CC 4111 1111 1111 1111.") == "CC [CARD-1]."  # CC for a credit card
    assert redact("Reglamento CE 2016/679") == "Reglamento CE 2016/679"
    assert redact("Documento adjunto 2025") == "Documento adjunto 2025"


def test_latin_american_phone_numbers():
    assert redact("Llama al +57 300 123 4567") == "Llama al [PHONE-1]"
    assert redact("WhatsApp +54 9 11 1234-5678") == "WhatsApp [PHONE-1]"
    assert redact("Cel: 3001234567") == "Cel: [PHONE-1]"
    assert redact("Celular 300 123 4567") == "Celular [PHONE-1]"
    assert redact("Tel (011) 4123-4567") == "Tel [PHONE-1]"
    assert redact("Fono +56 9 8765 4321") == "Fono [PHONE-1]"
    assert redact("Llámame al 9 1234 5678") == "Llámame al [PHONE-1]"
    assert redact("Oficina 55 1234 5678") == "Oficina [PHONE-1]"
    assert redact("Honduras +504 9876-5432") == "Honduras [PHONE-1]"
    assert redact("Perú +51 987 654 321") == "Perú [PHONE-1]"


def test_things_that_are_not_phone_numbers_in_spanish_texts():
    for text in ("Vigencia 2023-2024", "Presupuesto de 1.500.000 COP", "Año 2024, trimestre 3", "Factura 123-4567"):
        assert redact(text) == text, text


def test_latin_american_greetings_closings_and_titles():
    assert learned("Buen día Marta,") == "Buen día [PERSON-1],"
    assert learned("Apreciada Laura:") == "Apreciada [PERSON-1]:"
    assert learned("Estimado Ing. Pérez:") == "Estimado Ing. [PERSON-1]:"
    assert learned("Buenas tardes Lic. Ríos,") == "Buenas tardes Lic. [PERSON-1],"
    assert learned("Cordial saludo,\nJuan Pérez") == "Cordial saludo,\n[PERSON-1]"
    assert learned("Atte.\nSofía Ramírez") == "Atte.\n[PERSON-1]"
    assert learned("Bendiciones,\nDaniel Mora") == "Bendiciones,\n[PERSON-1]"
    assert learned("Buen día a todos,") == "Buen día a todos,"


# --- Brazil ----------------------------------------------------------------------------------


def test_brazilian_cpf_and_cnpj_with_their_check_digits():
    assert redact("CPF 529.982.247-25") == "CPF [ID-1]"
    assert redact("CNPJ 11.222.333/0001-81") == "CNPJ [ID-1]"
    assert redact("529.982.247-25 e 111.444.777-35") == "[ID-1] e [ID-2]"
    assert redact("00.000.000/0001-91 e 11.444.777/0001-61") == "[ID-1] e [ID-2]"
    assert redact("529.982.247-26") == "529.982.247-26"  # wrong check digit and no label
    assert redact("Pedido 11.222.333/0001-82") == "Pedido 11.222.333/0001-82"
    assert redact("111.111.111-11") == "111.111.111-11"  # all digits the same is no CPF
    # after its label a number is masked in any form, even with a wrong check digit
    assert redact("CPF 52998224725") == "CPF [ID-1]"
    assert redact("CPF: 529.982.247-26") == "CPF: [ID-1]"
    assert redact("CNPJ 11222333000181") == "CNPJ [ID-1]"
    # the same CPF with and without its points is one person
    assert redact("529.982.247-25 / CPF 52998224725") == "[ID-1] / CPF [ID-1]"


def test_brazilian_documents_after_their_label():
    assert redact("RG 12.345.678-9") == "RG [ID-1]"
    assert redact("RG: 12.345.678-X") == "RG: [ID-1]"
    assert redact("CNH 12345678900") == "CNH [ID-1]"
    assert redact("PIS 12012345678") == "PIS [ID-1]"
    assert redact("Título de eleitor 123456789012") == "Título de eleitor [ID-1]"
    assert redact("Carteira de identidade nº 12.345.678") == "Carteira de identidade nº [ID-1]"
    assert redact("Passaporte FB123456") == "Passaporte FB123456"  # letters first: no number to take
    # the same numbers with no label are not IDs
    assert redact("Pedido 12.345.678 e 12345678900") == "Pedido 12.345.678 e 12345678900"
    assert redact("CEP 01310-100") == "CEP 01310-100"
    assert redact("R$ 1.234,56 em 5 parcelas") == "R$ 1.234,56 em 5 parcelas"


def test_brazilian_phone_numbers():
    assert redact("Ligue +55 11 91234-5678") == "Ligue [PHONE-1]"
    assert redact("Celular (11) 91234-5678") == "Celular [PHONE-1]"
    assert redact("Me chame no 11 91234 5678") == "Me chame no [PHONE-1]"
    assert redact("Fixo (11) 3123-4567") == "Fixo [PHONE-1]"
    assert redact("Zap: 11912345678") == "Zap: [PHONE-1]"
    assert redact("Contato: (21) 2345-6789") == "Contato: [PHONE-1]"
    assert redact("Fone 0800 123 4567") == "Fone [PHONE-1]"


def test_portuguese_greetings_closings_and_titles():
    assert learned("Olá Marta,") == "Olá [PERSON-1],"
    assert learned("Bom dia Carlos,") == "Bom dia [PERSON-1],"
    assert learned("Prezado Sr. Silva,") == "Prezado Sr. [PERSON-1],"
    assert learned("Prezada Dra. Fernanda Lima,") == "Prezada Dra. [PERSON-1],"
    assert learned("Atenciosamente,\nJoão Pereira") == "Atenciosamente,\n[PERSON-1]"
    assert learned("Att.\nAna Souza") == "Att.\n[PERSON-1]"
    assert learned("Abraços,\nPedro Lima") == "Abraços,\n[PERSON-1]"
    assert learned("Bom dia a todos,") == "Bom dia a todos,"
    assert learned("Prezados senhores,") == "Prezados senhores,"
    assert learned("Olá equipe,") == "Olá equipe,"
