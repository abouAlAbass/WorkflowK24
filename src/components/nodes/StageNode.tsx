import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { Layers, ArrowRight, Sparkles, AlertTriangle, CheckCircle2 } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const StageNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const stage = customData.stageData;
  const { activeSimNodeId, enterStage } = useWorkflowStore();
  const isSimActive = activeSimNodeId === id;

  const subNodes = stage?.subWorkflow?.nodes || [];
  const subEdges = stage?.subWorkflow?.edges || [];
  const stageName = stage?.stageName || customData.label || 'Workflow Stage';
  const stageDescription = stage?.description || 'Composite stage with encapsulated sub-process workflow logic.';

  const handleOpenStage = (e: React.MouseEvent) => {
    e.stopPropagation();
    enterStage(id);
  };

  return (
    <div
      onDoubleClick={handleOpenStage}
      data-testid={`stage-node-${id}`}
      className={`relative min-w-[320px] max-w-[360px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-indigo-500 dark:border-indigo-500 transition-all duration-200 shadow-xl shadow-indigo-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive 
          ? 'ring-4 ring-amber-400 scale-105 shadow-amber-200 dark:shadow-amber-950' 
          : selected 
            ? 'ring-4 ring-indigo-500/30 shadow-indigo-100 dark:shadow-indigo-950 scale-[1.02]' 
            : 'hover:shadow-2xl hover:border-indigo-600 dark:hover:border-indigo-400'
      }`}
    >
      {/* Target Inbound Connection Handle */}
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-indigo-600 !border-2 !border-white hover:!bg-indigo-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow transition"
        aria-label="Stage inbound handle"
      />

      {/* Node Header */}
      <div className="flex items-center justify-between p-3.5 border-b border-indigo-100 dark:border-indigo-900/60 bg-gradient-to-r from-indigo-50 via-purple-50 to-blue-50 dark:from-slate-800 dark:via-indigo-950/40 dark:to-slate-800 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-indigo-600 text-white shadow-sm shadow-indigo-500/30">
            <Layers className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center gap-1.5">
              <span className="text-[10px] font-mono font-extrabold tracking-wider text-indigo-600 dark:text-indigo-400 uppercase bg-indigo-100/70 dark:bg-indigo-950 px-1.5 py-0.2 rounded border border-indigo-200 dark:border-indigo-800">
                Composite Stage
              </span>
              <span className="text-[10px] text-slate-400 font-mono">Sub-process</span>
            </div>
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-slate-100 truncate max-w-[210px] mt-0.5">
              {stageName}
            </h3>
          </div>
        </div>

        {isSimActive && (
          <span className="flex h-2.5 w-2.5 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-amber-500" />
          </span>
        )}
      </div>

      {/* Node Body */}
      <div className="p-3.5 space-y-3">
        <p className="text-xs text-[#475569] dark:text-slate-300 leading-relaxed font-medium line-clamp-2">
          {stageDescription}
        </p>

        {/* Sub-workflow metrics pill */}
        <div className="flex items-center justify-between p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800 text-[11px] font-mono">
          <div className="flex items-center gap-1.5 text-indigo-600 dark:text-indigo-400 font-bold">
            <Sparkles className="w-3.5 h-3.5" />
            <span>{subNodes.length} Sub-Steps</span>
          </div>
          <span className="text-slate-400">
            {subEdges.length} Transitions
          </span>
        </div>

        {/* Miniature sub-step flow preview */}
        {subNodes.length > 0 && (
          <div className="flex items-center gap-1 overflow-x-auto py-1 text-[10px] font-medium text-slate-600 dark:text-slate-400 no-scrollbar">
            {subNodes.slice(0, 4).map((node, index) => (
              <React.Fragment key={node.id}>
                <span className="px-2 py-0.5 rounded-md bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-900 whitespace-nowrap truncate max-w-[85px]">
                  {node.data?.label || node.id}
                </span>
                {index < Math.min(subNodes.length - 1, 3) && (
                  <ArrowRight className="w-3 h-3 text-slate-300 dark:text-slate-600 shrink-0" />
                )}
              </React.Fragment>
            ))}
            {subNodes.length > 4 && (
              <span className="text-[9px] text-slate-400 font-bold">+{subNodes.length - 4}</span>
            )}
          </div>
        )}

        {/* Drill-down action button */}
        <button
          onClick={handleOpenStage}
          data-testid={`open-stage-btn-${id}`}
          className="w-full py-2 px-3 rounded-xl bg-indigo-50 hover:bg-indigo-100 dark:bg-indigo-950/70 dark:hover:bg-indigo-900/80 border border-indigo-200 dark:border-indigo-800 text-indigo-700 dark:text-indigo-300 text-xs font-bold flex items-center justify-center gap-2 cursor-pointer transition-all active:scale-[0.98] shadow-2xs group"
        >
          <span>Design Stage Workflow</span>
          <ArrowRight className="w-3.5 h-3.5 transition-transform group-hover:translate-x-1" />
        </button>
      </div>

      {/* Outbound Handles */}
      {/* 1. Stage Complete Success Handle */}
      <div className="relative py-1 border-t border-slate-100 dark:border-slate-800 px-3.5 flex items-center justify-between text-[11px] font-bold text-emerald-600 dark:text-emerald-400">
        <span className="flex items-center gap-1">
          <CheckCircle2 className="w-3 h-3" />
          <span>Stage Complete</span>
        </span>
        <Handle
          type="source"
          position={Position.Right}
          id="stage-complete"
          className="!w-3.5 !h-3.5 !bg-emerald-600 !border-2 !border-white hover:!bg-emerald-700 !-right-2 transition-transform hover:scale-125 shadow-sm"
          title="Fires when stage sub-process completes successfully"
          aria-label="Stage complete output handle"
        />
      </div>

      {/* 2. Stage Exception / Reject Handle */}
      <div className="relative py-1 border-t border-slate-100 dark:border-slate-800 px-3.5 flex items-center justify-between text-[11px] font-bold text-red-600 dark:text-red-400">
        <span className="flex items-center gap-1">
          <AlertTriangle className="w-3 h-3" />
          <span>Stage Failed / Rejected</span>
        </span>
        <Handle
          type="source"
          position={Position.Right}
          id="stage-failed"
          className="!w-3.5 !h-3.5 !bg-red-600 !border-2 !border-white hover:!bg-red-700 !-right-2 transition-transform hover:scale-125 shadow-sm"
          title="Fires if stage fails or is terminated"
          aria-label="Stage failed output handle"
        />
      </div>
    </div>
  );
});

StageNode.displayName = 'StageNode';
