import React, { useState, useEffect } from 'react';
import { 
  Sliders, 
  Trash2, 
  X, 
  Play, 
  CheckCircle2, 
  Mail, 
  Flag, 
  GitBranch, 
  Layers,
  Clock
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useTheme } from '../../hooks/useTheme';
import { TriggerConfig } from './TriggerConfig';
import { ApprovalConfig } from './ApprovalConfig';
import { SendEmailConfig } from './SendEmailConfig';
import { EndEventConfig } from './EndEventConfig';
import { DecisionConfig } from './DecisionConfig';
import { StageConfig } from './StageConfig';
import type { WorkflowNodeCustomData } from '../../types/workflow';

export const ConfigDrawer: React.FC = () => {
  const { nodes, selectedNodeId, setSelectedNodeId, deleteNode } = useWorkflowStore();
  const { t } = useTheme();
  const [autoSaveTime, setAutoSaveTime] = useState<string>(() => new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }));

  const selectedNode = nodes.find(n => n.id === selectedNodeId);
  const data = selectedNode?.data as unknown as WorkflowNodeCustomData;

  useEffect(() => {
    setAutoSaveTime(new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
  }, [data]);

  const getNodeIcon = () => {
    switch (data?.nodeType) {
      case 'trigger': return <Play className="w-4 h-4 text-[#059669]" />;
      case 'approval': return <CheckCircle2 className="w-4 h-4 text-[#2563EB]" />;
      case 'sendEmail': return <Mail className="w-4 h-4 text-[#D97706]" />;
      case 'end': return <Flag className="w-4 h-4 text-[#DC2626]" />;
      case 'decision': return <GitBranch className="w-4 h-4 text-[#7C3AED]" />;
      case 'stage': return <Layers className="w-4 h-4 text-[#6366F1]" />;
      default: return <Sliders className="w-4 h-4 text-[#475569]" />;
    }
  };

  const renderConfigBody = () => {
    if (!selectedNode || !data) return null;

    switch (data.nodeType) {
      case 'trigger':
        return <TriggerConfig nodeId={selectedNode.id} data={data} />;
      case 'approval':
        return <ApprovalConfig nodeId={selectedNode.id} data={data} />;
      case 'sendEmail':
        return <SendEmailConfig nodeId={selectedNode.id} data={data} />;
      case 'end':
        return <EndEventConfig nodeId={selectedNode.id} data={data} />;
      case 'decision':
        return <DecisionConfig nodeId={selectedNode.id} data={data} />;
      case 'stage':
        return <StageConfig nodeId={selectedNode.id} data={data} />;
      default:
        return (
          <div className="text-xs text-[#475569] dark:text-slate-300 p-4">
            No inspector available for this node type.
          </div>
        );
    }
  };

  if (!selectedNode || !data) {
    return (
      <aside 
        aria-label="Node Configuration Inspector"
        className="w-80 bg-[#F8FAFC] dark:bg-slate-900 border-s border-slate-200 dark:border-slate-800 flex flex-col h-full z-10 p-6 select-none justify-center items-center text-center shadow-xs text-[#0F172A] dark:text-slate-100 transition-colors"
      >
        <div className="p-4 rounded-2xl border border-blue-200 dark:border-blue-800 bg-[#EFF6FF] dark:bg-blue-950/40 text-[#2563EB] dark:text-blue-400 mb-3 shadow-xs">
          <Layers className="w-8 h-8" />
        </div>
        <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100">
          {t.drawer.noNodeSelected}
        </h3>
        <p className="text-xs text-[#475569] dark:text-slate-300 mt-1 max-w-[220px] leading-relaxed font-medium">
          {t.drawer.noNodeHint}
        </p>
      </aside>
    );
  }

  return (
    <aside 
      aria-label={`Configuration for ${data.label || 'Node'}`}
      className="w-80 bg-[#F8FAFC] dark:bg-slate-900 border-s border-slate-200 dark:border-slate-800 flex flex-col h-full z-10 select-none shadow-xs text-[#0F172A] dark:text-slate-100 transition-colors"
    >
      {/* Drawer Header */}
      <div className="p-3.5 border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 flex items-center justify-between">
        <div className="flex items-center gap-2.5 min-w-0 flex-1">
          <div className="p-1.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 shadow-2xs shrink-0">
            {getNodeIcon()}
          </div>
          <div className="min-w-0 flex-1">
            <h2 className="font-bold text-xs text-[#0F172A] dark:text-slate-100 truncate">
              {data.label || t.drawer.inspectorTitle}
            </h2>
            <span className="text-[10px] text-[#475569] dark:text-slate-400 font-mono font-semibold block">
              ID: {selectedNode.id}
            </span>
          </div>
        </div>

        <div className="flex items-center gap-1 shrink-0">
          <button
            type="button"
            onClick={() => deleteNode(selectedNode.id)}
            className="p-1.5 rounded-lg text-slate-400 hover:text-[#DC2626] dark:hover:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/50 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-rose-500"
            title={t.drawer.deleteNode}
            aria-label={t.drawer.deleteNode}
          >
            <Trash2 className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={() => setSelectedNodeId(null)}
            className="p-1.5 rounded-lg text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-100 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
            title={t.drawer.closeDrawer}
            aria-label={t.drawer.closeDrawer}
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-4">
        {renderConfigBody()}
      </div>

      {/* 6. Clarify auto-save vs Save Definition footer */}
      <div className="p-2.5 border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-[10px] text-[#475569] dark:text-slate-400 text-center font-medium flex items-center justify-center gap-1.5">
        <Clock className="w-3 h-3 text-[#059669] dark:text-emerald-400" />
        <span>{t.drawer.draftAutoSaved} at {autoSaveTime} • Use <strong>Save Definition</strong> for named versions</span>
      </div>
    </aside>
  );
};
