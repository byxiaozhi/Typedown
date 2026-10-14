// ==text== renders as <mark>, the markers greyed and shown only in the active block, like ~~del~~.
export default function mark (h, cursor, block, token, outerClass) {
  return this.delEmStrongFac('mark', h, cursor, block, token, outerClass)
}
