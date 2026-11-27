import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { GitMerge } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const ParallelJoinNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const join = customData.joinData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const policy = join?.policy || 'all';
  const onRemaining = join?.onRemaining || 'cancel';
  const mergeStrategy = join?.mergeStrategy || 'error_on_conflict';

  return (
    <div
      className={`relative min-w-[270px] max-w-[310px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#0F766E] transition-all duration-200 shadow-lg shadow-teal-950/5 text-[#0F172A] dark:text-slate-100 ${
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
        id="join-in"
        className="!w-3.5 !h-3.5 !bg-[#0F766E] !border-2 !border-white hover:!bg-teal-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect parallel branches to converge"
        aria-label="Inbound parallel join handle"
      />

      <div className="flex items-center justify-between p-3.5 border-b border-teal-100 dark:border-teal-900/60 bg-teal-50/80 dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#0F766E] dark:text-teal-400 border border-teal-200 dark:border-teal-800 shadow-sm">
            <GitMerge className="w-5 h-5" />
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider text-[#0F766E] dark:text-teal-400 uppercase">
              Parallel Join
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[170px]">
              {customData.label || join?.label || 'Parallel Join'}
            </h3>
          </div>
        </div>
      </div>

      <div className="p-3.5 space-y-2 text-xs">
        <div className="flex items-center justify-between gap-2">
          <span className="text-[#475569] dark:text-slate-400 font-semibold">Policy</span>
          <span className="font-mono font-bold px-2 py-0.5 rounded-lg bg-teal-50 dark:bg-teal-950/60 text-[#0F766E] dark:text-teal-300 border border-teal-200 dark:border-teal-800">
            {policy === 'n_of_m' ? `n_of_m (n=${join?.quorumCount ?? join?.requiredCount ?? 1})` : policy}
          </span>
        </div>
        <div className="flex items-center justify-between gap-2">
          <span className="text-[#475569] dark:text-slate-400 font-semibold">Merge</span>
          <span className="font-mono text-[11px] font-semibold text-[#334155] dark:text-slate-300">
            {mergeStrategy}
          </span>
        </div>
        {policy !== 'all' && (
          <div className="flex items-center justify-between gap-2">
            <span className="text-[#475569] dark:text-slate-400 font-semibold">Remaining</span>
            <span className="font-mono text-[11px] font-semibold text-[#334155] dark:text-slate-300">
              {onRemaining}
            </span>
          </div>
        )}
      </div>

      <Handle
        type="source"
        position={Position.Right}
        id="source-out"
        className="!w-3.5 !h-3.5 !bg-[#0F766E] !border-2 !border-white hover:!bg-teal-700 !-right-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect downstream workflow step"
        aria-label="Outbound target handle"
      />
    </div>
  );
});

ParallelJoinNode.displayName = 'ParallelJoinNode';
