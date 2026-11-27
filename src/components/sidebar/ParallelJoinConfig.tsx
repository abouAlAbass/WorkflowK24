import React from 'react';
import { GitMerge } from 'lucide-react';
import type {
  WorkflowNodeCustomData,
  ParallelJoinPolicy,
  ParallelOnRemainingPolicy,
  ParallelMergeStrategy,
  ParallelJoinNodeData
} from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface ParallelJoinConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const ParallelJoinConfig: React.FC<ParallelJoinConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData } = useWorkflowStore();

  const join: ParallelJoinNodeData = data.joinData || {
    policy: 'all',
    onRemaining: 'cancel',
    mergeStrategy: 'error_on_conflict'
  };

  const updateJoin = (patch: Partial<ParallelJoinNodeData>) => {
    updateNodeData(nodeId, {
      joinData: {
        ...join,
        ...patch
      }
    });
  };

  const inputClass =
    'w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#0F766E] shadow-2xs transition-all';

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label htmlFor={`join-label-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
          Join Label
        </label>
        <input
          id={`join-label-${nodeId}`}
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Synchronize Approvals"
        />
      </div>

      <div className="space-y-1.5">
        <label htmlFor={`join-policy-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <GitMerge className="w-3.5 h-3.5 text-[#0F766E] dark:text-teal-400" />
          <span>Synchronization Policy</span>
        </label>
        <select
          id={`join-policy-${nodeId}`}
          value={join.policy}
          onChange={(e) => updateJoin({ policy: e.target.value as ParallelJoinPolicy })}
          className={inputClass}
        >
          <option value="all">all — Wait for all branches</option>
          <option value="any">any — Fire on first completed branch</option>
          <option value="n_of_m">n_of_m — Fire when N branches complete</option>
        </select>
      </div>

      {join.policy === 'n_of_m' && (
        <div className="space-y-1.5">
          <label htmlFor={`join-quorum-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
            Required Branch Count (N)
          </label>
          <input
            id={`join-quorum-${nodeId}`}
            type="number"
            min={1}
            value={join.quorumCount ?? join.requiredCount ?? 1}
            onChange={(e) => {
              const parsed = Math.max(1, parseInt(e.target.value || '1', 10));
              updateJoin({ quorumCount: parsed, requiredCount: parsed });
            }}
            className={inputClass}
          />
        </div>
      )}

      {join.policy !== 'all' && (
        <div className="space-y-1.5">
          <label htmlFor={`join-remaining-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
            On Remaining Branches
          </label>
          <select
            id={`join-remaining-${nodeId}`}
            value={join.onRemaining || 'cancel'}
            onChange={(e) => updateJoin({ onRemaining: e.target.value as ParallelOnRemainingPolicy })}
            className={inputClass}
          >
            <option value="cancel">cancel — Cancel remaining tokens & open tasks</option>
            <option value="let_finish">let_finish — Allow remaining branches to complete</option>
          </select>
        </div>
      )}

      <div className="space-y-1.5">
        <label htmlFor={`join-merge-${nodeId}`} className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
          Variable Merge Strategy
        </label>
        <select
          id={`join-merge-${nodeId}`}
          value={join.mergeStrategy || 'error_on_conflict'}
          onChange={(e) => updateJoin({ mergeStrategy: e.target.value as ParallelMergeStrategy })}
          className={inputClass}
        >
          <option value="error_on_conflict">error_on_conflict — Fail if branches conflict</option>
          <option value="last_writer_wins">last_writer_wins — Latest arrival wins</option>
          <option value="append">append — Combine branch values into array</option>
          <option value="sum">sum — Sum numeric branch values</option>
          <option value="first_non_null">first_non_null — First non-null branch value</option>
        </select>
      </div>
    </div>
  );
};
