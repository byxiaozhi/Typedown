import transport from 'services/transport'

class FormatPicker {
  static pluginName = 'formatPicker'
  constructor(muya) {
    muya.eventCenter.subscribe('muya-format-picker', ({ reference, ...arg }) => {
      if (reference) {
        const boundingClientRect = reference.getBoundingClientRect()
        transport.postMessage('OpenFormatPicker', { ...arg, boundingClientRect })
      }
    })
  }
}

export default FormatPicker
