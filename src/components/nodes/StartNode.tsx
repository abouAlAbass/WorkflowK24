import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps } from '@xyflow/react';
import { Play, FileText, Clock, Webhook, Database } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

export const StartNode: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const customData = data as unknown as WorkflowNodeCustomData;
  const trigger = customData.triggerData;
  const activeSimNodeId = useWorkflowStore(s => s.activeSimNodeId);
  const isSimActive = activeSimNodeId === id;

  const triggerType = trigger?.triggerType || 'form';

  const getTriggerIcon = () => {
    switch (triggerType) {
      case 'form': return <FileText className="w-5 h-5 text-[#059669] dark:text-emerald-400" />;
      case 'schedule': return <Clock className="w-5 h-5 text-[#059669] dark:text-emerald-400" />;
      case 'webhook': return <Webhook className="w-5 h-5 text-[#059669] dark:text-emerald-400" />;
      case 'item_created': return <Database className="w-5 h-5 text-[#059669] dark:text-emerald-400" />;
      default: return <Play className="w-5 h-5 text-[#059669] dark:text-emerald-400" />;
    }
  };

  return (
    <div
      className={`relative min-w-[280px] rounded-2xl bg-white dark:bg-slate-900 border-2 border-[#059669] transition-all duration-200 shadow-lg shadow-emerald-950/5 text-[#0F172A] dark:text-slate-100 ${
        isSimActive 
          ? 'ring-4 ring-emerald-400 scale-105 shadow-emerald-200' 
          : selected 
            ? 'ring-4 ring-emerald-500/30 shadow-emerald-100 dark:shadow-emerald-950' 
            : 'hover:shadow-xl hover:border-emerald-600'
      }`}
    >
      {/* Node Header */}
      <div className="flex items-center justify-between p-3.5 border-b border-emerald-100 dark:border-emerald-900/60 bg-[#ECFDF5] dark:bg-slate-800/80 rounded-t-[14px]">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-white dark:bg-slate-900 text-[#059669] dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800 shadow-sm">
            {getTriggerIcon()}
          </div>
          <div>
            <span className="text-xs font-extrabold tracking-wider text-[#059669] dark:text-emerald-400 uppercase">
              Start / Trigger
            </span>
            <h3 className="text-sm font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[180px]">
              {customData.label || trigger?.label || 'Workflow Trigger'}
            </h3>
          </div>
        </div>
        {isSimActive && (
          <span className="flex h-2.5 w-2.5 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-[#059669]" />
          </span>
        )}
      </div>

      {/* Node Body Details - Font sizes >= 12px for secondary text */}
      <div className="p-3.5 space-y-2.5 text-xs">
        <div className="flex items-center justify-between p-2 rounded-xl bg-emerald-50/50 dark:bg-slate-800/60 border border-emerald-100 dark:border-emerald-900">
          <span className="text-[#475569] dark:text-slate-300 font-medium text-xs">Trigger Type:</span>
          <span className="font-bold capitalize text-[#059669] dark:text-emerald-400 bg-white dark:bg-slate-800 px-2 py-0.5 rounded-md border border-emerald-200 dark:border-emerald-800 shadow-2xs text-xs">
            {triggerType.replace('_', ' ')}
          </span>
        </div>

        <div className="flex items-center justify-between p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800">
          <span className="text-[#475569] dark:text-slate-300 font-medium text-xs">Event Name:</span>
          <span className="font-mono text-xs font-bold text-[#0F172A] dark:text-slate-100 truncate max-w-[150px]" title={trigger?.eventName}>
            {trigger?.eventName || 'On_Start'}
          </span>
        </div>

        {trigger?.variables && trigger.variables.length > 0 && (
          <div className="pt-1 flex items-center justify-between text-xs text-[#475569] dark:text-slate-400 px-1">
            <span className="font-medium">Payload Vars:</span>
            <span className="bg-slate-100 dark:bg-slate-800 text-[#0F172A] dark:text-slate-200 font-semibold px-2 py-0.5 rounded-full font-mono text-xs border border-slate-200 dark:border-slate-700">
              {trigger.variables.length} fields
            </span>
          </div>
        )}
      </div>

      {/* Connection Source Handle - 14px target size (>=12px) */}
      <Handle
        type="source"
        position={Position.Right}
        id="trigger-out"
        className="!w-3.5 !h-3.5 !bg-[#059669] !border-2 !border-white hover:!bg-emerald-500 !-right-2 transition-transform hover:scale-125 cursor-pointer shadow-sm"
        title="Connect trigger to next workflow step"
        aria-label="Trigger source connection handle"
      />
    </div>
  );
});

StartNode.displayName = 'StartNode';
