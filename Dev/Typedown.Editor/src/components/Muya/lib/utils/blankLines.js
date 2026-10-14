import { CURSOR_ANCHOR_DNA, CURSOR_FOCUS_DNA } from '../config'

// Blank lines between blocks.
//
// The editor shows extra blank lines as empty paragraphs, but fewer of them than there are lines: an empty paragraph
// is written back as two lines (itself and the gap after it), so a run of blank lines maps onto
// `visibleEmptyParagraphs` of them and the rest would be lost on the first visual edit. The lexer therefore records
// the exact count on the block after the run (`blankLinesBefore`), or on the last block of a document or quotation
// for a run at its end (`blankLinesAfter`), and the Markdown export writes that count back for as long as the empty
// paragraphs between the two blocks are still the ones the count was read with and the block is still in the
// container it was read in (`blankLinesParent`). Once the reader adds or removes an empty paragraph there, or moves
// the block into a quotation or a list, the count no longer applies and the run is written the ordinary way.

/** Empty paragraphs shown for `n` blank lines at the start of a container, between two blocks, or at its end. */
export const visibleEmptyParagraphs = (n, position) =>
  n <= 0 ? 0 : position === 'start' ? Math.floor(n / 2) : Math.floor((n - 1) / 2)

/** An empty paragraph: what a blank line in the source becomes, and what Enter on an empty line makes. The caret
 *  markers the export with a cursor puts into the text (getMarkdownAndCodeMirrorCursor) do not count. */
export const isEmptyParagraph = block =>
  !!block && block.type === 'p' && block.children.length === 1 && block.children[0].type === 'span' &&
  block.children[0].text.replace(CURSOR_ANCHOR_DNA, '').replace(CURSOR_FOCUS_DNA, '') === ''
