import React from 'react';
import { GitBranch, Check, X } from 'lucide-react';
import type { WorkflowNodeCustomData, DecisionCondition } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface DecisionConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const DecisionConfig: React.FC<DecisionConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData } = useWorkflowStore();

  const decision = data.decisionData || {
    label: 'Decision Rule',
    conditions: [{ id: 'c1', field: 'Amount', operator: 'greater_than', value: '5000' }],
    trueOutcomeLabel: 'Yes (> $5k)',
    falseOutcomeLabel: 'No (<= $5k)'
  };

  const condition: DecisionCondition = decision.conditions?.[0] || {
    id: 'c1',
    field: 'Amount',
    operator: 'greater_than',
    value: '5000'
  };

  const handleConditionChange = (fieldKey: keyof DecisionCondition, value: string) => {
    const updatedCondition = { ...condition, [fieldKey]: value };
    updateNodeData(nodeId, {
      decisionData: {
        ...decision,
        conditions: [updatedCondition]
      }
    });
  };

  const inputClass = "w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB] shadow-2xs transition-all";

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Decision Title / Label</label>
        <input
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Check Purchase Amount"
        />
      </div>

      <div className="space-y-3">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <GitBranch className="w-3.5 h-3.5 text-[#7C3AED] dark:text-purple-400" />
          <span>If Condition Expression</span>
        </label>

        <div className="space-y-2 p-3.5 rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-2xs">
          <div className="space-y-1">
            <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400">Target Field Variable:</span>
            <input
              type="text"
              value={condition.field}
              onChange={(e) => handleConditionChange('field', e.target.value)}
              className={`${inputClass} font-mono`}
              placeholder="e.g. Amount or Salary"
            />
          </div>

          <div className="space-y-1">
            <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400">Comparison Operator:</span>
            <select
              value={condition.operator}
              onChange={(e) => handleConditionChange('operator', e.target.value)}
              className={inputClass}
            >
              <option value="greater_than">Is Greater Than (&gt;)</option>
              <option value="less_than">Is Less Than (&lt;)</option>
              <option value="equals">Equals (==)</option>
              <option value="not_equals">Does Not Equal (!=)</option>
              <option value="contains">Contains Substring</option>
            </select>
          </div>

          <div className="space-y-1">
            <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400">Comparison Value:</span>
            <input
              type="text"
              value={condition.value}
              onChange={(e) => handleConditionChange('value', e.target.value)}
              className={`${inputClass} font-mono`}
              placeholder="5000"
            />
          </div>
        </div>
      </div>

      <div className="space-y-3 pt-2 border-t border-slate-200 dark:border-slate-800">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Outbound Branch Labels</label>

        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <div className="p-1.5 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800 text-[#059669] dark:text-emerald-400">
              <Check className="w-4 h-4" />
            </div>
            <div className="flex-1">
              <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400 block mb-0.5">True Path Label:</span>
              <input
                type="text"
                value={decision.trueOutcomeLabel || ''}
                onChange={(e) => updateNodeData(nodeId, {
                  decisionData: { ...decision, trueOutcomeLabel: e.target.value }
                })}
                className={inputClass}
                placeholder="Yes / Approved"
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <div className="p-1.5 rounded-lg bg-rose-50 dark:bg-rose-950/60 border border-red-200 dark:border-red-800 text-[#DC2626] dark:text-rose-400">
              <X className="w-4 h-4" />
            </div>
            <div className="flex-1">
              <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400 block mb-0.5">False Path Label:</span>
              <input
                type="text"
                value={decision.falseOutcomeLabel || ''}
                onChange={(e) => updateNodeData(nodeId, {
                  decisionData: { ...decision, falseOutcomeLabel: e.target.value }
                })}
                className={inputClass}
                placeholder="No / Declined"
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
