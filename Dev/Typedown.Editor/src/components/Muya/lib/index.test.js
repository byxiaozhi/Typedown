// config first: it and utils import each other, and only this order has both ready
import './config'
import Muya from './index'

// The editor's parts reach the WebView2 host and ESM-only packages; setMarkdown needs none of them.
jest.mock('./contentState', () => jest.fn())
jest.mock('./eventHandler/mouseEvent', () => jest.fn())
jest.mock('./eventHandler/clipboard', () => jest.fn())
jest.mock('./eventHandler/keyboard', () => jest.fn())
jest.mock('./eventHandler/dragDrop', () => jest.fn())
jest.mock('./eventHandler/resize', () => jest.fn())
jest.mock('./eventHandler/clickEvent', () => jest.fn())
jest.mock('./utils/exportMarkdown', () => jest.fn())
jest.mock('../../../services/exportHtml', () => jest.fn())
jest.mock('./ui/tooltip', () => jest.fn())
jest.mock('./parser/speech', () => ({ stripMarks: jest.fn() }))

// The mini toolbar hides on this event: whatever replaces the content of the editor has to say it.
describe('Muya.setMarkdown', () => {
  const fake = () => ({
    eventCenter: { dispatch: jest.fn() },
    contentState: { importMarkdown: jest.fn(), importCursor: jest.fn(), render: jest.fn() },
    dispatchChange: jest.fn()
  })

  it('says that the content is replaced, before it is', () => {
    jest.useFakeTimers()
    const muya = fake()
    muya.eventCenter.dispatch.mockImplementation(() => expect(muya.contentState.importMarkdown).not.toHaveBeenCalled())

    Muya.prototype.setMarkdown.call(muya, '# replacement')

    expect(muya.eventCenter.dispatch).toHaveBeenCalledWith('muya-markdown-set')
    expect(muya.contentState.importMarkdown).toHaveBeenCalledWith('# replacement')
    jest.useRealTimers()
  })
})
