import transport from 'services/transport'

class TableBarTools {
  static pluginName = 'tableBarTools'

  constructor(muya) {
    muya.eventCenter.subscribe('muya-table-bar', ({ reference, tableInfo }) => {
      if (reference) {
        const boundingClientRect = reference.getBoundingClientRect()
        transport.postMessage('OpenTableTools', { tableInfo, boundingClientRect })
      }
    })

    this.onEditTable = item => {
      muya.contentState.editTable(item)
    }

    transport.addListener('EditTable', this.onEditTable);
  }

  destroy() {
    transport.removeListener('EditTable', this.onEditTable);
  }

}

export default TableBarTools
