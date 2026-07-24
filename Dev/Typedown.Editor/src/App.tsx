import React from 'react';
import ErrorBoundary from 'components/ErrorBoundary';
import TabCoordinator from 'components/Tabs/TabCoordinator';
import 'services/theme'
import 'services/scrollbar'
import 'services/localization'
import './App.scss';

document.oncontextmenu = () => false;

function App() {
  return (
    <ErrorBoundary>
      <TabCoordinator />
    </ErrorBoundary>
  );
}

export default App;
