import { h } from '../snabbdom'

// The editor used to draw its small icons with private-use codepoints of "Segoe Fluent Icons". That font only
// exists on Windows, so on Linux and macOS every one of them came out as an empty box. These are the same icons
// as SVG, built as vnodes (snabbdom gives an "svg" subtree the right namespace) rather than innerHTML, which
// would leave children the patcher does not know about.
const ICONS = {
  // two overlapping sheets
  copy: [['rect', { x: 8.5, y: 8.5, width: 11, height: 11, rx: 1.5 }], ['path', { d: 'M15.5 5.5H5.5a1 1 0 0 0-1 1v10' }]],
  // pencil
  edit: [['path', { d: 'M4.5 19.5h4L19 9a1.8 1.8 0 0 0 0-2.5l-1.5-1.5a1.8 1.8 0 0 0-2.5 0L4.5 15.5z' }]],
  // arrow turning back
  back: [['path', { d: 'M9.5 6.5 5 11l4.5 4.5' }], ['path', { d: 'M5 11h9a4.5 4.5 0 0 1 0 9h-3' }]],
  // arrows pointing out of a box
  resize: [['path', { d: 'M14 5.5h4.5V10' }], ['path', { d: 'M10 18.5H5.5V14' }],
    ['path', { d: 'M18.5 5.5 13 11' }], ['path', { d: 'M5.5 18.5 11 13' }]],
  alignLeft: [['path', { d: 'M4.5 6.5h15' }], ['path', { d: 'M4.5 11.5h9' }], ['path', { d: 'M4.5 16.5h12' }]],
  alignCenter: [['path', { d: 'M4.5 6.5h15' }], ['path', { d: 'M7.5 11.5h9' }], ['path', { d: 'M6 16.5h12' }]],
  alignRight: [['path', { d: 'M4.5 6.5h15' }], ['path', { d: 'M10.5 11.5h9' }], ['path', { d: 'M7.5 16.5h12' }]],
  // waste basket
  trash: [['path', { d: 'M5.5 7.5h13' }], ['path', { d: 'M9.5 7.5V5.8a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v1.7' }],
    ['path', { d: 'M7 7.5l.9 11a1 1 0 0 0 1 .9h6.2a1 1 0 0 0 1-.9l.9-11' }]],
  // grid
  table: [['rect', { x: 4.5, y: 5.5, width: 15, height: 13, rx: 1 }], ['path', { d: 'M4.5 10h15' }], ['path', { d: 'M9.5 5.5v13' }]],
  // connected nodes
  diagram: [['rect', { x: 4.5, y: 4.5, width: 6, height: 5, rx: 1 }], ['rect', { x: 13.5, y: 14.5, width: 6, height: 5, rx: 1 }],
    ['path', { d: 'M7.5 9.5v4a2 2 0 0 0 2 2h4' }]],
  // angle brackets
  code: [['path', { d: 'M9 8.5 5 12l4 3.5' }], ['path', { d: 'M15 8.5 19 12l-4 3.5' }]],
  bulletList: [['path', { d: 'M9 7h10' }], ['path', { d: 'M9 12h10' }], ['path', { d: 'M9 17h10' }],
    ['circle', { cx: 5.5, cy: 7, r: 1.1 }], ['circle', { cx: 5.5, cy: 12, r: 1.1 }], ['circle', { cx: 5.5, cy: 17, r: 1.1 }]],
  numberList: [['path', { d: 'M9 7h10' }], ['path', { d: 'M9 12h10' }], ['path', { d: 'M9 17h10' }],
    ['path', { d: 'M4.4 5.6h1v3' }], ['path', { d: 'M4.2 10.8h1.6L4.2 13.2h1.6' }], ['path', { d: 'M4.3 15.6h1.4v1.2H4.6v1.2h1.1' }]],
  taskList: [['path', { d: 'M10 7h9' }], ['path', { d: 'M10 17h9' }],
    ['path', { d: 'M3.8 7 5 8.2 7.4 5.6' }], ['path', { d: 'M3.8 17 5 18.2l2.4-2.6' }]],
  quote: [['path', { d: 'M5.5 5.5v13' }], ['path', { d: 'M10 8h9' }], ['path', { d: 'M10 12h9' }], ['path', { d: 'M10 16h6' }]],
}

/**
 * A 24x24 line icon that follows the surrounding text colour.
 * @param {string} name one of ICONS
 * @param {number} size in pixels
 */
export const svgIcon = (name, size = 14) => h('span.icon', {
  style: { display: 'inline-flex', 'align-items': 'center', 'justify-content': 'center' }
}, [
  h('svg', {
    attrs: {
      viewBox: '0 0 24 24',
      width: size,
      height: size,
      fill: 'none',
      stroke: 'currentColor',
      'stroke-width': 1.6,
      'stroke-linecap': 'round',
      'stroke-linejoin': 'round',
      'aria-hidden': 'true'
    }
  }, (ICONS[name] || []).map(([tag, attrs]) => h(tag, { attrs })))
])

export default svgIcon
