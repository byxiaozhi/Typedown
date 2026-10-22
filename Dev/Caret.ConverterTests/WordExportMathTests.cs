using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Typedown.WinUI.Services.Export;
using Xunit;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Caret.ConverterTests
{
    // Phase 2 of the Word export: the formulas ($x^2$ and $$...$$) are Word equations.
    public class WordExportMathTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private WordprocessingDocument Open(string markdown)
        {
            var path = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8) + ".docx");
            WordExporter.ExportToFile(markdown, path);
            var doc = WordprocessingDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} ({e.Path?.XPath})").ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
            return doc;
        }

        // The equation of a one-line document.
        private M.OfficeMath Equation(string markdown)
        {
            using var doc = Open(markdown);
            return (M.OfficeMath)doc.MainDocumentPart.Document.Body.Descendants<M.OfficeMath>().Single().CloneNode(true);
        }

        [Fact]
        public void An_inline_formula_is_an_equation_in_the_paragraph()
        {
            using var doc = Open("Area is $x^2 + y_1$ here.");
            var paragraph = doc.MainDocumentPart.Document.Body.Elements<W.Paragraph>().Single();
            Assert.Single(paragraph.Elements<M.OfficeMath>());
            Assert.Equal("Area is ", paragraph.Elements<W.Run>().First().InnerText);
            var math = paragraph.Elements<M.OfficeMath>().Single();
            Assert.Single(math.Descendants<M.Superscript>());
            Assert.Single(math.Descendants<M.Subscript>());
        }

        [Fact]
        public void Fractions_and_roots()
        {
            var fraction = Equation("$\\frac{a+1}{b}$").Descendants<M.Fraction>().Single();
            Assert.Equal("a+1", fraction.Numerator.InnerText.Replace("\u2212", "-"));
            Assert.Equal("b", fraction.Denominator.InnerText);
            var square = Equation("$\\sqrt{x}$").Descendants<M.Radical>().Single();
            Assert.NotNull(square.RadicalProperties.GetFirstChild<M.HideDegree>());
            var cube = Equation("$\\sqrt[3]{x}$").Descendants<M.Radical>().Single();
            Assert.Equal("3", cube.Degree.InnerText);
            Assert.Null(cube.RadicalProperties.GetFirstChild<M.HideDegree>());
        }

        [Fact]
        public void A_sum_has_its_limits_and_its_body()
        {
            var nary = Equation("$$\\sum_{i=1}^{n} i^2$$").Descendants<M.Nary>().Single();
            Assert.Equal("\u2211", nary.NaryProperties.GetFirstChild<M.AccentChar>().Val.Value);
            Assert.Equal("i=1", nary.GetFirstChild<M.SubArgument>().InnerText);
            Assert.Equal("n", nary.GetFirstChild<M.SuperArgument>().InnerText);
            Assert.Single(nary.GetFirstChild<M.Base>().Descendants<M.Superscript>());
        }

        [Fact]
        public void An_integral_keeps_what_follows_it()
        {
            var math = Equation("$\\int_0^1 x\\,dx$");
            var nary = math.Descendants<M.Nary>().Single();
            Assert.Equal("\u222B", nary.NaryProperties.GetFirstChild<M.AccentChar>().Val.Value);
            Assert.Equal("x", nary.GetFirstChild<M.Base>().InnerText);
            Assert.EndsWith("dx", math.InnerText);
        }

        [Fact]
        public void Brackets_and_matrices()
        {
            var delimiter = Equation("$\\left( \\frac{a}{b} \\right)$").Descendants<M.Delimiter>().Single();
            Assert.Equal("(", delimiter.DelimiterProperties.GetFirstChild<M.BeginChar>().Val.Value);
            Assert.Equal(")", delimiter.DelimiterProperties.GetFirstChild<M.EndChar>().Val.Value);
            Assert.Single(delimiter.Descendants<M.Fraction>());
            var matrix = Equation("$$\\begin{pmatrix} 1 & 2 \\\\ 3 & 4 \\end{pmatrix}$$").Descendants<M.Matrix>().Single();
            var rows = matrix.Elements<M.MatrixRow>().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "1", "2" }, rows[0].Elements<M.Base>().Select(c => c.InnerText).ToArray());
            Assert.Equal(new[] { "3", "4" }, rows[1].Elements<M.Base>().Select(c => c.InnerText).ToArray());
        }

        [Fact]
        public void Greek_letters_symbols_and_text()
        {
            Assert.Equal("\u03B1+\u03B2\u2264\u221E", Equation("$\\alpha + \\beta \\le \\infty$").InnerText);
            var words = Equation("$\\text{if } x \\in \\mathbb{R}$");
            Assert.Contains("if ", words.InnerText);
            Assert.Contains("\u211D", words.InnerText);
            Assert.NotEmpty(words.Descendants<M.NormalText>());
        }

        [Fact]
        public void A_limit_has_its_index_below()
        {
            var math = Equation("$\\lim_{x \\to 0} f(x)$");
            Assert.Single(math.Descendants<M.LimitLower>());
        }

        [Fact]
        public void An_accent_a_function_name_and_an_unknown_command()
        {
            Assert.Single(Equation("$\\hat{x}$").Descendants<M.Accent>());
            Assert.StartsWith("sin", Equation("$\\sin x$").InnerText);
            Assert.Equal("foo", Equation("$\\foo$").InnerText);
        }

        [Fact]
        public void Garbage_never_stops_the_export()
        {
            foreach (var markdown in new[] { "$a^$", "$\\frac{1}$", "$$\\begin{pmatrix} 1 &$$", "$\\left($", "$x_{$", "$$\\sum_$$", "$}$" })
            {
                using var doc = Open(markdown);
                Assert.NotEmpty(doc.MainDocumentPart.Document.Body.Descendants<M.OfficeMath>().ToList());
            }
        }

        [Fact]
        public void A_block_formula_is_an_equation_paragraph_of_its_own()
        {
            using var doc = Open("Before\n\n$$\nx = \\frac{-b}{2a}\n$$\n\nAfter");
            var paragraphs = doc.MainDocumentPart.Document.Body.Elements<W.Paragraph>().ToList();
            Assert.Equal(3, paragraphs.Count);
            Assert.Single(paragraphs[1].Descendants<M.Paragraph>());
            Assert.Single(paragraphs[1].Descendants<M.Fraction>());
        }

        [Fact]
        public void Prices_are_not_formulas()
        {
            using var doc = Open("It costs $5 and then $10 more.");
            Assert.Empty(doc.MainDocumentPart.Document.Body.Descendants<M.OfficeMath>());
            Assert.Equal("It costs $5 and then $10 more.", doc.MainDocumentPart.Document.Body.InnerText);
        }
    }
}
