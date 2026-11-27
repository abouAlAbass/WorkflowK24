import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { Database, ShieldAlert, ArrowRightLeft } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const SmartObjectNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const so = customData.smartObjectData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const inputCount = Object.keys(so?.inputMapping || {}).length;
  const outputCount = Object.keys(so?.outputMapping || {}).length;

  return (
    <div
      className={`relative min-w-[280px] max-w-[320px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#0D9488] transition-all duration-200 shadow-lg shadow-teal-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive
          ? 'ring-4 ring-teal-400 scale-105 shadow-teal-200'
          : selected
            ? 'ring-4 ring-teal-500/30 shadow-teal-100 dark:shadow-teal-950'
            : 'hover:shadow-xl hover:border-teal-600'
      }`}
    >
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-[#0D9488] !border-2 !border-white hover:!bg-teal-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow step"
        aria-label="Inbound target handle"
      />

      <div className="flex items-center justify-between p-3.5 border-b border-teal-100 dark:border-teal-900/60 bg-[#F0FDFA] dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#0D9488] dark:text-teal-400 border border-teal-200 dark:border-teal-800 shadow-sm shrink-0">
            <Database className="w-5 h-5 text-[#0D9488] dark:text-teal-400" />
          </div>
          <div className="min-w-0">
            <span className="text-xs font-extrabold tracking-wider text-[#0D9488] dark:text-teal-400 uppercase">
              SmartObject
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[180px]">
              {customData.label || so?.label || 'SmartObject Action'}
            </h3>
          </div>
        </div>
        <span className="px-2 py-0.5 rounded-md text-[10px] font-mono font-extrabold shrink-0 bg-teal-100 text-teal-800 dark:bg-teal-900/50 dark:text-teal-300">
          {so?.methodName || 'method'}
        </span>
      </div>

      <div className="p-3.5 space-y-2.5 text-xs">
        <div className="flex items-center justify-between p-2 rounded-xl bg-teal-50/50 dark:bg-slate-800/60 border border-teal-100 dark:border-teal-900">
          <span className="font-medium text-[#475569] dark:text-slate-300 shrink-0">Object@Ver:</span>
          <span className="font-mono font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[170px]" title={so?.smartObjectRef}>
            {so?.smartObjectRef || '(not selected)'}
          </span>
        </div>

        <div className="flex items-center justify-between p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800 text-[11px] font-mono">
          <span className="flex items-center gap-1 text-[#475569] dark:text-slate-300">
            <ArrowRightLeft className="w-3.5 h-3.5 text-[#0D9488]" />
            Inputs: <strong className="text-[#0F172A] dark:text-slate-100">{inputCount}</strong>
          </span>
          <span className="text-[#475569] dark:text-slate-300">
            Outputs: <strong className="text-[#0F172A] dark:text-slate-100">{outputCount}</strong>
          </span>
        </div>

        {so?.onBusinessError?.routeTo && (
          <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-[11px] text-amber-800 dark:text-amber-300 font-semibold">
            <ShieldAlert className="w-3.5 h-3.5 shrink-0" />
            <span className="truncate">Error -&gt; {so.onBusinessError.routeTo}</span>
          </div>
        )}
      </div>

      <Handle
        type="source"
        position={Position.Right}
        id="smartobject-out"
        className="!w-3.5 !h-3.5 !bg-[#0D9488] !border-2 !border-white hover:!bg-teal-700 !-right-2 transition-transform hover:scale-125 cursor-pointer shadow-sm"
        title="Connect downstream step"
        aria-label="SmartObject outbound connection handle"
      />
    </div>
  );
});

SmartObjectNode.displayName = 'SmartObjectNode';
