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
    this.contentState.pasteHandler(type, text ?? '', html ?? '')
  }

}

export default Clipboard
