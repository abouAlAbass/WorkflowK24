import { useState, useEffect } from 'react';
import { AppLayout } from './components/layout/AppLayout';
import { HomeDashboard } from './components/dashboard/HomeDashboard';
import { ActiveInstancesView } from './components/views/ActiveInstancesView';
import { DataModelsView } from './components/views/DataModelsView';
import { TaskCenterView } from './components/views/TaskCenterView';
import { AnalyticsView } from './components/views/AnalyticsView';
import { UsersView } from './components/views/UsersView';
import { SettingsView } from './components/views/SettingsView';
import { HeaderToolbar } from './components/header/HeaderToolbar';
import { NodePalette } from './components/palette/NodePalette';
import { WorkflowCanvas } from './components/canvas/WorkflowCanvas';
import { ConfigDrawer } from './components/sidebar/ConfigDrawer';
import { SimulatorPanel } from './components/modals/SimulatorPanel';
import { JsonSchemaModal } from './components/modals/JsonSchemaModal';
import { ValidationModal } from './components/modals/ValidationModal';
import { ModeSelectorModal } from './components/modals/ModeSelectorModal';
import { useWorkflowStore } from './hooks/useWorkflowStore';

export function App() {
  const [isJsonModalOpen, setIsJsonModalOpen] = useState(false);
  const [jsonModalTab, setJsonModalTab] = useState<'export' | 'import' | 'database'>('export');
  const [isValidationModalOpen, setIsValidationModalOpen] = useState(false);
  
  const currentTab = useWorkflowStore((state) => state.currentTab);
  const isModeModalOpen = useWorkflowStore((state) => state.isModeModalOpen);
  const closeModeModal = useWorkflowStore((state) => state.closeModeModal);
  const syncDataFromRepository = useWorkflowStore((state) => state.syncDataFromRepository);

  useEffect(() => {
    syncDataFromRepository();
  }, [syncDataFromRepository]);

  const handleOpenJsonModal = (tab: 'export' | 'import' | 'database' = 'export') => {
    setJsonModalTab(tab);
    setIsJsonModalOpen(true);
  };

  const renderCurrentView = () => {
    switch (currentTab) {
      case 'dashboard':
        return <HomeDashboard />;

      case 'designer':
        return (
          <div className="flex-1 flex flex-col overflow-hidden relative">
            <HeaderToolbar
              onOpenJsonModal={handleOpenJsonModal}
              onOpenValidationModal={() => setIsValidationModalOpen(true)}
            />
            <div className="flex-1 flex overflow-hidden relative">
              <NodePalette />
              <WorkflowCanvas />
              <ConfigDrawer />
              <SimulatorPanel />
            </div>
          </div>
        );

      case 'instances':
        return <ActiveInstancesView />;

      case 'smartobjects':
        return <DataModelsView />;

      case 'tasks':
        return <TaskCenterView />;

      case 'analytics':
        return <AnalyticsView />;

      case 'users':
        return <UsersView />;

      case 'settings':
        return <SettingsView />;

      default:
        return <HomeDashboard />;
    }
  };

  return (
    <AppLayout>
      {renderCurrentView()}

      {/* Designer Modals */}
      <JsonSchemaModal
        isOpen={isJsonModalOpen}
        initialTab={jsonModalTab}
        onClose={() => setIsJsonModalOpen(false)}
      />

      <ValidationModal
        isOpen={isValidationModalOpen}
        onClose={() => setIsValidationModalOpen(false)}
      />

      {/* Runtime Mode & Login Selector */}
      <ModeSelectorModal
        isOpen={isModeModalOpen}
        onClose={closeModeModal}
        canDismiss={true}
      />
    </AppLayout>
  );
}

export default App;
