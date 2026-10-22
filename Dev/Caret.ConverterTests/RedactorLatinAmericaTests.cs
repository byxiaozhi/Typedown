using System;
using System.IO;
using Typedown.WinUI.Services.Conversion;
using Xunit;

namespace Caret.ConverterTests
{
    // Masking Latin American IDs and phone numbers in the app's own masker (the same cases as the plugin's Python tests:
    // plugins/markitdown-email/tests/test_redact.py). Numbers with a check digit are found on their own, the others only after
    // their label, so a random number is not taken for an ID.
    public class RedactorLatinAmericaTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private static string Redact(string text) => new Redactor().Redact(text);

        private static string Learned(string text)
        {
            var redactor = new Redactor();
            redactor.LearnNames(text, EmailRules.Builtin);
            return redactor.Redact(text);
        }

        [Fact]
        public void Chilean_RUT_with_its_check_digit()
        {
            Assert.Equal("RUT [ID-1]", Redact("RUT 12.345.678-5"));
            Assert.Equal("Rut: [ID-1] y [ID-2]", Redact("Rut: 76086428-5 y 14.000.006-K"));
            Assert.Equal("[ID-1] y [ID-1]", Redact("12.345.678-5 y 12345678-5")); // the same number written two ways
            Assert.Equal("[ID-1]", Redact("14.000.006-k"));
            Assert.Equal("Ref 12.345.678-0", Redact("Ref 12.345.678-0")); // wrong check digit and no label
        }

        [Fact]
        public void Argentine_CUIT_and_CUIL()
        {
            Assert.Equal("CUIT [ID-1]", Redact("CUIT 30-50001091-2"));
            Assert.Equal("CUIL [ID-1] / [ID-2] / [ID-3]", Redact("CUIL 20-12345678-6 / 23-12345600-9 / 23-12345600-4"));
            Assert.Equal("Pedido 20-12345678-7", Redact("Pedido 20-12345678-7")); // wrong check digit
            Assert.Equal("Pedido 11-12345678-6", Redact("Pedido 11-12345678-6")); // not a prefix of a person or a company
        }

        [Fact]
        public void Colombian_NIT()
        {
            Assert.Equal("NIT [ID-1] y [ID-2]", Redact("NIT 800.197.268-4 y 890.903.938-8"));
            Assert.Equal("Factura 800.197.268-5", Redact("Factura 800.197.268-5"));
        }

        [Fact]
        public void Mexican_CURP_and_RFC()
        {
            Assert.Equal("CURP [ID-1]", Redact("CURP HEGG560427MVZRRL04"));
            Assert.Equal("CURP HEGG560427MVZRRL05", Redact("CURP HEGG560427MVZRRL05")); // wrong check digit
            Assert.Equal("RFC [ID-1] y [ID-2]", Redact("RFC GODE561231GR8 y SAT970701NN3"));
            Assert.Equal("RFC GODE561231GR9", Redact("RFC GODE561231GR9"));
            Assert.Equal("ESTAMOS EN ENERO2024", Redact("ESTAMOS EN ENERO2024"));
        }

        [Fact]
        public void Honduran_identity_number_and_RTN()
        {
            Assert.Equal("Identidad [ID-1]", Redact("Identidad 0801-1990-12345"));
            Assert.Equal("RTN [ID-1]", Redact("RTN 0801-1990-123456"));
            Assert.Equal("Vigente 2023-2024-12345", Redact("Vigente 2023-2024-12345"));
        }

        [Fact]
        public void Numbers_without_a_check_digit_are_masked_only_after_their_label()
        {
            Assert.Equal("DNI [ID-1]", Redact("DNI 12.345.678"));
            Assert.Equal("D.N.I. Nº [ID-1]", Redact("D.N.I. Nº 30123456"));
            Assert.Equal("Cédula de ciudadanía No. [ID-1]", Redact("Cédula de ciudadanía No. 1.234.567.890"));
            Assert.Equal("C.C. [ID-1] y CC: [ID-2]", Redact("C.C. 1234567890 y CC: 52.123.456"));
            Assert.Equal("Documento de identidad [ID-1]", Redact("Documento de identidad 87654321"));
            Assert.Equal("RUC [ID-1]", Redact("RUC 20131312955"));
            Assert.Equal("Pasaporte Nº [ID-1]", Redact("Pasaporte Nº 123456789"));
            // the same number, unlabelled, is not an ID
            Assert.Equal("Pedido 12.345.678 y 1234567890", Redact("Pedido 12.345.678 y 1234567890"));
            // a Spanish DNI with a wrong letter is left alone, label or not
            Assert.Equal("DNI 12345678A", Redact("DNI 12345678A"));
        }

