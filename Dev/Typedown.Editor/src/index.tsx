import React from 'react';
import ReactDOM from 'react-dom';

// One bundle, three pages: the editor, and (in windows of their own, opened from the View menu) the teleprompter and the speaking
// clock. The editor's code is not loaded for the other two: it asks its host for things only the editor window answers.
const page = window.location.hash
if (page === '#teleprompter' || page === '#clock') {
  import('./teleprompter/start').then(m => m.default(page === '#clock'))
} else {
  import('./App').then(({ default: App }) => {
    ReactDOM.render(
      <React.StrictMode>
        <App />
      </React.StrictMode>,
      document.getElementById('root')
    );
  })
}
