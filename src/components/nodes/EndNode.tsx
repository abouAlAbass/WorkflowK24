import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { CheckCircle, XCircle, AlertTriangle } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const EndNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const end = customData.endData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const status = end?.status || 'completed';

  const getStatusConfig = () => {
    switch (status) {
      case 'completed':
        return {
          icon: <CheckCircle className="w-5 h-5 text-[#059669]" />,
          badgeClass: 'bg-emerald-50 text-[#059669] border-emerald-200',
          ringClass: 'ring-emerald-400 border-emerald-500',
          selectedBorder: 'ring-4 ring-emerald-500/30',
          headerBg: 'bg-emerald-50/70 border-b border-emerald-100',
          handleBg: '!bg-[#059669]'
        };
      case 'rejected':
      case 'terminated':
        return {
          icon: <XCircle className="w-5 h-5 text-[#DC2626]" />,
          badgeClass: 'bg-[#FEF2F2] text-[#DC2626] border-red-200',
          ringClass: 'ring-red-400 border-[#DC2626]',
          selectedBorder: 'ring-4 ring-red-500/30',
          headerBg: 'bg-[#FEF2F2] border-b border-red-100',
          handleBg: '!bg-[#DC2626]'
        };
      default:
        return {
          icon: <AlertTriangle className="w-5 h-5 text-[#D97706]" />,
          badgeClass: 'bg-amber-50 text-[#D97706] border-amber-200',
          ringClass: 'ring-amber-400 border-amber-500',
          selectedBorder: 'ring-4 ring-amber-500/30',
          headerBg: 'bg-amber-50/70 border-b border-amber-100',
          handleBg: '!bg-[#D97706]'
        };
    }
  };

  const config = getStatusConfig();

  return (
    <div
      className={`relative min-w-[260px] max-w-[300px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#DC2626] transition-all duration-200 shadow-lg shadow-red-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive 
          ? `ring-4 ${config.ringClass} scale-105 shadow-red-200` 
          : selected 
            ? config.selectedBorder 
            : 'hover:shadow-xl hover:border-red-600'
      }`}
    >
      {/* Target Inbound Handle - 14px target */}
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className={`!w-3.5 !h-3.5 ${config.handleBg} !border-2 !border-white hover:scale-125 !-left-2 transition-transform shadow-sm`}
        title="Connect inbound workflow step"
        aria-label="End event target handle"
      />

      {/* Header */}
      <div className={`flex items-center justify-between p-3.5 ${config.headerBg} dark:bg-slate-800/80 rounded-t-[14px]`}>
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 border border-red-200 dark:border-red-900 shadow-sm">
            {config.icon}
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider uppercase text-[#DC2626] dark:text-rose-400">
              End Event
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[160px]">
              {customData.label || end?.label || 'End Event'}
            </h3>
          </div>
        </div>
        {isSimActive && (
          <span className="flex h-2.5 w-2.5 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-[#DC2626]" />
          </span>
        )}
      </div>

      {/* Body - Font sizes >= 12px for secondary text */}
      <div className="p-3.5 space-y-2 text-xs">
        <div className="flex items-center justify-between p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800">
          <span className="text-[#475569] dark:text-slate-300 font-medium text-xs">Terminal Status:</span>
          <span className={`font-bold capitalize px-2 py-0.5 rounded-md border text-xs ${config.badgeClass} dark:bg-slate-800`}>
            {status}
          </span>
        </div>

        {end?.message && (
          <p className="p-2 rounded-xl bg-slate-50/80 dark:bg-slate-800/40 text-xs text-[#475569] dark:text-slate-300 italic truncate border border-slate-200 dark:border-slate-800 font-medium" title={end.message}>
            "{end.message}"
          </p>
        )}
      </div>
    </div>
  );
});

EndNode.displayName = 'EndNode';
