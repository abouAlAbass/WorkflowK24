import React from 'react';
import { GitBranch, Check, X, Plus, Trash2 } from 'lucide-react';
import type { WorkflowNodeCustomData, DecisionCondition, DecisionOperator } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface DecisionConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const DecisionConfig: React.FC<DecisionConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData } = useWorkflowStore();

  const decision = data.decisionData || {
    label: 'Decision Rule',
    conditions: [{ id: 'c1', field: 'Amount', operator: 'greater_than' as DecisionOperator, value: '5000' }],
    logicalOperator: 'AND' as const,
    trueOutcomeLabel: 'Yes (> $5k)',
    falseOutcomeLabel: 'No (<= $5k)'
  };

  const conditions: DecisionCondition[] = decision.conditions && decision.conditions.length > 0
    ? decision.conditions
    : [{ id: 'c1', field: 'Amount', operator: 'greater_than', value: '5000' }];

  const logicalOperator = decision.logicalOperator || 'AND';

  const handleConditionChange = (index: number, fieldKey: keyof DecisionCondition, value: string) => {
    const nextConditions = conditions.map((c, idx) =>
      idx === index ? { ...c, [fieldKey]: value } : c
    );
    updateNodeData(nodeId, {
      decisionData: {
        ...decision,
        conditions: nextConditions
      }
    });
  };

  const handleAddCondition = () => {
    const newCond: DecisionCondition = {
      id: `c-${Date.now()}`,
      field: 'Department',
      operator: 'equals',
      value: 'Finance'
    };
    updateNodeData(nodeId, {
      decisionData: {
        ...decision,
        logicalOperator,
        conditions: [...conditions, newCond]
      }
    });
  };

  const handleRemoveCondition = (index: number) => {
    if (conditions.length <= 1) return;
    updateNodeData(nodeId, {
      decisionData: {
        ...decision,
        conditions: conditions.filter((_, idx) => idx !== index)
      }
    });
  };

  const handleLogicalOperatorChange = (op: 'AND' | 'OR') => {
    updateNodeData(nodeId, {
      decisionData: {
        ...decision,
        logicalOperator: op,
        conditions
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
        <div className="flex items-center justify-between">
          <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
            <GitBranch className="w-3.5 h-3.5 text-[#7C3AED] dark:text-purple-400" />
            <span>Condition Rules ({conditions.length})</span>
          </label>

          {conditions.length > 1 && (
            <div className="inline-flex rounded-lg border border-slate-300 dark:border-slate-700 overflow-hidden text-[10px] font-bold">
              <button
                type="button"
                onClick={() => handleLogicalOperatorChange('AND')}
                className={`px-2.5 py-1 cursor-pointer transition-colors ${
                  logicalOperator === 'AND'
                    ? 'bg-[#2563EB] text-white'
                    : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-300'
                }`}
              >
                AND
              </button>
              <button
                type="button"
                onClick={() => handleLogicalOperatorChange('OR')}
                className={`px-2.5 py-1 cursor-pointer transition-colors ${
                  logicalOperator === 'OR'
                    ? 'bg-[#2563EB] text-white'
                    : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-300'
                }`}
              >
                OR
              </button>
            </div>
          )}
        </div>

        <div className="space-y-3">
          {conditions.map((condition, idx) => {
            const hideValueInput = condition.operator === 'is_empty' || condition.operator === 'is_not_empty';
            return (
              <div key={condition.id || idx} className="space-y-2 p-3.5 rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-2xs relative">
                {conditions.length > 1 && (
                  <div className="flex items-center justify-between pb-1 border-b border-slate-100 dark:border-slate-700/70">
                    <span className="text-[10px] font-mono font-bold text-[#7C3AED] dark:text-purple-400">
                      {idx === 0 ? 'IF' : logicalOperator} — Rule #{idx + 1}
                    </span>
                    <button
                      type="button"
                      onClick={() => handleRemoveCondition(idx)}
                      aria-label={`Remove condition ${idx + 1}`}
                      className="text-slate-400 hover:text-rose-500 p-1 rounded cursor-pointer transition-colors"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                )}

                <div className="space-y-1">
                  <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400">Target Field Variable:</span>
                  <input
                    type="text"
                    value={condition.field}
                    onChange={(e) => handleConditionChange(idx, 'field', e.target.value)}
                    className={`${inputClass} font-mono`}
                    placeholder="e.g. Amount or user.department"
                  />
                </div>

                <div className="space-y-1">
                  <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400">Comparison Operator:</span>
                  <select
                    value={condition.operator}
                    onChange={(e) => handleConditionChange(idx, 'operator', e.target.value)}
                    className={inputClass}
                  >
                    <option value="greater_than">Is Greater Than (&gt;)</option>
                    <option value="greater_than_or_equal">Greater Than or Equal (&gt;=)</option>
                    <option value="less_than">Is Less Than (&lt;)</option>
                    <option value="less_than_or_equal">Less Than or Equal (&lt;=)</option>
                    <option value="equals">Equals (==)</option>
                    <option value="not_equals">Does Not Equal (!=)</option>
                    <option value="contains">Contains Substring</option>
                    <option value="not_contains">Does Not Contain</option>
                    <option value="starts_with">Starts With</option>
                    <option value="ends_with">Ends With</option>
                    <option value="is_empty">Is Empty / Null</option>
                    <option value="is_not_empty">Is Not Empty</option>
                  </select>
                </div>

                {!hideValueInput && (
                  <div className="space-y-1">
                    <span className="text-[10px] font-bold text-[#475569] dark:text-slate-400">Comparison Value:</span>
                    <input
                      type="text"
                      value={condition.value}
                      onChange={(e) => handleConditionChange(idx, 'value', e.target.value)}
                      className={`${inputClass} font-mono`}
                      placeholder="5000"
                    />
                  </div>
                )}
              </div>
            );
          })}
        </div>

        <button
          type="button"
          onClick={handleAddCondition}
          className="w-full py-2 px-3 rounded-xl border border-dashed border-slate-300 dark:border-slate-700 hover:border-blue-500 text-xs font-semibold text-[#2563EB] dark:text-blue-400 flex items-center justify-center gap-1.5 cursor-pointer transition-colors"
        >
          <Plus className="w-3.5 h-3.5" />
          <span>Add Condition Rule</span>
        </button>
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
