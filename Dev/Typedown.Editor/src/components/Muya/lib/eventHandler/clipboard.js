class Clipboard {
  constructor(muya) {
    this.muya = muya
    this.listen()
  }

  listen() {
    this.contentState = this.muya.contentState
  }

  copy({ type, copyInfo }) {
    this.contentState.copyHandler(type, copyInfo)
  }

  cut({ type, copyInfo }) {
    this.contentState.copyHandler(type, copyInfo)
    this.contentState.cutHandler()
  }

  paste({ type, text, html }) {
    // A paste that fails (odd HTML the conversion cannot take) is reported, not lost silently.
    Promise.resolve(this.contentState.pasteHandler(type, text ?? '', html ?? ''))
      .catch(err => { console.log('paste failed', err); throw err })
  }

}

export default Clipboard
