import remote from 'services/remote/common'

class TablePicker {
  static pluginName = 'tablePicker'
  constructor(muya) {
    muya.eventCenter.subscribe('muya-table-picker', async (data, reference, cb) => {
      const { rows, columns } = await remote.resizeTable({ rows: data.row + 1, columns: data.column + 1 });
      cb(Math.max(rows - 1, 0), Math.max(columns - 1, 0))
    })
  }
}

export default TablePicker
