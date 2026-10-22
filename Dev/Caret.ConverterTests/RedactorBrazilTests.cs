using System;
using System.IO;
using Typedown.WinUI.Services.Conversion;
using Xunit;

namespace Caret.ConverterTests
{
    // Masking Brazilian IDs and phone numbers in the app's own masker (the same cases as the plugin's Python tests:
    // plugins/markitdown-email/tests/test_redact.py). The CPF and CNPJ have two check digits and are found on their own; the
    // other documents (RG, CNH, PIS, título de eleitor) only after their label, so a random number is not taken for an ID.
    public class RedactorBrazilTests : IDisposable
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
        public void CPF_and_CNPJ_with_their_check_digits()
        {
            Assert.Equal("CPF [ID-1]", Redact("CPF 529.982.247-25"));
            Assert.Equal("CNPJ [ID-1]", Redact("CNPJ 11.222.333/0001-81"));
            Assert.Equal("[ID-1] e [ID-2]", Redact("529.982.247-25 e 111.444.777-35"));
            Assert.Equal("[ID-1] e [ID-2]", Redact("00.000.000/0001-91 e 11.444.777/0001-61"));
            Assert.Equal("529.982.247-26", Redact("529.982.247-26")); // wrong check digit and no label
            Assert.Equal("Pedido 11.222.333/0001-82", Redact("Pedido 11.222.333/0001-82"));
            Assert.Equal("111.111.111-11", Redact("111.111.111-11")); // all digits the same is no CPF
            // after its label a number is masked in any form, even with a wrong check digit
            Assert.Equal("CPF [ID-1]", Redact("CPF 52998224725"));
            Assert.Equal("CPF: [ID-1]", Redact("CPF: 529.982.247-26"));
            Assert.Equal("CNPJ [ID-1]", Redact("CNPJ 11222333000181"));
            // the same CPF with and without its points is one person
            Assert.Equal("[ID-1] / CPF [ID-1]", Redact("529.982.247-25 / CPF 52998224725"));
        }

        [Fact]
        public void Documents_after_their_label_only()
        {
            Assert.Equal("RG [ID-1]", Redact("RG 12.345.678-9"));
            Assert.Equal("RG: [ID-1]", Redact("RG: 12.345.678-X"));
            Assert.Equal("CNH [ID-1]", Redact("CNH 12345678900"));
            Assert.Equal("PIS [ID-1]", Redact("PIS 12012345678"));
            Assert.Equal("Título de eleitor [ID-1]", Redact("Título de eleitor 123456789012"));
            Assert.Equal("Carteira de identidade nº [ID-1]", Redact("Carteira de identidade nº 12.345.678"));
            Assert.Equal("Passaporte FB123456", Redact("Passaporte FB123456"));
            Assert.Equal("Pedido 12.345.678 e 12345678900", Redact("Pedido 12.345.678 e 12345678900"));
            Assert.Equal("CEP 01310-100", Redact("CEP 01310-100"));
            Assert.Equal("R$ 1.234,56 em 5 parcelas", Redact("R$ 1.234,56 em 5 parcelas"));
        }

        [Theory]
        [InlineData("Ligue +55 11 91234-5678", "Ligue [PHONE-1]")]
        [InlineData("Celular (11) 91234-5678", "Celular [PHONE-1]")]
        [InlineData("Me chame no 11 91234 5678", "Me chame no [PHONE-1]")]
        [InlineData("Fixo (11) 3123-4567", "Fixo [PHONE-1]")]
        [InlineData("Zap: 11912345678", "Zap: [PHONE-1]")]
        [InlineData("Contato: (21) 2345-6789", "Contato: [PHONE-1]")]
        [InlineData("Fone 0800 123 4567", "Fone [PHONE-1]")]
        public void Brazilian_phone_numbers(string text, string expected) => Assert.Equal(expected, Redact(text));

        [Fact]
        public void Portuguese_greetings_closings_and_titles()
        {
            Assert.Equal("Olá [PERSON-1],", Learned("Olá Marta,"));
            Assert.Equal("Bom dia [PERSON-1],", Learned("Bom dia Carlos,"));
            Assert.Equal("Prezado Sr. [PERSON-1],", Learned("Prezado Sr. Silva,"));
            Assert.Equal("Prezada Dra. [PERSON-1],", Learned("Prezada Dra. Fernanda Lima,"));
            Assert.Equal("Atenciosamente,\n[PERSON-1]", Learned("Atenciosamente,\nJoão Pereira"));
            Assert.Equal("Att.\n[PERSON-1]", Learned("Att.\nAna Souza"));
            Assert.Equal("Abraços,\n[PERSON-1]", Learned("Abraços,\nPedro Lima"));
            Assert.Equal("Bom dia a todos,", Learned("Bom dia a todos,"));
            Assert.Equal("Prezados senhores,", Learned("Prezados senhores,"));
            Assert.Equal("Olá equipe,", Learned("Olá equipe,"));
        }

        [Fact]
        public void A_Brazilian_email_comes_out_without_its_documents_phone_numbers_and_names()
        {
            var eml = string.Join("\r\n",
                "From: Ana Souza <ana.souza@empresa.com.br>",
                "To: Pedro Lima <pedro.lima@cliente.com.br>",
                "Subject: RES: Cadastro do fornecedor",
                "Date: Mon, 5 Oct 2026 10:00:00 -0300",
                "MIME-Version: 1.0",
                "Content-Type: text/plain; charset=utf-8",
                "Content-Transfer-Encoding: 8bit",
                "",
                "Bom dia Pedro,",
                "",
                "Seguem os dados para o cadastro: CNPJ 11.222.333/0001-81, e o responsável, CPF 529.982.247-25 e RG 12.345.678-9. Meu celular é (11) 91234-5678.",
                "",
                "Atenciosamente,",
                "Ana Souza",
                "");
            var path = Path.Combine(work, "cadastro.eml");
            File.WriteAllText(path, eml, new System.Text.UTF8Encoding(false));
            var markdown = DocumentConverter.Convert(path, new ConversionOptions()).Markdown;
            foreach (var secret in new[] { "11.222.333", "529.982.247", "12.345.678", "91234-5678", "Ana", "Souza", "Pedro", "Lima", "empresa.com.br" })
                Assert.DoesNotContain(secret, markdown);
            Assert.Contains("CNPJ [ID-", markdown);
            Assert.Contains("CPF [ID-", markdown);
            Assert.Contains("[PHONE-1]", markdown);
        }
    }
}