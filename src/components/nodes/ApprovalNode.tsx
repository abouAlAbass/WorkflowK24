import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { UserCheck, Users, CheckCircle2 } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const ApprovalNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const approval = customData.approvalData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const assignmentType = approval?.assignmentType || 'user';
  const assignee = approval?.assignee || 'Unassigned';
  const outcomes = approval?.outcomes || [
    { id: 'approve', label: 'Approve', color: '#059669' },
    { id: 'reject', label: 'Reject', color: '#DC2626' }
  ];

  return (
    <div
      className={`relative min-w-[290px] max-w-[340px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#2563EB] transition-all duration-200 shadow-lg shadow-blue-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive 
          ? 'ring-4 ring-amber-400 scale-105 shadow-amber-200' 
          : selected 
            ? 'ring-4 ring-blue-500/30 shadow-blue-100 dark:shadow-blue-950' 
            : 'hover:shadow-xl hover:border-blue-600'
      }`}
    >
      {/* Target Connection Handle (Inbound) - 14px target */}
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-[#2563EB] !border-2 !border-white hover:!bg-blue-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow step"
        aria-label="Inbound target handle"
      />

      {/* Node Header */}
      <div className="flex items-center justify-between p-3.5 border-b border-blue-100 dark:border-blue-900/60 bg-[#EFF6FF] dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#2563EB] dark:text-blue-400 border border-blue-200 dark:border-blue-800 shadow-sm">
            <CheckCircle2 className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider text-[#2563EB] dark:text-blue-400 uppercase">
              Approval Task
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[190px]">
              {customData.label || approval?.label || 'Approval Task'}
            </h3>
          </div>
        </div>
        {isSimActive && (
          <span className="flex h-2.5 w-2.5 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-amber-500" />
          </span>
        )}
      </div>

      {/* Node Content Body - All secondary text >= 12px */}
      <div className="p-3.5 space-y-3">
        {/* Assignee Details */}
        <div className="flex items-center justify-between p-2 rounded-xl bg-blue-50/50 dark:bg-slate-800/60 border border-blue-100 dark:border-blue-900 text-xs">
          <div className="flex items-center gap-2 text-[#475569] dark:text-slate-300">
            {assignmentType === 'user' ? (
              <UserCheck className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
            ) : (
              <Users className="w-4 h-4 text-purple-600 dark:text-purple-400" />
            )}
            <span className="font-medium text-xs">{assignmentType === 'user' ? 'User:' : 'Group:'}</span>
          </div>
          <span className="font-bold text-xs text-[#0F172A] dark:text-slate-100 truncate max-w-[140px]" title={assignee}>
            {assignee}
          </span>
        </div>

        {/* Dynamic Outcomes Container */}
        <div className="space-y-1.5 pt-1">
          <div className="flex items-center justify-between text-xs font-bold text-[#475569] dark:text-slate-400 px-1">
            <span>Decision Outcomes ({outcomes.length})</span>
            <span className="text-xs text-[#2563EB] dark:text-blue-400">Outbound Handles</span>
          </div>

          <div className="space-y-2">
            {outcomes.map((outcome) => (
              <div
                key={outcome.id}
                className="relative flex items-center justify-between p-2 rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200/80 dark:border-slate-700 hover:bg-blue-50/60 dark:hover:bg-slate-700/80 transition-colors group"
              >
                <div className="flex items-center gap-2">
                  <span
                    className="w-3 h-3 rounded-full shrink-0 shadow-sm"
                    style={{ backgroundColor: outcome.color }}
                  />
                  <span className="text-xs font-semibold text-[#0F172A] dark:text-slate-100">
                    {outcome.label}
                  </span>
                </div>

                <Handle
                  type="source"
                  position={Position.Right}
                  id={outcome.id}
                  className="!w-3.5 !h-3.5 !border-2 !border-white hover:scale-125 !-right-2 cursor-pointer shadow-sm transition-transform"
                  style={{ backgroundColor: outcome.color }}
                  title={`Outcome handle: ${outcome.label}`}
                  aria-label={`Outcome handle: ${outcome.label}`}
                />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
});

ApprovalNode.displayName = 'ApprovalNode';
