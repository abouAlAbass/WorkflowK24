import React from 'react';
import { Flag } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface EndEventConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const EndEventConfig: React.FC<EndEventConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData } = useWorkflowStore();

  const end = data.endData || {
    label: 'End Workflow',
    status: 'completed',
    message: ''
  };

  const inputClass = "w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB] shadow-2xs transition-all";

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">End Event Title / Label</label>
        <input
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Workflow Completed Successfully"
        />
      </div>

      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <Flag className="w-3.5 h-3.5 text-[#DC2626] dark:text-rose-400" />
          <span>Execution Terminal Status</span>
        </label>
        <select
          value={end.status}
          onChange={(e) => updateNodeData(nodeId, {
            endData: { ...end, status: e.target.value as any }
          })}
          className={inputClass}
        >
          <option value="completed">✅ Completed (Success Path)</option>
          <option value="rejected">❌ Rejected (Denied Path)</option>
          <option value="terminated">⚠️ Terminated (Error / Cancelled)</option>
        </select>
      </div>

      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Completion Return Log Message</label>
        <textarea
          rows={3}
          value={end.message || ''}
          onChange={(e) => updateNodeData(nodeId, {
            endData: { ...end, message: e.target.value }
          })}
          className={inputClass}
          placeholder="e.g. Employee onboarding process executed successfully."
        />
      </div>
    </div>
  );
};
