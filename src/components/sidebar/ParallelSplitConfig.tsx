import React from 'react';
import { GitFork } from 'lucide-react';
import type {
  WorkflowNodeCustomData,
  ParallelSplitMode,
  ParallelFailurePolicy,
  ParallelSplitNodeData
} from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface ParallelSplitConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const ParallelSplitConfig: React.FC<ParallelSplitConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData } = useWorkflowStore();

  const split: ParallelSplitNodeData = data.splitData || {
    mode: 'all',
    itemVariable: 'item',
    onBranchFailure: 'fail_instance'
  };

  const updateSplit = (patch: Partial<ParallelSplitNodeData>) => {
    updateNodeData(nodeId, {
      splitData: {
        ...split,
        ...patch
      }
    });
  };

  const inputClass =
    'w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#0284C7] shadow-2xs transition-all';

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label htmlFor={`split-label-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
          Split Label
        </label>
        <input
          id={`split-label-${nodeId}`}
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Parallel Approvals"
        />
      </div>

      <div className="space-y-1.5">
        <label htmlFor={`split-mode-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <GitFork className="w-3.5 h-3.5 text-[#0284C7] dark:text-sky-400" />
          <span>Split Mode</span>
        </label>
        <select
          id={`split-mode-${nodeId}`}
          value={split.mode}
          onChange={(e) => updateSplit({ mode: e.target.value as ParallelSplitMode })}
          className={inputClass}
        >
          <option value="all">all — Activate all outgoing branches</option>
          <option value="conditional">conditional — Activate matching branches</option>
          <option value="dynamic_foreach">dynamic_foreach — One branch per collection item</option>
        </select>
      </div>

      {split.mode === 'dynamic_foreach' && (
        <div className="space-y-3 p-3 rounded-2xl bg-sky-50/70 dark:bg-sky-950/20 border border-sky-200 dark:border-sky-900">
          <div className="space-y-1.5">
            <label htmlFor={`split-collection-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
              Collection Expression
            </label>
            <input
              id={`split-collection-${nodeId}`}
              type="text"
              value={split.collectionExpression || ''}
              onChange={(e) => updateSplit({ collectionExpression: e.target.value })}
              className={inputClass}
              placeholder="e.g. {{vars.approvers}} or items"
            />
          </div>

          <div className="space-y-1.5">
            <label htmlFor={`split-item-var-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
              Item Variable Name
            </label>
            <input
              id={`split-item-var-${nodeId}`}
              type="text"
              value={split.itemVariable || 'item'}
              onChange={(e) => updateSplit({ itemVariable: e.target.value })}
              className={inputClass}
              placeholder="item"
            />
          </div>
        </div>
      )}

      <div className="space-y-1.5">
        <label htmlFor={`split-failure-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
          On Branch Failure
        </label>
        <select
          id={`split-failure-${nodeId}`}
          value={split.onBranchFailure || 'fail_instance'}
          onChange={(e) => updateSplit({ onBranchFailure: e.target.value as ParallelFailurePolicy })}
          className={inputClass}
        >
          <option value="fail_instance">fail_instance — Fault instance & cancel siblings</option>
          <option value="cancel_siblings">cancel_siblings — Cancel open sibling branches</option>
          <option value="continue_without">continue_without — Continue with remaining branches</option>
        </select>
      </div>
    </div>
  );
};
