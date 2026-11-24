import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { GitBranch, Check, X } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const DecisionNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const decision = customData.decisionData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const firstCondition = decision?.conditions?.[0];
  const conditionText = firstCondition 
    ? `${firstCondition.field} ${firstCondition.operator.replace('_', ' ')} ${firstCondition.value}`
    : 'No condition set';

  const trueLabel = decision?.trueOutcomeLabel || 'True';
  const falseLabel = decision?.falseOutcomeLabel || 'False';

  return (
    <div
      className={`relative min-w-[280px] max-w-[320px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#7C3AED] transition-all duration-200 shadow-lg shadow-purple-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive 
          ? 'ring-4 ring-purple-400 scale-105 shadow-purple-200' 
          : selected 
            ? 'ring-4 ring-purple-500/30 shadow-purple-100 dark:shadow-purple-950' 
            : 'hover:shadow-xl hover:border-purple-600'
      }`}
    >
      {/* Target Inbound Handle - 14px target */}
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-[#7C3AED] !border-2 !border-white hover:!bg-purple-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow step"
        aria-label="Inbound target handle"
      />

      {/* Header */}
      <div className="flex items-center justify-between p-3.5 border-b border-purple-100 dark:border-purple-900/60 bg-[#F5F3FF] dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#7C3AED] dark:text-purple-400 border border-purple-200 dark:border-purple-800 shadow-sm">
            <GitBranch className="w-5 h-5 text-[#7C3AED] dark:text-purple-400" />
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider text-[#7C3AED] dark:text-purple-400 uppercase">
              Decision / Rule
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[170px]">
              {customData.label || decision?.label || 'Condition Check'}
            </h3>
          </div>
        </div>
        {isSimActive && (
          <span className="flex h-2.5 w-2.5 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-purple-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-[#7C3AED]" />
          </span>
        )}
      </div>

      {/* Body - Font sizes >= 12px for secondary text */}
      <div className="p-3.5 space-y-3 text-xs">
        <div className="p-2 rounded-xl bg-purple-50/50 dark:bg-slate-800/60 border border-purple-100 dark:border-purple-900 space-y-1">
          <span className="text-xs font-extrabold text-[#7C3AED] dark:text-purple-400 uppercase tracking-wider">
            Rule Condition:
          </span>
          <p className="font-mono text-[#0F172A] dark:text-slate-100 text-xs bg-white dark:bg-slate-800 p-1.5 rounded-lg border border-purple-200 dark:border-purple-800 truncate font-bold shadow-2xs" title={conditionText}>
            {conditionText}
          </p>
        </div>

        <div className="space-y-2 pt-1">
          <div className="relative flex items-center justify-between p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800">
            <div className="flex items-center gap-1.5 text-[#059669] dark:text-emerald-400 font-bold text-xs">
              <Check className="w-4 h-4" />
              <span>{trueLabel}</span>
            </div>
            <Handle
              type="source"
              position={Position.Right}
              id="decision-true"
              style={{
                backgroundColor: '#059669',
                width: '14px',
                height: '14px',
                right: '-8px',
                borderColor: '#ffffff',
                borderWidth: '2px',
                boxShadow: '0 0 6px #05966960'
              }}
              title={`Branch for ${trueLabel}`}
              aria-label={`Branch for ${trueLabel}`}
              className="hover:scale-125 transition-transform cursor-pointer shadow-sm"
            />
          </div>

          <div className="relative flex items-center justify-between p-2 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-red-200 dark:border-red-800">
            <div className="flex items-center gap-1.5 text-[#DC2626] dark:text-rose-400 font-bold text-xs">
              <X className="w-4 h-4" />
              <span>{falseLabel}</span>
            </div>
            <Handle
              type="source"
              position={Position.Right}
              id="decision-false"
              style={{
                backgroundColor: '#DC2626',
                width: '14px',
                height: '14px',
                right: '-8px',
                borderColor: '#ffffff',
                borderWidth: '2px',
                boxShadow: '0 0 6px #DC262660'
              }}
              title={`Branch for ${falseLabel}`}
              aria-label={`Branch for ${falseLabel}`}
              className="hover:scale-125 transition-transform cursor-pointer shadow-sm"
            />
          </div>
        </div>
      </div>
    </div>
  );
});

DecisionNode.displayName = 'DecisionNode';
