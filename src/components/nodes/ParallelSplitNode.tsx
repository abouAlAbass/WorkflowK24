import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { GitFork } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const ParallelSplitNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const split = customData.splitData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const mode = split?.mode || 'all';
  const failurePolicy = split?.onBranchFailure || 'fail_instance';

  return (
    <div
      className={`relative min-w-[270px] max-w-[310px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#0284C7] transition-all duration-200 shadow-lg shadow-sky-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive
          ? 'ring-4 ring-sky-400 scale-105 shadow-sky-200'
          : selected
            ? 'ring-4 ring-sky-500/30 shadow-sky-100 dark:shadow-sky-950'
            : 'hover:shadow-xl hover:border-sky-600'
      }`}
    >
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-[#0284C7] !border-2 !border-white hover:!bg-sky-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow step"
        aria-label="Inbound target handle"
      />

      <div className="flex items-center justify-between p-3.5 border-b border-sky-100 dark:border-sky-900/60 bg-sky-50/80 dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#0284C7] dark:text-sky-400 border border-sky-200 dark:border-sky-800 shadow-sm">
            <GitFork className="w-5 h-5" />
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider text-[#0284C7] dark:text-sky-400 uppercase">
              Parallel Split
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[170px]">
              {customData.label || split?.label || 'Parallel Split'}
            </h3>
          </div>
        </div>
      </div>

      <div className="p-3.5 space-y-2 text-xs">
        <div className="flex items-center justify-between gap-2">
          <span className="text-[#475569] dark:text-slate-400 font-semibold">Mode</span>
          <span className="font-mono font-bold px-2 py-0.5 rounded-lg bg-sky-50 dark:bg-sky-950/60 text-[#0284C7] dark:text-sky-300 border border-sky-200 dark:border-sky-800">
            {mode}
          </span>
        </div>
        {mode === 'dynamic_foreach' && split?.collectionExpression && (
          <div className="flex items-center justify-between gap-2">
            <span className="text-[#475569] dark:text-slate-400 font-semibold">Collection</span>
            <span className="font-mono text-[11px] font-bold truncate max-w-[150px] text-[#0F172A] dark:text-slate-200">
              {split.collectionExpression}
            </span>
          </div>
        )}
        <div className="flex items-center justify-between gap-2">
          <span className="text-[#475569] dark:text-slate-400 font-semibold">On Failure</span>
          <span className="font-mono text-[11px] font-semibold text-[#334155] dark:text-slate-300">
            {failurePolicy}
          </span>
        </div>
      </div>

      <Handle
        type="source"
        position={Position.Right}
        id="split-out"
        className="!w-3.5 !h-3.5 !bg-[#0284C7] !border-2 !border-white hover:!bg-sky-700 !-right-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect parallel branches"
        aria-label="Outbound parallel branches handle"
      />
    </div>
  );
});

ParallelSplitNode.displayName = 'ParallelSplitNode';
