using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // Edit > Copy as WhatsApp text: Markdown in, a message WhatsApp draws as it should out (*bold*, _italic_, ~strike~, `code`,
    // "- " lists, "> " quotes; no headings, links, images or tables, which become text).
    public class WhatsAppFormatTests
    {
        private static string W(string markdown) => WhatsAppFormat.Convert(markdown);

        [Theory]
        [InlineData("**bold** and *italic* and ~~gone~~", "*bold* and _italic_ and ~gone~")]
        [InlineData("__bold__ and _italic_", "*bold* and _italic_")]
        [InlineData("***both***", "*_both_*")]
        [InlineData("**bold with *italic* inside**", "*bold with _italic_ inside*")]
        [InlineData("snake_case_word stays and 2 * 3 * 4 too", "snake_case_word stays and 2 * 3 * 4 too")]
        [InlineData("`code_with_underscores` and *x*", "`code_with_underscores` and _x_")]
        [InlineData("==highlight==", "highlight")]
        [InlineData("\\*not bold\\*", "*not bold*")]
        [InlineData("Sea $x_i$ el valor", "Sea x_i el valor")]
        [InlineData("**Atención:** revisa el _plan_.", "*Atención:* revisa el _plan_.")]
        public void Inline_marks_become_the_WhatsApp_markers(string markdown, string expected) => Assert.Equal(expected, W(markdown));

        [Theory]
        [InlineData("[Caret](https://example.com/a_b)", "Caret (https://example.com/a_b)")]
        [InlineData("[https://x.com](https://x.com)", "https://x.com")]
        [InlineData("[inicio](#top) y [otra](notes/other.md)", "inicio y otra")]
        [InlineData("[correo](mailto:ana@x.com)", "correo (ana@x.com)")]
        [InlineData("<https://x.com/p>", "https://x.com/p")]
        [InlineData("Mira https://x.com/a_b_c ahora", "Mira https://x.com/a_b_c ahora")]
        [InlineData("![logo](logo.png)", "logo")]
        [InlineData("![logo](https://x.com/l.png)", "logo (https://x.com/l.png)")]
        [InlineData("![](https://x.com/l.png)", "https://x.com/l.png")]
        [InlineData("**[enlace](https://x.com)**", "*enlace (https://x.com)*")]
        public void Links_and_images_become_text(string markdown, string expected) => Assert.Equal(expected, W(markdown));

        [Fact]
        public void Headings_are_bold_lines_set_off_by_a_blank_line()
        {
            Assert.Equal("*Title*\n\nText", W("# Title\n\nText"));
            Assert.Equal("*Bold heading*", W("## **Bold** heading"));
            Assert.Equal("*Title*\nText", W("Title\n=====\nText"));
            Assert.Equal("*Sub*\nText", W("Sub\n---\nText"));
            Assert.Equal("Intro\n\n*Title*", W("Intro\n# Title"));
            Assert.Equal("#hashtag stays", W("#hashtag stays"));
        }

        [Fact]
        public void Lists_keep_their_levels_and_markers()
        {
            Assert.Equal("- a\n- b\n  - c\n    - d\n- e", W("- a\n- b\n  - c\n    - d\n- e"));
            Assert.Equal("- a\n  - b", W("- a\n    - b")); // four spaces or two: one level
            Assert.Equal("- a\n- b", W("* a\n+ b"));
            Assert.Equal("1. uno\n2. dos", W("1. uno\n2. dos"));
            Assert.Equal("1. uno\n  - sub\n2. dos", W("1. uno\n   - sub\n2. dos"));
        }

        [Fact]
        public void Tasks_quotes_and_rules()
        {
            Assert.Equal("⬜ por hacer\n✅ hecho", W("- [ ] por hacer\n- [x] hecho"));
            Assert.Equal("> uno\n> dos\n> anidada", W("> uno\n> dos\n>> anidada"));
            Assert.Equal("a\n\n———\n\nb", W("a\n\n---\n\nb"));
            Assert.Equal("a\n\n———\n\nb", W("a\n\n***\n\nb"));
        }

        [Fact]
        public void Code_blocks_are_kept_as_they_are()
        {
            Assert.Equal("```\nconst a = 1;\n```", W("```js\nconst a = 1;\n```"));
            Assert.Equal("```\n**not bold** _nor this_\n```", W("```\n**not bold** _nor this_\n```"));
            Assert.Equal("Text\n\n```\ncode\n```", W("Text\n```\ncode\n```"));
            Assert.Equal("```\nx = 1\n```", W("~~~\nx = 1\n~~~"));
            Assert.Equal("```\nsin código de cierre\n```", W("```\nsin código de cierre")); // an open block runs to the end
            Assert.Equal("```\nE = mc^2\n```", W("$$\nE = mc^2\n$$"));
        }

        [Fact]
        public void A_table_becomes_a_block_with_the_columns_lined_up()
        {
            Assert.Equal("```\nNombre | Edad\n-------+-----\nAna    | 31\nLuis   | 4\n```",
                W("| Nombre | Edad |\n|---|---|\n| Ana | 31 |\n| Luis | 4 |"));
            // markers inside the cells have no meaning in a block of code
            Assert.Equal("```\nA | b\n--+--\nc | d\n```", W("| **A** | b |\n|:--|--:|\n| `c` | _d_ |"));
            // a line with a bar above a rule is not a table
            Assert.Equal("*a | b*", W("a | b\n---"));
        }

        [Fact]
        public void Review_marks_are_taken_as_accepted_and_comments_dropped()
        {
            Assert.Equal("Un buen día", W("Un {++buen ++}día{>>@Ana 2026-10-07: ok<<}"));
            Assert.Equal("Hola mundo", W("Hola {~~planeta~>mundo~~}"));
            Assert.Equal("Texto", W("Texto {--borrado--}{>>una nota<<}"));
        }

        [Fact]
        public void Front_matter_html_footnotes_and_blank_lines()
        {
            Assert.Equal("*H*", W("---\ntitle: x\n---\n# H"));
            Assert.Equal("Línea\ndos negrita", W("Línea<br>dos <b>negrita</b>"));
            Assert.Equal("a", W("a<!-- oculto -->"));
            Assert.Equal("Texto[1]\n\n[1] Nota", W("Texto[^1]\n\n[^1]: Nota"));
            Assert.Equal("a\n\nb", W("a\n\n\n\n\nb"));
            Assert.Equal("a\nb", W("a\r\nb\r\n"));
            Assert.Equal("", W(""));
            Assert.Equal("", W("   \n  "));
            Assert.Equal("", W(null));
        }

        [Fact]
        public void A_note_that_opens_with_a_rule_keeps_what_lies_between_the_rules()
        {
            // not front matter: no "key:" line, so nothing is dropped
            Assert.Equal("———\nPrimera parte\n\n———\nSegunda", W("---\nPrimera parte\n\n---\nSegunda"));
            Assert.Equal("———\ntexto sin cierre\n\nmás texto", W("---\ntexto sin cierre\n\nmás texto"));
            // and a selection that starts with a rule and holds a setext underline
            Assert.Equal("———\n\n*Título*\nCuerpo", W("---\nTítulo\n---\nCuerpo"));
            // real front matter still goes, however it is written
            Assert.Equal("Texto", W("---\ntitle: Acta\ntags: [a, b]\n---\nTexto"));
            Assert.Equal("Texto", W("---\nauthor: Ana\n...\nTexto"));
        }
        [Fact]
        public void A_whole_note_in_Spanish()
        {
            var note = string.Join("\n",
                "---",
                "title: Acta",
                "---",
                "# Acta de reunión",
                "",
                "**Fecha:** 8 de octubre · **Lugar:** Bogotá",
                "",
                "## Acuerdos",
                "",
                "- Enviar la [propuesta](https://ejemplo.co/propuesta_v2) a *Laura*",
                "- [x] Confirmar el NIT",
                "- [ ] Pagar la ~~factura~~ cuenta de cobro",
                "",
                "> Recordar: el cierre es el viernes.",
                "",
                "| Tema | Responsable |",
                "|---|---|",
                "| Contrato | Ana |",
                "",
                "---",
                "Gracias.");
            var expected = string.Join("\n",
                "*Acta de reunión*",
                "",
                "*Fecha:* 8 de octubre · *Lugar:* Bogotá",
                "",
                "*Acuerdos*",
                "",
                "- Enviar la propuesta (https://ejemplo.co/propuesta_v2) a _Laura_",
                "✅ Confirmar el NIT",
                "⬜ Pagar la ~factura~ cuenta de cobro",
                "",
                "> Recordar: el cierre es el viernes.",
                "",
                "```",
                "Tema     | Responsable",
                "---------+------------",
                "Contrato | Ana",
                "```",
                "",
                "———",
                "Gracias.");
            Assert.Equal(expected, W(note));
        }

        [Fact]
        public void The_message_limit_is_WhatsApps()
        {
            Assert.Equal(65536, WhatsAppFormat.MessageLimit);
        }
    }
}