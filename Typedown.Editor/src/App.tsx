import React from 'react';
import Editor from 'components/Editor';
import 'services/theme'
import 'services/localization'
import './App.scss';

document.oncontextmenu = () => false;

function App() {
  return (
    <Editor />
  );
}

export default App;
