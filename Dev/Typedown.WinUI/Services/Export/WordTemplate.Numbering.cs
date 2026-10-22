using System.Linq;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    internal sealed partial class WordTemplate
    {
        // Our lists next to those of the template: the numbers of ours are moved past the ones the template uses, in the text and in the
        // definitions, and the definitions are added to the numbering part of the template. false: the template has no lists of its own.
        public bool MergeNumbering(W.Numbering ours, params OpenXmlElement[] roots)
        {
            var theirs = Package.MainDocumentPart.NumberingDefinitionsPart?.Numbering;
            if (theirs == null) return false;
            var abstractBase = theirs.Elements<W.AbstractNum>().Select(a => a.AbstractNumberId?.Value ?? 0).DefaultIfEmpty(-1).Max() + 1;
            var numberBase = theirs.Elements<W.NumberingInstance>().Select(i => i.NumberID?.Value ?? 0).DefaultIfEmpty(0).Max();
            foreach (var root in roots.Where(r => r != null))
                foreach (var id in root.Descendants<W.NumberingId>()) if (id.Val?.Value != null) id.Val = id.Val.Value + numberBase;
            var abstracts = ours.Elements<W.AbstractNum>().ToList();
            var instances = ours.Elements<W.NumberingInstance>().ToList();
            foreach (var a in abstracts) a.AbstractNumberId = (a.AbstractNumberId?.Value ?? 0) + abstractBase;
            foreach (var i in instances)
            {
                i.NumberID = (i.NumberID?.Value ?? 0) + numberBase;
                if (i.AbstractNumId?.Val?.Value != null) i.AbstractNumId.Val = i.AbstractNumId.Val.Value + abstractBase;
            }
            var lastAbstract = theirs.Elements<W.AbstractNum>().LastOrDefault();
            foreach (var a in abstracts)
            {
                var copy = a.CloneNode(true);
                if (lastAbstract != null) lastAbstract = theirs.InsertAfter(copy, lastAbstract) as W.AbstractNum;
                else theirs.InsertAt(copy, 0);
            }
            foreach (var i in instances) theirs.Append(i.CloneNode(true));
            return true;
        }
    }
}
