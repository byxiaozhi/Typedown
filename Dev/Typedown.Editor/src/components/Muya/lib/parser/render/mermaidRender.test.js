import StateRender from './index'
import loadRenderer from '../../renderers'

jest.mock('../../renderers', () => jest.fn())

const deferred = () => {
  let resolve
  const promise = new Promise(done => { resolve = done })
  return { promise, resolve }
}

const waitForCall = async mock => {
  for (let i = 0; i < 20 && mock.mock.calls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 0))
  }
}

const makeState = () => new StateRender({
  options: { readOnly: false },
  eventCenter: { dispatch: jest.fn() },
  contentState: {
    cursor: { start: {}, end: {} },
    selectedBlock: null
  }
})

beforeEach(() => {
  document.body.innerHTML = '<div id="diagram"></div>'
  window.actualTheme = 'light'
  loadRenderer.mockReset()
})

test('serializes Mermaid renders and leaves the newest edited block rendered', async () => {
  const first = deferred()
  let active = 0
  let maxActive = 0
  const mermaid = {
    initialize: jest.fn(),
    render: jest.fn(async (_id, code) => {
      active++
      maxActive = Math.max(maxActive, active)
      if (code === 'first') await first.promise
      active--
      return { svg: `<svg data-code="${code}"></svg>` }
    })
  }
  loadRenderer.mockResolvedValue(mermaid)

  const state = makeState()
  const oldTarget = document.querySelector('#diagram')
  state.mermaidCache.set('#diagram', { code: 'first' })
  const firstRender = state.renderMermaid()
  await waitForCall(mermaid.render)
  expect(mermaid.render).toHaveBeenCalledTimes(1)

  oldTarget.replaceWith(Object.assign(document.createElement('div'), { id: 'diagram' }))
  state.mermaidCache.set('#diagram', { code: 'latest' })
  const latestRender = state.renderMermaid()
  first.resolve()
  await Promise.all([firstRender, latestRender])

  expect(maxActive).toBe(1)
  expect(document.querySelector('#diagram svg')).toHaveAttribute('data-code', 'latest')
})

test('bounds the SVG cache while Mermaid source is edited repeatedly', async () => {
  loadRenderer.mockResolvedValue({
    initialize: jest.fn(),
    render: jest.fn(async (_id, code) => ({ svg: `<svg data-code="${code}"></svg>` }))
  })
  const state = makeState()

  for (let i = 0; i < 80; i++) {
    const target = document.querySelector('#diagram')
    target.replaceWith(Object.assign(document.createElement('div'), { id: 'diagram' }))
    state.mermaidCache.set('#diagram', { code: `graph-${i}` })
    await state.renderMermaid()
  }

  expect(state.mermaidRenderCache.size).toBeLessThanOrEqual(32)
  expect(document.querySelector('#diagram svg')).toHaveAttribute('data-code', 'graph-79')
})
