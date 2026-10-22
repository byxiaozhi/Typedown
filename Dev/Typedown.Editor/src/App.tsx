import React from 'react';
import Editor from 'components/Editor';
import ErrorBoundary from 'components/ErrorBoundary';
import 'services/theme'
import 'services/scrollbar'
import 'services/localization'
import 'services/speechPage'
import 'services/speechRing'
import 'services/punctuation'
import 'services/trackJump'
import './App.scss';

document.oncontextmenu = () => false;

function App() {
  return (
    <ErrorBoundary>
      <Editor />
    </ErrorBoundary>
  );
}

export default App;
