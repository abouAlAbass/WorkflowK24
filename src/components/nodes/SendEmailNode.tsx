import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { Mail, Send, Tag } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const SendEmailNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const email = customData.sendEmailData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  return (
    <div
      className={`relative min-w-[280px] max-w-[320px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#D97706] transition-all duration-200 shadow-lg shadow-amber-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive 
          ? 'ring-4 ring-amber-400 scale-105 shadow-amber-200' 
          : selected 
            ? 'ring-4 ring-amber-500/30 shadow-amber-100 dark:shadow-amber-950' 
            : 'hover:shadow-xl hover:border-amber-600'
      }`}
    >
      {/* Target Inbound Handle - 14px target */}
      <Handle
        type="target"
        position={Position.Left}
        id="target-in"
        className="!w-3.5 !h-3.5 !bg-[#D97706] !border-2 !border-white hover:!bg-amber-700 !-left-2 transition-transform hover:scale-125 shadow-sm"
        title="Connect inbound workflow step"
        aria-label="Inbound target handle"
      />

      {/* Header */}
      <div className="flex items-center justify-between p-3.5 border-b border-amber-100 dark:border-amber-900/60 bg-[#FFFBEB] dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#D97706] dark:text-amber-400 border border-amber-200 dark:border-amber-800 shadow-sm">
            <Mail className="w-5 h-5 text-[#D97706] dark:text-amber-400" />
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider text-[#D97706] dark:text-amber-400 uppercase">
              Send Email
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[180px]">
              {customData.label || email?.label || 'Send Email'}
            </h3>
          </div>
        </div>
        {isSimActive && (
          <span className="flex h-2.5 w-2.5 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-[#D97706]" />
          </span>
        )}
      </div>

      {/* Body - Font sizes >= 12px for secondary text */}
      <div className="p-3.5 space-y-2.5 text-xs">
        <div className="flex items-center justify-between p-2 rounded-xl bg-amber-50/50 dark:bg-slate-800/60 border border-amber-100 dark:border-amber-900">
          <div className="flex items-center gap-1.5 text-[#475569] dark:text-slate-300">
            <Send className="w-4 h-4 text-[#D97706] dark:text-amber-400" />
            <span className="font-medium text-xs">To:</span>
          </div>
          <span className="font-mono text-xs font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[170px]" title={email?.to}>
            {email?.to || 'user@example.com'}
          </span>
        </div>

        <div className="p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800 space-y-1">
          <div className="flex items-center gap-1 text-xs text-[#475569] dark:text-slate-400">
            <Tag className="w-3.5 h-3.5 text-[#475569] dark:text-slate-400" />
            <span className="font-medium text-xs">Subject:</span>
          </div>
          <p className="text-xs text-[#0F172A] dark:text-slate-100 truncate font-bold" title={email?.subject}>
            {email?.subject || '(No Subject)'}
          </p>
        </div>
      </div>

      {/* Outbound Handle - 14px target */}
      <Handle
        type="source"
        position={Position.Right}
        id="email-out"
        className="!w-3.5 !h-3.5 !bg-[#D97706] !border-2 !border-white hover:!bg-amber-700 !-right-2 transition-transform hover:scale-125 cursor-pointer shadow-sm"
        title="Connect downstream step"
        aria-label="Email outbound connection handle"
      />
    </div>
  );
});

SendEmailNode.displayName = 'SendEmailNode';
