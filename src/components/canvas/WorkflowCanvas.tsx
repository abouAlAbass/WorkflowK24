import React, { useCallback, useMemo } from 'react';
import { 
  ReactFlow, 
  Background, 
  Controls, 
  MiniMap, 
  BackgroundVariant, 
  useReactFlow,
  ReactFlowProvider
} from '@xyflow/react';
import type { Node } from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useTheme } from '../../hooks/useTheme';
import { StartNode } from '../nodes/StartNode';
import { ApprovalNode } from '../nodes/ApprovalNode';
import { SendEmailNode } from '../nodes/SendEmailNode';
import { EndNode } from '../nodes/EndNode';
import { DecisionNode } from '../nodes/DecisionNode';
import { StageNode } from '../nodes/StageNode';
import { CustomEdge } from './CustomEdge';
import { ArrowLeft, ChevronRight, Layers, Sparkles } from 'lucide-react';
import type { NodeType, WorkflowNodeCustomData } from '../../types/workflow';

const CanvasContent: React.FC = () => {
  const { 
    nodes, 
    edges, 
    onNodesChange, 
    onEdgesChange, 
    onConnect, 
    setSelectedNodeId, 
    addNode,
    workflowName,
    activeStageId,
    stageStack,
    exitStage,
    navigateToStageBreadcrumb
  } = useWorkflowStore();

  const { theme } = useTheme();
  const isDark = theme === 'dark';

  const reactFlowInstance = useReactFlow();

  const nodeTypes = useMemo(() => ({
    trigger: StartNode,
    approval: ApprovalNode,
    sendEmail: SendEmailNode,
    end: EndNode,
    decision: DecisionNode,
    stage: StageNode,
  }), []);

  const edgeTypes = useMemo(() => ({
    default: CustomEdge,
    custom: CustomEdge
  }), []);

  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();

      const type = event.dataTransfer.getData('application/reactflow') as NodeType;
      if (!type) return;

      const position = reactFlowInstance.screenToFlowPosition({
        x: event.clientX,
        y: event.clientY,
      });

      addNode(type, position);
    },
    [reactFlowInstance, addNode]
  );

  const onNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      setSelectedNodeId(node.id);
    },
    [setSelectedNodeId]
  );

  const onPaneClick = useCallback(() => {
    setSelectedNodeId(null);
  }, [setSelectedNodeId]);

  return (
    <div 
      className="w-full h-full relative flex-1 overflow-hidden select-none bg-slate-50 dark:bg-slate-950 transition-colors"
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      {/* Stage Scope Breadcrumb Navigation Bar */}
      {activeStageId && (
        <div 
          data-testid="stage-breadcrumb-banner"
          className="absolute top-4 left-6 right-6 z-20 flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3 rounded-2xl bg-white/95 dark:bg-slate-900/95 backdrop-blur-md border border-indigo-200 dark:border-indigo-900/60 shadow-xl shadow-indigo-950/10 transition-all pointer-events-auto"
        >
          <div className="flex items-center gap-2 flex-wrap">
            <button
              onClick={exitStage}
              data-testid="stage-breadcrumb-back-btn"
              className="px-3.5 py-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-extrabold text-xs flex items-center gap-1.5 shadow-md shadow-indigo-600/20 cursor-pointer transition-all active:scale-95"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>Back to Main Workflow</span>
            </button>

            <div className="h-4 w-px bg-slate-200 dark:bg-slate-700 mx-1 hidden sm:block" />

            {/* Breadcrumb Trail */}
            <nav aria-label="Stage Breadcrumbs" className="flex items-center gap-1.5 text-xs">
              <button
                onClick={() => navigateToStageBreadcrumb(-1)}
                className="text-slate-500 dark:text-slate-400 hover:text-indigo-600 dark:hover:text-indigo-400 font-bold transition-colors cursor-pointer"
              >
                {workflowName || 'Main Workflow'}
              </button>

              {stageStack.map((item, idx) => (
                <React.Fragment key={item.stageId}>
                  <ChevronRight className="w-3.5 h-3.5 text-slate-400" />
                  {idx < stageStack.length - 1 ? (
                    <button
                      onClick={() => navigateToStageBreadcrumb(idx)}
                      className="text-slate-500 dark:text-slate-400 hover:text-indigo-600 dark:hover:text-indigo-400 font-bold transition-colors cursor-pointer"
                    >
                      {item.stageName}
                    </button>
                  ) : (
                    <span className="px-2.5 py-0.5 rounded-lg bg-indigo-50 dark:bg-indigo-950/80 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800 font-extrabold flex items-center gap-1.5 shadow-2xs">
                      <Layers className="w-3.5 h-3.5 text-indigo-600 dark:text-indigo-400" />
                      <span>{item.stageName}</span>
                    </span>
                  )}
                </React.Fragment>
              ))}
            </nav>
          </div>

          <div className="flex items-center gap-3 text-xs font-mono">
            <span className="px-2.5 py-1 rounded-lg bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 font-bold">
              {nodes.length} Sub-Nodes • {edges.length} Transitions
            </span>
            <span className="text-[11px] font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
              <Sparkles className="w-3.5 h-3.5" />
              <span>Stage Scope Active</span>
            </span>
          </div>
        </div>
      )}

      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onNodeClick={onNodeClick}
        onPaneClick={onPaneClick}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        defaultEdgeOptions={{
          type: 'default',
          animated: true,
          style: { stroke: isDark ? '#64748B' : '#94A3B8', strokeWidth: 2 }
        }}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        className="nintex-flow-canvas"
        proOptions={{ hideAttribution: true }}
      >
        {/* Dynamic Dot Grid Background */}
        <Background 
          variant={BackgroundVariant.Dots} 
          gap={24} 
          size={1.5} 
          color={isDark ? '#334155' : '#CBD5E1'} 
        />

        {/* 5. Restyle Minimap grouped with zoom controls */}
        <div className="absolute bottom-4 right-4 z-10 flex flex-col items-end gap-2 pointer-events-none">
          <Controls 
            className={`!static !m-0 pointer-events-auto transition-colors ${
              isDark 
                ? '!rounded-xl shadow-md !bg-slate-900/90 !backdrop-blur-md !border !border-slate-700 !text-slate-100' 
                : '!rounded-xl shadow-md !bg-white/90 !backdrop-blur-md !border !border-slate-300 !text-[#0F172A]'
            }`}
            showInteractive={false}
          />
          <MiniMap 
            nodeStrokeWidth={2}
            zoomable
            pannable
            aria-label="Workflow Canvas Minimap"
            style={{ width: 160, height: 100 }}
            className={`!static !m-0 pointer-events-auto transition-all ${
              isDark 
                ? '!rounded-xl overflow-hidden shadow-md !border !border-slate-700 !bg-slate-900/85 !backdrop-blur-md' 
                : '!rounded-xl overflow-hidden shadow-md !border !border-slate-300 !bg-white/85 !backdrop-blur-md'
            }`}
            nodeColor={(node: Node) => {
              const data = node.data as unknown as WorkflowNodeCustomData;
              switch (data?.nodeType) {
                case 'trigger': return '#059669';
                case 'approval': return '#2563EB';
                case 'sendEmail': return '#D97706';
                case 'end': return '#DC2626';
                case 'decision': return '#7C3AED';
                case 'stage': return '#6366F1';
                default: return '#94A3B8';
              }
            }}
            maskColor={isDark ? 'rgba(11, 15, 25, 0.75)' : 'rgba(241, 245, 249, 0.75)'}
          />
        </div>
      </ReactFlow>
    </div>
  );
};

export const WorkflowCanvas: React.FC = () => {
  return (
    <ReactFlowProvider>
      <CanvasContent />
    </ReactFlowProvider>
  );
};