        [Fact]
        public void Labels_that_only_look_like_ids()
        {
            Assert.Equal("Cc: 1234567", Redact("Cc: 1234567")); // carbon copy, and lower case
            Assert.Equal("CC [CARD-1].", Redact("CC 4111 1111 1111 1111.")); // CC for a credit card
            Assert.Equal("Reglamento CE 2016/679", Redact("Reglamento CE 2016/679"));
            Assert.Equal("Documento adjunto 2025", Redact("Documento adjunto 2025"));
        }

        [Theory]
        [InlineData("Llama al +57 300 123 4567", "Llama al [PHONE-1]")]
        [InlineData("WhatsApp +54 9 11 1234-5678", "WhatsApp [PHONE-1]")]
        [InlineData("Cel: 3001234567", "Cel: [PHONE-1]")]
        [InlineData("Celular 300 123 4567", "Celular [PHONE-1]")]
        [InlineData("Tel (011) 4123-4567", "Tel [PHONE-1]")]
        [InlineData("Fono +56 9 8765 4321", "Fono [PHONE-1]")]
        [InlineData("Llámame al 9 1234 5678", "Llámame al [PHONE-1]")]
        [InlineData("Oficina 55 1234 5678", "Oficina [PHONE-1]")]
        [InlineData("Honduras +504 9876-5432", "Honduras [PHONE-1]")]
        [InlineData("Perú +51 987 654 321", "Perú [PHONE-1]")]
        public void Latin_American_phone_numbers(string text, string expected) => Assert.Equal(expected, Redact(text));

        [Theory]
        [InlineData("Vigencia 2023-2024")]
        [InlineData("Presupuesto de 1.500.000 COP")]
        [InlineData("Año 2024, trimestre 3")]
        [InlineData("Factura 123-4567")]
        public void Things_that_are_not_phone_numbers_in_Spanish_texts(string text) => Assert.Equal(text, Redact(text));

        [Fact]
        public void Latin_American_greetings_closings_and_titles()
        {
            Assert.Equal("Buen día [PERSON-1],", Learned("Buen día Marta,"));
            Assert.Equal("Apreciada [PERSON-1]:", Learned("Apreciada Laura:"));
            Assert.Equal("Estimado Ing. [PERSON-1]:", Learned("Estimado Ing. Pérez:"));
            Assert.Equal("Buenas tardes Lic. [PERSON-1],", Learned("Buenas tardes Lic. Ríos,"));
            Assert.Equal("Cordial saludo,\n[PERSON-1]", Learned("Cordial saludo,\nJuan Pérez"));
            Assert.Equal("Atte.\n[PERSON-1]", Learned("Atte.\nSofía Ramírez"));
            Assert.Equal("Bendiciones,\n[PERSON-1]", Learned("Bendiciones,\nDaniel Mora"));
            Assert.Equal("Buen día a todos,", Learned("Buen día a todos,"));
        }

        [Fact]
        public void A_Spanish_email_comes_out_without_its_ids_phone_numbers_and_names()
        {
            var eml = string.Join("\r\n",
                "From: Laura Gómez <laura.gomez@empresa.com.co>",
                "To: Juan Pérez <juan.perez@cliente.cl>",
                "Subject: Factura y datos del proveedor",
                "Date: Thu, 8 Oct 2026 09:30:00 -0500",
                "MIME-Version: 1.0",
                "Content-Type: text/plain; charset=utf-8",
                "Content-Transfer-Encoding: 8bit",
                "",
                "Buen día Juan,",
                "",
                "Te confirmo los datos para la factura: NIT 800.197.268-4, el representante legal con cédula de ciudadanía No. 1.234.567.890 y su RUT 12.345.678-5 en Chile. Mi celular es +57 300 123 4567.",
                "",
                "Cordial saludo,",
                "Laura Gómez",
                "");
            var path = Path.Combine(work, "factura.eml");
            File.WriteAllText(path, eml, new System.Text.UTF8Encoding(false));
            var markdown = DocumentConverter.Convert(path, new ConversionOptions()).Markdown;
            foreach (var secret in new[] { "800.197.268", "1.234.567.890", "12.345.678", "300 123 4567", "Laura", "Gómez", "Juan", "Pérez", "empresa.com.co" })
                Assert.DoesNotContain(secret, markdown);
            Assert.Contains("NIT [ID-1]", markdown);
            Assert.Contains("[PHONE-1]", markdown);
        }
    }
}