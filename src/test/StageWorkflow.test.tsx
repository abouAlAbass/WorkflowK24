import { describe, it, expect, beforeEach } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { useWorkflowStore } from '../store/useWorkflowStore';
import { ReactFlowProvider } from '@xyflow/react';
import { StageNode } from '../components/nodes/StageNode';
import { WorkflowCanvas } from '../components/canvas/WorkflowCanvas';

describe('Workflow Stage & Sub-Workflow Drill-Down', () => {
  beforeEach(() => {
    // Reset canvas to standard onboarding template
    useWorkflowStore.getState().loadTemplate('onboarding');
  });

  it('initializes with a default IT Provisioning Stage node on the canvas', () => {
    const nodes = useWorkflowStore.getState().nodes;
    const stageNode = nodes.find(n => n.type === 'stage');

    expect(stageNode).toBeDefined();
    expect(stageNode?.data.stageData).toBeDefined();
    expect(stageNode?.data.stageData?.stageName).toBe('IT & Hardware Provisioning Stage');
    expect(stageNode?.data.stageData?.subWorkflow.nodes.length).toBeGreaterThanOrEqual(3);
  });

  it('allows adding a new Stage node from the toolbox', () => {
    const store = useWorkflowStore.getState();
    const initialCount = store.nodes.length;

    store.addNode('stage', { x: 500, y: 500 });

    const updatedNodes = useWorkflowStore.getState().nodes;
    expect(updatedNodes.length).toBe(initialCount + 1);

    const addedStage = updatedNodes[updatedNodes.length - 1];
    expect(addedStage.type === 'stage').toBe(true);
    expect(addedStage.data.nodeType).toBe('stage');
    expect(addedStage.data.stageData?.subWorkflow.nodes.length).toBe(3);
    expect(addedStage.data.stageData?.subWorkflow.edges.length).toBe(2);
  });

  it('drills down into stage workflow and returns cleanly with updated sub-workflow', () => {
    const store = useWorkflowStore.getState();
    const stageNode = store.nodes.find(n => n.type === 'stage');
    expect(stageNode).toBeDefined();
    if (!stageNode) return;

    const initialSubNodesCount = stageNode.data.stageData?.subWorkflow.nodes.length || 0;
    const parentNodesCount = store.nodes.length;

    // 1. Enter Stage
    store.enterStage(stageNode.id);

    expect(useWorkflowStore.getState().activeStageId).toBe(stageNode.id);
    expect(useWorkflowStore.getState().stageStack.length).toBe(1);
    expect(useWorkflowStore.getState().stageStack[0].stageId).toBe(stageNode.id);

    // Canvas now shows the sub-workflow nodes!
    expect(useWorkflowStore.getState().nodes.length).toBe(initialSubNodesCount);

    // 2. Add an internal step (e.g. an email notification inside this stage)
    store.addNode('sendEmail', { x: 900, y: 300 });
    expect(useWorkflowStore.getState().nodes.length).toBe(initialSubNodesCount + 1);

    // 3. Exit Stage
    store.exitStage();

    expect(useWorkflowStore.getState().activeStageId).toBeNull();
    expect(useWorkflowStore.getState().stageStack.length).toBe(0);

    // Parent canvas is restored
    expect(useWorkflowStore.getState().nodes.length).toBe(parentNodesCount);

    // And the stage node now holds the updated sub-workflow!
    const updatedStageNode = useWorkflowStore.getState().nodes.find(n => n.id === stageNode.id);
    expect(updatedStageNode?.data.stageData?.subWorkflow.nodes.length).toBe(initialSubNodesCount + 1);
  });

  it('renders StageNode component with drill-down button and sub-steps count', () => {
    const stageNode = useWorkflowStore.getState().nodes.find(n => n.type === 'stage');
    expect(stageNode).toBeDefined();
    if (!stageNode) return;

    render(
      <ReactFlowProvider>
        <StageNode 
          id={stageNode.id} 
          data={stageNode.data} 
          selected={false} 
          type="stage" 
          zIndex={1} 
          isConnectable={true} 
          positionAbsoluteX={0} 
          positionAbsoluteY={0} 
        />
      </ReactFlowProvider>
    );

    expect(screen.getByText(/Composite Stage/i)).toBeInTheDocument();
    expect(screen.getByText(/Design Stage Workflow/i)).toBeInTheDocument();
    expect(screen.getByTestId(`open-stage-btn-${stageNode.id}`)).toBeInTheDocument();
  });

  it('renders stage breadcrumb banner in WorkflowCanvas when a stage is active and allows exiting', () => {
    const stageNode = useWorkflowStore.getState().nodes.find(n => n.type === 'stage');
    expect(stageNode).toBeDefined();
    if (!stageNode) return;

    // Drill into stage
    useWorkflowStore.getState().enterStage(stageNode.id);

    render(<WorkflowCanvas />);

    // Breadcrumb banner should be visible
    expect(screen.getByTestId('stage-breadcrumb-banner')).toBeInTheDocument();
    expect(screen.getByText(/Back to Main Workflow/i)).toBeInTheDocument();
    expect(screen.getByText(/Stage Scope Active/i)).toBeInTheDocument();

    // Click back button
    const backBtn = screen.getByTestId('stage-breadcrumb-back-btn');
    fireEvent.click(backBtn);

    expect(useWorkflowStore.getState().activeStageId).toBeNull();
  });
});
