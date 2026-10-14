import remote from 'services/remote/common'

class TablePicker {
  static pluginName = 'tablePicker'
  constructor(muya) {
    muya.eventCenter.subscribe('muya-table-picker', async (data, reference, cb) => {
      // Cancelled, the dialog answers nothing and the table stays as it is (taking it apart threw).
      const result = await remote.resizeTable({ rows: data.row + 1, columns: data.column + 1 })
      if (!result) return
      const { rows, columns } = result
      cb(Math.max(rows - 1, 0), Math.max(columns - 1, 0))
    })
  }
}

export default TablePicker
