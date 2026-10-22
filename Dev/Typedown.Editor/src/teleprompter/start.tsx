import React from 'react'
import ReactDOM from 'react-dom'
import Teleprompter from './Teleprompter'

// The teleprompter and the speaking clock are pages of their own (a window each), made from the same bundle as the editor.
export default function start (clockOnly: boolean) {
    ReactDOM.render(<Teleprompter clockOnly={clockOnly} />, document.getElementById('root'))
}
