class LinkTools {
  static pluginName = 'linkTools'

  constructor(muya) {
    const { eventCenter } = muya
    eventCenter.subscribe('muya-link-tools', ({ reference }) => {
      if (reference && !reference.getAttribute('data-tooltip')) {
        reference.setAttribute('data-tooltip', "CtrlAndClickOpenLink")
        muya.tooltip.mouseOver({
          target: {
            closest: () => reference
          }
        })
      }
    })
  }

}

export default LinkTools
